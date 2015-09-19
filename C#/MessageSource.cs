using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class MessageSource : IDisposable {
		private List<Connection> connections;
		private Task worker;
		private bool disposed;

		protected bool Running { get; private set; }

		public BlockingCollection<IRequest> Destination { get; internal set; }
		public IMessageProvider Provider { get; internal set; }
		public IReadOnlyCollection<Connection> Connections => this.connections;

		protected abstract Task<Connection> AcceptConnection();
		public abstract void Shutdown();

		protected MessageSource() {
			this.connections = new List<Connection>();
			this.disposed = false;

			this.Running = false;
		}

		public virtual void Start() {
			if (this.disposed) throw new ObjectDisposedException(nameof(MessageSource));
			if (this.Running) throw new InvalidOperationException("Already started.");
			if (this.Destination == null) throw new InvalidOperationException(nameof(this.Destination));
			if (this.Provider == null) throw new InvalidOperationException(nameof(this.Provider));

			this.Running = true;

			this.worker = new Task(this.AcceptConnections, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		protected void BeginShutdown() {
			this.Running = false;
		}

		protected void EndShutdown() {
			this.worker.Wait();
			this.worker.Dispose();
			this.worker = null;

			this.connections.ForEach(c => c.Dispose());
		}

		private async void AcceptConnections() {
			while (this.Running)
				this.ProcessConnection(await this.AcceptConnection());
		}

		private async void ProcessConnection(Connection connection) {
			using (connection) {
				if (connection == null)
					return;

				connection.MessageFormat = this.Provider;

				this.connections.Add(connection);

				while (this.Running) {
					var message = await connection.Receive();

					if (message == null)
						break;

					this.Destination.Add(message);
				}

				this.connections.Remove(connection);
			}
		}

		[SuppressMessage("Microsoft.Usage", "CA2213", Justification = "Task.Dispose is called by EndShutdown which is called by Shutdown.")]
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