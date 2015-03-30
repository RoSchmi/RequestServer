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
		private BlockingCollection<Tuple<long, Message>> notifications;
		private Dictionary<uint, MessageHandler<ContextType>> authenticatedHandlers;
		private Dictionary<uint, MessageHandler<ContextType>> unauthenticatedHandlers;
		private List<MessageSource> sources;
		private CancellationTokenSource cancellationSource;
		private Task incomingWorker;
		private Task outgoingWorker;
		private Task notificationWorker;
		private AutoResetEvent updateEvent;
		private bool updating;

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
			this.notifications = new BlockingCollection<Tuple<long, Message>>();
			this.authenticatedHandlers = new Dictionary<uint, MessageHandler<ContextType>>();
			this.unauthenticatedHandlers = new Dictionary<uint, MessageHandler<ContextType>>();
			this.sources = new List<MessageSource>();
			this.incomingWorker = new Task(this.ProcessIncomingMessages, TaskCreationOptions.LongRunning);
			this.outgoingWorker = new Task(this.ProcessOutgoingMessages, TaskCreationOptions.LongRunning);
			this.notificationWorker = new Task(this.ProcessNotifications, TaskCreationOptions.LongRunning);
			this.cancellationSource = new CancellationTokenSource();
			this.updateEvent = new AutoResetEvent(false);
			this.updating = false;
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
				var matchingClasses = childClasses.Where(c => c.IsDefined(typeof(MessageDefinitionAttribute)) && ((c.GetCustomAttribute<MessageDefinitionAttribute>().ServerId & serverId) != 0));

				foreach (var c in matchingClasses) {
					var handler = (MessageHandler<ContextType>)c.GetConstructor(Type.EmptyTypes).Invoke(null);
					var key = handler.GetKey();
					var handlers = c.GetCustomAttribute<MessageDefinitionAttribute>().AuthenticationRequired ? this.authenticatedHandlers : this.unauthenticatedHandlers;

					if (handlers.ContainsKey(key))
						throw new InvalidOperationException("This method is already defined.");

					handler.Context = this.Context;

					handlers.Add(key, handler);
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
			this.NotifyAll(NotificationCode.ServerShuttingDown);

			this.cancellationSource.Cancel();

			this.incomingWorker.Wait();
			this.outgoingWorker.Wait();
			this.notificationWorker.Wait();

			this.sources.ForEach(s => s.Stop());

			this.Updater?.Stop();
		}

		internal void OnUpdateStarted() {
			this.updating = true;

			this.NotifyAll(NotificationCode.UpdateStarted);
		}

		internal void OnUpdateFinished() {
			this.NotifyAll(NotificationCode.UpdateFinished);

			this.updating = false;

			this.updateEvent.Set();
		}

		private void ProcessIncomingMessages() {
			Message message;
			BinaryWriter responseWriter = new BinaryWriter(new MemoryStream());

			while (!this.cancellationSource.IsCancellationRequested) {
				if (this.updating)
					this.updateEvent.WaitOne();

				try {
					message = this.incomingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				this.ReceivedMessages++;

				var key = MessageHandler<ContextType>.GetKey(message.RequestCategory, message.RequestMethod);
				var handlers = message.Connection.AuthenticatedId != 0 ? this.authenticatedHandlers : this.unauthenticatedHandlers;
				var opposite = message.Connection.AuthenticatedId != 0 ? this.unauthenticatedHandlers : this.authenticatedHandlers;

				if (handlers.ContainsKey(key)) {
					var handler = handlers[key];

					handler.AuthenticatedId = message.Connection.AuthenticatedId;

					handler.Context.BeginMessage();

					try {
						using (var reader = new BinaryReader(message.Body))
							handler.Deserialize<MessageInputAttribute>(reader);

						if (handler.IsValid()) {
							message.ResponseCode = handler.Perform();

							handler.Context.SaveChanges();

							message.Connection.AuthenticatedId = handler.AuthenticatedId;
						}
						else {
							message.ResponseCode = ResponseCode.ParameterValidationFailed;
						}

					}
					catch (EndOfStreamException) {
						message.ResponseCode = ResponseCode.WrongParameterNumber;
					}
					catch (MessageContextSaveFailedException e) {
						if (e.CanRetry) {
							if (++message.ProcessAttempts <= this.MessageRetryAttempts) {
								this.incomingMessages.Add(message);
							}
							else {
								message.ResponseCode = ResponseCode.TryAgainLater;
							}
						}
						else {
							message.ResponseCode = e.ResponseCode;
						}
					}

					handler.Context.EndMessage();

					if (message.ResponseCode == ResponseCode.Success)
						handler.Serialize<MessageOutputAttribute>(responseWriter);

					foreach (var n in handler.Notifications)
						this.notifications.Add(n);

					this.SentMessages += handler.Notifications.Count;

					handler.Notifications.Clear();
				}
				else if (opposite.ContainsKey(key)) {
					message.ResponseCode = ResponseCode.NotAuthorized;
				}
				else {
					message.ResponseCode = ResponseCode.WrongMethod;
				}

				message.BodyLength = (ushort)(responseWriter.BaseStream.Position);
				responseWriter.BaseStream.CopyTo(message.Body);

				this.outgoingMessages.Add(message);
				this.SentMessages++;
			}
		}

		private async void ProcessOutgoingMessages() {
			Message message;

			while (true) {
				try {
					message = this.outgoingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				if (!(await message.Connection.Send(message)) && ++message.SendAttempts <= this.MessageRetryAttempts)
					this.outgoingMessages.Add(message);

				if (this.cancellationSource.IsCancellationRequested && this.outgoingMessages.Count == 0)
					break;
			}
		}

		private void NotifyAll(ushort notificationType) {
			var message = Message.CreateNotification(notificationType, 0);

			foreach (var s in this.sources)
				lock (s.Connections)
					foreach (var c in s.Connections)
						this.outgoingMessages.Add(new Message(c, message.Header));
		}

		private void ProcessNotifications() {
			Tuple<long, Message> notification;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					notification = this.notifications.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				foreach (var c in this.FindConnectionsForAuthenticatedId(notification.Item1))
					this.outgoingMessages.Add(new Message(c, notification.Item2.Header));
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