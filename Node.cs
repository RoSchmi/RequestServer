using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class Node : IDisposable {
		private class HandlerDefinition {
			public MessageDefinitionAttribute Attribute { get; set; }
			public MessageHandler Handler { get; set; }
		}

		private BlockingCollection<IRequest> incomingMessages;
		private BlockingCollection<IResponse> outgoingMessages;
		private BlockingCollection<Notification> notifications;
		private Dictionary<long, HandlerDefinition> handlers;
		private List<MessageSource> sources;
		private CancellationTokenSource cancellationSource;
		private Task incomingWorker;
		private Task outgoingWorker;
		private Task notificationWorker;
		private AutoResetEvent updateEvent;
		private bool updating;
		private bool disposed;
		private bool running;

		public long RequestsProcessed { get; private set; }
		public long RequestsDropped { get; private set; }
		public long ResponsesDropped { get; private set; }
		public long NotificationsSent { get; private set; }
		public long NotificationsDropped { get; private set; }
		public long PendingRequests => this.incomingMessages.LongCount();
		public long PendingResponses => this.outgoingMessages.LongCount();
		public long PendingNotifications => this.notifications.LongCount();
		public long ConnectionCount => this.sources.Sum(s => s.Connections.Count);

		public long MessageRetryAttempts { get; set; }
		public MessageContext Context { get; set; }
		public Updater Updater { get; set; }
		public IMessageProvider Provider { get; set; }

		public Node() {
			this.MessageRetryAttempts = 5;

			this.sources = new List<MessageSource>();
			this.updateEvent = new AutoResetEvent(false);
			this.updating = false;
			this.disposed = false;
			this.running = false;
		}

		public void AddSource(MessageSource source) {
			if (this.disposed) throw new ObjectDisposedException(nameof(Node));
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (this.running) throw new InvalidOperationException("Cannot add a source when running.");

			source.Provider = this.Provider;

			this.sources.Add(source);
		}

		public void AssociateHandlers(long serverId) {
			if (this.disposed) throw new ObjectDisposedException(nameof(Node));
			if (this.running) throw new InvalidOperationException("Cannot associate when running.");

			var type = typeof(MessageHandler);

			this.handlers = new Dictionary<long, HandlerDefinition>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var classes = assembly.GetTypes().Where(t => t.GetType() != type && type.IsAssignableFrom(t) && t.IsDefined(typeof(MessageDefinitionAttribute)) && t.GetCustomAttribute<MessageDefinitionAttribute>().ServerId == serverId);

				foreach (var c in classes) {
					var attribute = c.GetCustomAttribute<MessageDefinitionAttribute>();
					var handler = (MessageHandler)c.GetConstructor(Type.EmptyTypes).Invoke(null);

					if (this.handlers.ContainsKey(attribute.Id))
						throw new MultiplyDefinedMessageHandlerException() { HandlerId = attribute.Id };

					handler.Context = this.Context;

					this.handlers[attribute.Id] = new HandlerDefinition() { Attribute = attribute, Handler = handler };
				}
			}

			if (this.handlers.Count == 0)
				throw new ArgumentException("No handlers found.", nameof(serverId));
		}

		public void Start() {
			if (this.disposed) throw new ObjectDisposedException(nameof(Node));
			if (this.Provider == null) throw new InvalidOperationException(nameof(this.Provider));
			if (this.sources.Count == 0) throw new InvalidOperationException("No sources defined.");
			if (this.handlers.Count == 0) throw new InvalidOperationException("No handlers associated.");
			if (this.running) throw new InvalidOperationException("Already started.");

			this.running = true;

			this.RequestsProcessed = 0;
			this.RequestsDropped = 0;
			this.ResponsesDropped = 0;
			this.NotificationsSent = 0;
			this.NotificationsDropped = 0;

			this.incomingMessages = new BlockingCollection<IRequest>();
			this.outgoingMessages = new BlockingCollection<IResponse>();
			this.notifications = new BlockingCollection<Notification>();
			this.incomingWorker = new Task(this.ProcessIncomingMessages, TaskCreationOptions.LongRunning);
			this.outgoingWorker = new Task(this.ProcessOutgoingMessages, TaskCreationOptions.LongRunning);
			this.notificationWorker = new Task(this.ProcessNotifications, TaskCreationOptions.LongRunning);
			this.cancellationSource = new CancellationTokenSource();

			this.incomingWorker.Start();
			this.outgoingWorker.Start();
			this.notificationWorker.Start();

			foreach (var s in this.sources) {
				s.Destination = this.incomingMessages;
				s.Start();
			}

			this.Updater?.Start();
		}

		public void Shutdown(int millisecondsToWait) {
			if (this.disposed) throw new ObjectDisposedException(nameof(Node));
			if (!this.running) throw new InvalidOperationException("Not started.");

			this.running = false;

			this.Updater?.Shutdown();

			this.NotifyAll(NotificationCode.ServerShuttingDown);

			Task.Delay(millisecondsToWait).Wait();

			this.cancellationSource.Cancel();

			this.incomingWorker.Wait();
			this.incomingWorker.Dispose();
			this.incomingWorker = null;

			this.outgoingWorker.Wait();
			this.outgoingWorker.Dispose();
			this.outgoingWorker = null;

			this.notificationWorker.Wait();
			this.notificationWorker.Dispose();
			this.notificationWorker = null;

			this.sources.ForEach(s => s.Shutdown());

			this.cancellationSource.Dispose();
			this.cancellationSource = null;

			this.incomingMessages.ToList().ForEach(m => m.Dispose());
			this.incomingMessages.Dispose();
			this.incomingMessages = null;

			this.outgoingMessages.ToList().ForEach(m => m.Dispose());
			this.outgoingMessages.Dispose();
			this.outgoingMessages = null;

			this.notifications.Dispose();
			this.notifications = null;
		}

		private void ProcessIncomingMessages() {
			using (var responseStream = new MemoryStream()) {
				IRequest request;

				while (!this.cancellationSource.IsCancellationRequested) {
					var response = this.Provider.CreateResponse();

					if (this.updating)
						this.updateEvent.WaitOne();

					try {
						request = this.incomingMessages.Take(this.cancellationSource.Token);
					}
					catch (OperationCanceledException) {
						break;
					}

					if (this.handlers.ContainsKey(request.RequestId)) {
						var def = this.handlers[request.RequestId];

						if (request.Connection.AuthenticatedLevel >= def.Attribute.AuthenticationLevelRequired) {
							def.Handler.AuthenticatedId = request.Connection.AuthenticatedId;
							def.Handler.AuthenticatedLevel = request.Connection.AuthenticatedLevel;

							def.Handler.Context?.BeginMessage();

							try {
								def.Handler.Deserialize(MessageParameterDirection.Input, request.Body);

								if (def.Handler.Valid) {
									response.ResponseCode = def.Handler.Perform();

									def.Handler.Context?.SaveChanges();

									request.Connection.AuthenticatedId = def.Handler.AuthenticatedId;
									request.Connection.AuthenticatedLevel = def.Handler.AuthenticatedLevel;
								}
								else {
									response.ResponseCode = ResponseCode.ParameterValidationFailed;
								}

							}
							catch (EndOfStreamException) {
								response.ResponseCode = ResponseCode.WrongParameterNumber;
							}
							catch (MessageContextSaveFailedException e) {
								if (e.CanRetryMessage) {
									if (++request.ProcessAttempts <= this.MessageRetryAttempts) {
										this.incomingMessages.Add(request);
									}
									else {
										response.ResponseCode = ResponseCode.TryAgainLater;

										this.RequestsDropped++;
									}
								}
								else {
									response.ResponseCode = e.ResponseCode;

									this.RequestsDropped++;
								}
							}

							def.Handler.Context?.EndMessage();

							if (response.ResponseCode == ResponseCode.Success)
								def.Handler.Serialize(MessageParameterDirection.Output, responseStream);

							foreach (var n in def.Handler.GeneratedNotifications)
								this.notifications.Add(n);

							def.Handler.GeneratedNotifications.Clear();
						}
						else {
							response.ResponseCode = ResponseCode.NotAuthorized;
						}
					}
					else {
						response.ResponseCode = ResponseCode.WrongRequestId;
					}

					response.Connection = request.Connection;
					response.TransactionId = request.TransactionId;
					response.BodyLength = responseStream.Position;
					response.SerializeHeader();

					responseStream.CopyTo(response.Body);
					responseStream.Seek(0, SeekOrigin.Begin);
					responseStream.SetLength(0);

					request.Dispose();

					this.outgoingMessages.Add(response);
				}
			}
		}

		private async void ProcessOutgoingMessages() {
			IResponse response;

			while (!this.cancellationSource.IsCancellationRequested) {
				try {
					response = this.outgoingMessages.Take(this.cancellationSource.Token);
				}
				catch (OperationCanceledException) {
					break;
				}

				using (response) {
					if (await response.Connection.Send(response)) {
						this.RequestsProcessed++;
					}
					else {
						if (++response.SendAttempts <= this.MessageRetryAttempts) {
							this.outgoingMessages.Add(response);
						}
						else {
							this.ResponsesDropped++;
						}
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

				using (var message = this.Provider.CreateNotification(notification.Type, notification.ObjectId)) {
					if (notification.Connection != null) {
						if (await notification.Connection.Send(message)) {
							this.NotificationsSent++;
						}
						else {
							this.NotificationsDropped++;
						}
					}
					else {
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
			}
		}

		private void NotifyAll(long notificationType) {
			foreach (var s in this.sources)
				lock (s.Connections)
					foreach (var c in s.Connections)
						this.notifications.Add(new Notification(c, notificationType, 0));
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

		protected virtual void Dispose(bool disposing) {
			if (this.disposed)
				return;

			if (disposing) {
				if (this.running)
					this.Shutdown(1000);

				this.updateEvent.Dispose();
				this.updateEvent = null;
			}

			this.disposed = true;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}