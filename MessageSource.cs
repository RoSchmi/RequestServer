using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class MessageSource {
		private Task worker;

		public CancellationToken CancellationToken { get; set; }
		public BlockingCollection<Message> MessageDestination { get; set; }
		public List<Connection> Connections{ get; }

		public MessageSource() {
			this.worker = new Task(this.ProcessPendingClients, TaskCreationOptions.LongRunning);

			this.MessageDestination = null;
			this.Connections = new List<Connection>();
		}

		public virtual void Start() {
			this.worker.Start();
		}

		public virtual void Stop() {
			this.worker.Wait();
		}

		private void ProcessPendingClients() {
			while (!this.CancellationToken.IsCancellationRequested)
				this.ProcessConnection(this.AcceptConnection());
		}

		private async void ProcessConnection(Connection connection) {
			if (connection == null)
				return;

			lock (this.Connections)
				this.Connections.Add(connection);

			while (!this.CancellationToken.IsCancellationRequested) {
				var message = await connection.Receive();

				if (message == null)
					break;

				this.MessageDestination.Add(message);
			}

			lock (this.Connections)
				this.Connections.Remove(connection);

			connection.Dispose();
		}

		public abstract Connection AcceptConnection();
	}
}