using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class TcpSource : MessageSource {
		private TcpListener listener;
		private IPEndPoint endpoint;

		public TcpSource(IPEndPoint endpoint) {
			this.endpoint = endpoint;
		}

		protected override async Task<Connection> AcceptConnection() {
			try {
				return new TcpConnection(await this.listener.AcceptTcpClientAsync());
			}
			catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted) {
				return null;
			}
		}

		public override void Start() {
			this.listener = new TcpListener(endpoint);
			this.listener.Start();

			base.Start();
		}

		public override void Shutdown() {
			this.listener.Stop();

			base.Shutdown();
		}

		private class TcpConnection : Connection {
			private TcpClient client;
			private NetworkStream networkStream;
			private bool disposed;

			public TcpConnection(TcpClient client) {
				this.client = client;
				this.networkStream = client.GetStream();
				this.disposed = false;
			}

			protected override async Task<bool> Send(byte[] buffer, long offset, long count) {
				await this.networkStream.WriteAsync(buffer, (int)offset, (int)count);

				return true;
			}

			protected override async Task<long> Receive(byte[] buffer, long offset, long count) {
				return await this.networkStream.ReadAsync(buffer, (int)offset, (int)count);
			}

			protected override void Dispose(bool disposing) {
				if (this.disposed)
					return;

				if (disposing) {
					this.client.Dispose();
					this.client = null;

					this.networkStream.Dispose();
					this.networkStream = null;
				}

				this.disposed = true;

				base.Dispose(disposing);
			}
		}
	}
}