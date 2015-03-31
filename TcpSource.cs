using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer.Sources {
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

		public override void Stop() {
			this.listener.Stop();

			base.Stop();
		}

		private class TcpConnection : Connection {
			private TcpClient client;
			private NetworkStream stream;
			private bool disposed;

			public TcpConnection(TcpClient client) {
				this.client = client;
				this.stream = client.GetStream();
				this.disposed = false;
			}

			protected override async Task<bool> Send(MemoryStream stream, long offset, long length) {
				await this.stream.WriteAsync(stream.GetBuffer(), (int)offset, (int)length);

				return true;
			}

			protected override async Task<int> Receive(MemoryStream stream, long offset, long length) {
				return await this.stream.ReadAsync(stream.GetBuffer(), (int)offset, (int)length);
			}

			protected override void Dispose(bool disposing) {
				if (this.disposed)
					return;

				if (disposing) {
					this.client.Dispose();
					this.client = null;

					this.stream.Dispose();
					this.stream = null;
				}

				this.disposed = true;

				base.Dispose(disposing);
			}
		}
	}
}