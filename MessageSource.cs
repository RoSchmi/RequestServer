using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class MessageSource {
		private List<Connection> connections;
		private bool running;
		private Task worker;

		public BlockingCollection<IMessage> ReceivedMessages { get; set; }
		public IReadOnlyCollection<Connection> Connections => this.connections;

		protected abstract Task<Connection> AcceptConnection();

		public MessageSource() {
			this.connections = new List<Connection>();
			this.running = false;
		}

		public virtual void Start() {
			this.running = true;

			this.worker = new Task(this.AcceptConnections, TaskCreationOptions.LongRunning);
			this.worker.Start();
		}

		public virtual void Stop() {
			this.running = false;

			this.worker.Wait();
		}

		private async void AcceptConnections() {
			while (this.running)
				this.ProcessConnection(await this.AcceptConnection());
		}

		private async void ProcessConnection(Connection connection) {
			using (connection) {
				if (connection == null)
					return;

				this.connections.Add(connection);

				while (this.running) {
					var message = await connection.Receive();

					if (message == null)
						break;

					this.ReceivedMessages.Add(message);
				}

				this.connections.Remove(connection);
			}
		}
	}
}