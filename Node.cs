using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class Node {
		private class HandlerDefinition {
			public MessageDefinitionAttribute Attribute { get; set; }
			public MessageHandler Handler { get; set; }
		}

		private BlockingCollection<IMessage> outgoingMessages;
		private BlockingCollection<IMessage> incomingMessages;
		private BlockingCollection<Notification> notifications;
		private Dictionary<long, HandlerDefinition> handlers;
		private List<MessageSource> sources;
		private CancellationTokenSource cancellationSource;
		private Task incomingWorker;
		private Task outgoingWorker;
		private Task notificationWorker;
		private AutoResetEvent updateEvent;
		private bool updating;

		public long MessageRetryAttempts { get; set; } = 5;
		public long ReceivedMessages { get; set; }
		public long SentMessages { get; set; }
		public long PendingIncomingMessages => this.incomingMessages.LongCount();
		public long PendingOutgoingMessages => this.outgoingMessages.LongCount() + this.notifications.LongCount();
		public long ConnectionCount => this.sources.Sum(s => s.Connections.Count);

		public MessageContext Context { get; set; }
		public Updater Updater { get; set; }
		public IMessageFormat MessageFormat { get; set; }

		public Node() {
			this.outgoingMessages = new BlockingCollection<IMessage>();
			this.incomingMessages = new BlockingCollection<IMessage>();
			this.notifications = new BlockingCollection<Notification>();
			this.handlers = new Dictionary<long, HandlerDefinition>();
			this.sources = new List<MessageSource>();
			this.incomingWorker = new Task(this.ProcessIncomingMessages, TaskCreationOptions.LongRunning);
			this.outgoingWorker = new Task(this.ProcessOutgoingMessages, TaskCreationOptions.LongRunning);
			this.notificationWorker = new Task(this.ProcessNotifications, TaskCreationOptions.LongRunning);
			this.cancellationSource = new CancellationTokenSource();
			this.updateEvent = new AutoResetEvent(false);
			this.updating = false;
		}

		public void AddSource(MessageSource source) {
			source.ReceivedMessages = this.incomingMessages;

			this.sources.Add(source);
		}

		public void AssociateServerRequests(long serverId) {
			var type = typeof(MessageHandler);

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var childClasses = assembly.GetTypes().Where(t => t.GetType() != type && type.IsAssignableFrom(t));
				var matchingClasses = childClasses.Where(c => c.IsDefined(typeof(MessageDefinitionAttribute)) && ((c.GetCustomAttribute<MessageDefinitionAttribute>().ServerId & serverId) != 0));

				foreach (var c in matchingClasses) {
					var attribute = c.GetCustomAttribute<MessageDefinitionAttribute>();
					var handler = (MessageHandler)c.GetConstructor(Type.EmptyTypes).Invoke(null);

					if (this.handlers.ContainsKey(attribute.Id))
						throw new InvalidOperationException("This method is already defined.");

					handler.Context = this.Context;

					this.handlers[attribute.Id] = new HandlerDefinition() { Attribute = attribute, Handler = handler };
				}
			}
		}

		public void Start() {
			this.incomingWorker.Start();
			this.outgoingWorker.Start();
			this.notificationWorker.Start();

			this.sources.ForEach(s => s.Start());

			this.Updater?.Start();
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
			IMessage message;
			MemoryStream responseStream = new MemoryStream();

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

				var handlers = this.handlers;

				if (handlers.ContainsKey(message.RequestId)) {
					var def = handlers[message.RequestId];

					if (message.Connection.AuthenticatedLevel >= def.Attribute.AuthenticationLevelRequired) {
						def.Handler.AuthenticatedId = message.Connection.AuthenticatedId;
						def.Handler.AuthenticatedLevel = message.Connection.AuthenticatedLevel;

						def.Handler.Context.BeginMessage();

						try {
							def.Handler.Deserialize(MessageParameterDirection.Input, message.Body);

							if (def.Handler.Valid) {
								message.ResponseCode = def.Handler.Perform();

								def.Handler.Context.SaveChanges();

								message.Connection.AuthenticatedId = def.Handler.AuthenticatedId;
								message.Connection.AuthenticatedLevel = def.Handler.AuthenticatedLevel;
							}
							else {
								message.ResponseCode = ResponseCode.ParameterValidationFailed;
							}

						}
						catch (EndOfStreamException) {
							message.ResponseCode = ResponseCode.WrongParameterNumber;
						}
						catch (MessageContextSaveFailedException e) {
							if (e.CanRetryMessage) {
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

						def.Handler.Context.EndMessage();

						if (message.ResponseCode == ResponseCode.Success)
							def.Handler.Serialize(MessageParameterDirection.Output, responseStream);


						foreach (var n in def.Handler.GeneratedNotifications)
							this.notifications.Add(n);

						this.SentMessages += def.Handler.GeneratedNotifications.Count;

						def.Handler.GeneratedNotifications.Clear();
					}
					else {
						message.ResponseCode = ResponseCode.NotAuthorized;
					}
				}
				else {
					message.ResponseCode = ResponseCode.WrongMethod;
				}

				message.BodyLength = (ushort)(responseStream.Position);
				responseStream.CopyTo(message.Body);
				responseStream.Seek(0, SeekOrigin.Begin);

				this.outgoingMessages.Add(message);
				this.SentMessages++;
			}
		}

		private async void ProcessOutgoingMessages() {
			IMessage message;

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

		private void NotifyAll(long notificationType) {
			var notification = this.MessageFormat.CreateNotification(notificationType, 0);

			foreach (var s in this.sources)
				lock (s.Connections)
					foreach (var c in s.Connections)
						this.outgoingMessages.Add(this.MessageFormat.CreateMessage(c, notification.Header));
		}

		private void ProcessNotifications() {
			Notification notification;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					notification = this.notifications.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				var message = this.MessageFormat.CreateNotification(notification.NotificationType, notification.ObjectId);

				foreach (var c in this.FindConnectionsForAuthenticatedId(notification.TargetAuthenticatedId))
					this.outgoingMessages.Add(this.MessageFormat.CreateMessage(c, message.Header));
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