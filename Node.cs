using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class Node<ContextType> where ContextType : MessageContext {
		private BlockingCollection<Message> outgoingMessages;
		private BlockingCollection<Message> incomingMessages;
		private BlockingCollection<Notification> notifications;
		private Dictionary<uint, MessageHandler<ContextType>> handlers;
		private List<MessageSource> sources;
		private CancellationTokenSource cancellationSource;
		private Task incomingWorker;
		private Task outgoingWorker;
		private Task notificationWorker;

		public int MessageRetryAttempts { get; set; } = 5;
		public long ReceivedMessages { get; set; }
		public long SentMessages { get; set; }
		public int PendingIncomingMessages { get { return this.incomingMessages.Count; } }
		public int PendingOutgoingMessages { get { return this.outgoingMessages.Count + this.notifications.Count; } }
		public long ConnectionCount { get { return this.sources.Sum(s => s.Connections.Count); } }

		public ContextType Context { get; set; }
		public Updater<ContextType> Updater { get; set; }

		public Node() {
			this.outgoingMessages = new BlockingCollection<Message>();
			this.incomingMessages = new BlockingCollection<Message>();
			this.notifications = new BlockingCollection<Notification>();
			this.handlers = new Dictionary<uint, MessageHandler<ContextType>>();
			this.sources = new List<MessageSource>();
			this.incomingWorker = new Task(this.ProcessIncomingMessages, TaskCreationOptions.LongRunning);
			this.outgoingWorker = new Task(this.ProcessOutgoingMessages, TaskCreationOptions.LongRunning);
			this.notificationWorker = new Task(this.ProcessNotifications, TaskCreationOptions.LongRunning);
			this.cancellationSource = new CancellationTokenSource();
		}

		public void AddSource(MessageSource source) {
			source.CancellationToken = this.cancellationSource.Token;
			source.MessageDestination = this.incomingMessages;

			this.sources.Add(source);
		}

		public void AssociateServerRequests(int serverId) {
			var type = typeof(MessageHandler<ContextType>);

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var childClasses = assembly.GetTypes().Where(t => t.GetType() != type && type.IsAssignableFrom(t));
				var matchingClasses = childClasses.Where(c => c.IsDefined(typeof(MessageServerAttribute)) && c.GetCustomAttribute<MessageServerAttribute>().ServerId == serverId);

				foreach (var c in matchingClasses) {
					var handler = (MessageHandler<ContextType>)c.GetConstructor(Type.EmptyTypes).Invoke(null);
					var key = handler.GetKey();

					if (this.handlers.ContainsKey(key))
						throw new InvalidOperationException("This method is already defined.");

					handler.Context = this.Context;

					this.handlers.Add(key, handler);
				}
			}
		}

		public void Start() {
			this.incomingWorker.Start();
			this.outgoingWorker.Start();
			this.notificationWorker.Start();

			this.sources.ForEach(s => s.Start());

			if (this.Updater != null) {
				this.Updater.CancellationToken = this.cancellationSource.Token;
				this.Updater.Start();
			}
		}

		public void Stop() {
			this.cancellationSource.Cancel();

			this.sources.ForEach(s => s.Stop());

			this.incomingWorker.Wait();
			this.outgoingWorker.Wait();
			this.notificationWorker.Wait();

			this.Updater?.Stop();
		}

		private void ProcessIncomingMessages() {
			Message message;
			var requestHeader = new RequestHeader();
			var responseHeader = new ResponseHeader();
			var responseBuffer = new byte[MessageHeader.Length + MessageHeader.MaxBodyLength];
			var responseWriter = new BinaryWriter(new MemoryStream(responseBuffer));

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					message = this.incomingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				this.ReceivedMessages++;

				responseWriter.Seek(MessageHeader.Length, SeekOrigin.Begin);

				using (var requestReader = new BinaryReader(new MemoryStream(message.Data))) {

					requestHeader.Deserialize(requestReader);

					requestReader.BaseStream.Seek(MessageHeader.Length, SeekOrigin.Begin);

					if (requestHeader.BodyLength + MessageHeader.Length == message.Data.Length) {
						var key = MessageHandler<ContextType>.GetKey(requestHeader.Category, requestHeader.Method);

						if (this.handlers.ContainsKey(key)) {
							var handler = this.handlers[key];

							handler.AuthenticatedId = message.Connection.AuthenticatedId;

							try {
								handler.Deserialize<MessageInputAttribute>(requestReader);

								handler.Context.BeginMessage();

								if (handler.IsValid()) {
									responseHeader.ResponseCode = handler.Perform();

									handler.Context.SaveChanges();

									message.Connection.AuthenticatedId = handler.AuthenticatedId;
								}
								else {
									responseHeader.ResponseCode = ResponseCode.ParameterValidationFailed;
								}

							}
							catch (EndOfStreamException) {
								responseHeader.ResponseCode = ResponseCode.WrongParameterNumber;
							}
							catch (MessageContextSaveFailedException e) {
								if (e.CanRetry) {
									if (++message.ProcessAttempts <= this.MessageRetryAttempts) {
										this.incomingMessages.Add(message);
									}
									else {
										responseHeader.ResponseCode = ResponseCode.TryAgainLater;
									}
								}
								else {
									responseHeader.ResponseCode = e.ResponseCode;
								}
							}

							handler.Context.EndMessage();

							if (responseHeader.ResponseCode == ResponseCode.Success)
								handler.Serialize<MessageOutputAttribute>(responseWriter);

							foreach (var n in handler.Notifications)
								this.notifications.Add(n);

							this.SentMessages += handler.Notifications.Count;

							handler.Notifications.Clear();
						}
						else {
							responseHeader.ResponseCode = ResponseCode.WrongMethod;
						}
					}
					else {
						responseHeader.ResponseCode = ResponseCode.WrongParameterNumber;
					}

					responseHeader.Id = requestHeader.Id;
					responseHeader.BodyLength = (ushort)(responseWriter.BaseStream.Position - MessageHeader.Length);

					responseWriter.Seek(0, SeekOrigin.Begin);

					responseHeader.Serialize(responseWriter);

					this.outgoingMessages.Add(new Message(message.Connection, responseBuffer, responseHeader.BodyLength + MessageHeader.Length));
					this.SentMessages++;
				}
			}
		}

		private void ProcessOutgoingMessages() {
			Message message;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					message = this.outgoingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				if (!message.Connection.Send(message) && ++message.SendAttempts <= this.MessageRetryAttempts)
					this.outgoingMessages.Add(message);
			}
		}

		private void ProcessNotifications() {
			Notification notification;
			var buffer = new byte[MessageHeader.Length];
			var writer = new BinaryWriter(new MemoryStream(buffer));

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					notification = this.notifications.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				foreach (var c in this.FindConnectionsForAuthenticatedId(notification.TargetAuthenticatedId)) {
					writer.Seek(0, SeekOrigin.Begin);

					notification.Serialize(writer);

					this.outgoingMessages.Add(new Message(c, buffer, buffer.Length));

				}
			}
		}

		private List<Connection> FindConnectionsForAuthenticatedId(long authenticatedId) {
			var list = new List<Connection>();

			foreach (var s in this.sources)
				lock (s.Connections)
					list.AddRange(s.Connections.Where(c => c.AuthenticatedId == authenticatedId));

			return list;
		}
	}
}