using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer.Sources {
	public class TcpSource : MessageSource {
		private TcpListener listener;

		public TcpSource(IPEndPoint endpoint) {
			this.listener = new TcpListener(endpoint);
		}

		public override Connection AcceptConnection() {
			try {
				return new TcpConnection(this.CancellationToken, this.listener.AcceptTcpClient());
			}
			catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted) {
				return null;
			}
		}

		public override void Start() {
			this.listener.Start();

			base.Start();
		}

		public override void Stop() {
			this.listener.Stop();

			base.Stop();
		}
	}

	public class TcpConnection : Connection {
		private TcpClient client;
		private NetworkStream stream;

		public TcpConnection(CancellationToken token, TcpClient client) : base(token) {
			this.client = client;
			this.stream = client.GetStream();
		}

		protected override async Task<bool> Send(MemoryStream stream, long offset, long length) {
			if (this.Disposed)
				return true;

			if (!this.stream.CanWrite)
				return false;

			stream.Seek(offset, SeekOrigin.Begin);

			try {
				await this.stream.WriteAsync(stream.GetBuffer(), (int)offset, (int)length, this.CancellationToken);

				return true;
			}
			catch (IOException) {

			}
			catch (ObjectDisposedException) {

			}

			return false;
		}

		protected override async Task<int> Receive(MemoryStream stream, long offset, long length) {
			try {
				return await this.stream.ReadAsync(stream.GetBuffer(), (int)offset, (int)length, this.CancellationToken);
			}
			catch (IOException) {

			}

			return 0;
		}

		protected override void Dispose(bool disposing) {
			if (!this.Disposed) {
				if (disposing) {
					this.stream.Dispose();
					this.client.Dispose();
				}
			}

			base.Dispose(disposing);
		}
	}
}