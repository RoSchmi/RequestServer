using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class MessageSource : IDisposable {
		private List<Connection> connections;
		private Task worker;
		private bool disposed;

		protected bool Running { get; private set; }

		public BlockingCollection<IMessage> MessageDestination { get; internal set; }
		public IMessageFormat MessageFormat { get; internal set; }
		public IReadOnlyCollection<Connection> Connections => this.connections;

		protected abstract Task<Connection> AcceptConnection();

		protected MessageSource() {
			this.connections = new List<Connection>();
			this.disposed = false;

			this.Running = false;
		}

		public virtual void Start() {
			if (this.Running) throw new InvalidOperationException("Already started.");

			this.Running = true;

			this.worker = new Task(this.AcceptConnections, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public virtual void Shutdown() {
			if (!this.Running) throw new InvalidOperationException("Not started.");

			this.Running = false;

			this.worker.Wait();
			this.worker.Dispose();
			this.worker = null;
		}

		private async void AcceptConnections() {
			while (this.Running)
				this.ProcessConnection(await this.AcceptConnection());
		}

		private async void ProcessConnection(Connection connection) {
			using (connection) {
				if (connection == null)
					return;

				connection.MessageFormat = this.MessageFormat;

				this.connections.Add(connection);

				while (this.Running) {
					var message = await connection.Receive();

					if (message == null)
						break;

					this.MessageDestination.Add(message);
				}

				connection.Open = false;

				this.connections.Remove(connection);
			}
		}

		protected virtual void Dispose(bool disposing) {
			if (this.disposed)
				return;

			if (disposing && this.Running)
				this.Shutdown();

			this.disposed = true;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}