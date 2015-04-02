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

		public long MessageRetryAttempts { get; set; }
		public long MessagesProcessed { get; set; }
		public long RequestsDropped { get; set; }
		public long ResponsesDropped { get; set; }
		public long NotificationsSent { get; set; }
		public long NotificationsDropped { get; set; }
		public long PendingIncomingMessages => this.incomingMessages.LongCount();
		public long PendingOutgoingMessages => this.outgoingMessages.LongCount() + this.notifications.LongCount();
		public long ConnectionCount => this.sources.Sum(s => s.Connections.Count);

		public MessageContext Context { get; set; }
		public Updater Updater { get; set; }
		public IMessageFormat MessageFormat { get; set; }

		public Node() {
			this.MessageRetryAttempts = 5;

			this.handlers = new Dictionary<long, HandlerDefinition>();
			this.sources = new List<MessageSource>();
			this.updateEvent = new AutoResetEvent(false);
			this.updating = false;
		}

		public void AddSource(MessageSource source) {
			source.MessageDestination = this.incomingMessages;

			this.sources.Add(source);
		}

		public void AssociateMessages(long serverId) {
			var type = typeof(MessageHandler);

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var classes = assembly.GetTypes().Where(t => t.GetType() != type && type.IsAssignableFrom(t) && t.IsDefined(typeof(MessageDefinitionAttribute)) && t.GetCustomAttribute<MessageDefinitionAttribute>().ServerId == serverId);

				foreach (var c in classes) {
					var attribute = c.GetCustomAttribute<MessageDefinitionAttribute>();
					var handler = (MessageHandler)c.GetConstructor(Type.EmptyTypes).Invoke(null);

					if (this.handlers.ContainsKey(attribute.Id))
						throw new InvalidOperationException($"The message with id {attribute.Id} is already defined.");

					handler.Context = this.Context;

					this.handlers[attribute.Id] = new HandlerDefinition() { Attribute = attribute, Handler = handler };
				}
			}
		}

		public void Start() {
			this.MessagesProcessed = 0;
			this.RequestsDropped = 0;
			this.ResponsesDropped = 0;
			this.NotificationsSent = 0;
			this.NotificationsDropped = 0;

			this.outgoingMessages = new BlockingCollection<IMessage>();
			this.incomingMessages = new BlockingCollection<IMessage>();
			this.notifications = new BlockingCollection<Notification>();
			this.incomingWorker = new Task(this.ProcessIncomingMessages, TaskCreationOptions.LongRunning);
			this.outgoingWorker = new Task(this.ProcessOutgoingMessages, TaskCreationOptions.LongRunning);
			this.notificationWorker = new Task(this.ProcessNotifications, TaskCreationOptions.LongRunning);
			this.cancellationSource = new CancellationTokenSource();

			this.incomingWorker.Start();
			this.outgoingWorker.Start();
			this.notificationWorker.Start();

			this.sources.ForEach(s => s.Start());

			this.Updater?.Start();
		}

		public void Stop(int millisecondsToWait) {
			this.Updater?.Stop();

			this.NotifyAll(NotificationCode.ServerShuttingDown);

			Task.Delay(millisecondsToWait).Wait();

			this.cancellationSource.Cancel();

			this.incomingWorker.Wait();
			this.outgoingWorker.Wait();
			this.notificationWorker.Wait();

			this.sources.ForEach(s => s.Stop());
		}

		private void ProcessIncomingMessages() {
			using (var responseStream = new MemoryStream()) {
				IMessage message;

				while (!this.cancellationSource.IsCancellationRequested) {
					if (this.updating)
						this.updateEvent.WaitOne();

					try {
						message = this.incomingMessages.Take(this.cancellationSource.Token);
					}
					catch (OperationCanceledException) {
						break;
					}

					if (this.handlers.ContainsKey(message.RequestId)) {
						var def = this.handlers[message.RequestId];

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

										this.RequestsDropped++;
									}
								}
								else {
									message.ResponseCode = e.ResponseCode;

									this.RequestsDropped++;
								}
							}

							def.Handler.Context.EndMessage();

							if (message.ResponseCode == ResponseCode.Success)
								def.Handler.Serialize(MessageParameterDirection.Output, responseStream);

							foreach (var n in def.Handler.GeneratedNotifications)
								this.notifications.Add(n);

							def.Handler.GeneratedNotifications.Clear();
						}
						else {
							message.ResponseCode = ResponseCode.NotAuthorized;
						}
					}
					else {
						message.ResponseCode = ResponseCode.WrongRequestId;
					}

					message.BodyLength = responseStream.Position;
					message.Body.Seek(0, SeekOrigin.Begin);

					responseStream.CopyTo(message.Body);
					responseStream.Seek(0, SeekOrigin.Begin);

					this.outgoingMessages.Add(message);
				}
			}
		}

		private async void ProcessOutgoingMessages() {
			IMessage message;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					message = this.outgoingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				if (await message.Connection.Send(message)) {
					this.MessagesProcessed++;
				}
				else {
					if (++message.SendAttempts <= this.MessageRetryAttempts) {
						this.outgoingMessages.Add(message);
					}
					else {
						this.ResponsesDropped++;
					}
				}
			}
		}

		private async void ProcessNotifications() {
			Notification notification;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					notification = this.notifications.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				var message = this.MessageFormat.CreateNotification(notification.Type, notification.ObjectId);

				foreach (var c in this.FindConnectionsForAuthenticatedId(notification.TargetAuthenticatedId)) {
					if (await c.Send(message)) {
						this.NotificationsSent++;
					}
					else {
						this.NotificationsDropped++;
					}
				}

			}
		}

		private void NotifyAll(long notificationType) {
			var message = this.MessageFormat.CreateNotification(notificationType, 0);

			foreach (var s in this.sources)
				lock (s.Connections)
					foreach (var c in s.Connections)
						this.outgoingMessages.Add(this.MessageFormat.CreateMessage(c, message.Header));
		}

		private List<Connection> FindConnectionsForAuthenticatedId(long authenticatedId) {
			var list = new List<Connection>();

			foreach (var s in this.sources)
				lock (s.Connections)
					list.AddRange(s.Connections.Where(c => c.AuthenticatedId == authenticatedId));

			return list;
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
	}
}