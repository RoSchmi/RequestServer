using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class TcpSource : MessageSource {
		private TcpListener listener;
		private IPEndPoint endpoint;
		private bool disposed;

		public TcpSource(IPEndPoint endpoint) {
			this.endpoint = endpoint;
			this.disposed = false;
		}

		protected override async Task<Connection> AcceptConnection() {
			try {
                return new TcpConnection(await this.listener.AcceptTcpClientAsync());
			}
			catch (ObjectDisposedException) {
				return null;
			}
		}

		public override void Start() {
			if (this.disposed) throw new ObjectDisposedException(nameof(TcpSource));
			if (this.Running) throw new InvalidOperationException("Already started.");

			this.listener = new TcpListener(endpoint);
			this.listener.Start();

			base.Start();
		}

		public override void Shutdown() {
			if (this.disposed) throw new ObjectDisposedException(nameof(TcpSource));
			if (!this.Running) throw new InvalidOperationException("Not started.");

			this.BeginShutdown();

			this.listener.Stop();

			this.EndShutdown();
		}

		protected override void Dispose(bool disposing) {
			if (this.disposed)
				return;

			this.disposed = true;

			base.Dispose(disposing);
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

			protected override async Task<long> Send(byte[] buffer, long offset, long count, bool isEndOfMessage) {
				await this.networkStream.WriteAsync(buffer, (int)offset, (int)count);

				return count;
			}

			protected override async Task<long> Receive(byte[] buffer, long offset, long count, bool isEndOfMessage) {
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