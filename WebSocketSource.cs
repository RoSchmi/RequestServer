using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public class WebSocketSource : MessageSource {
		private HttpListener listener;
		private IPEndPoint endpoint;
		private bool disposed;

        public WebSocketSource(IPEndPoint endpoint) {
			this.endpoint = endpoint;
			this.disposed = false;
		}

		protected override async Task<Connection> AcceptConnection() {
			try {
				return new WebSocketConnection((await this.listener.GetContext().AcceptWebSocketAsync(null)).WebSocket);
			}
			catch (HttpListenerException e) when (e.ErrorCode == 995) {
				return null;
			}
		}

		public override void Start() {
			if (this.Running) throw new InvalidOperationException("Already started.");

			this.listener = new HttpListener();
			this.listener.Prefixes.Add($"http://*:{this.endpoint.Port}/");
			this.listener.Start();

			base.Start();
		}

		public override void Shutdown() {
			if (!this.Running) throw new InvalidOperationException("Not started.");

			this.listener.Stop();
			this.listener.Close();
			this.listener = null;

			base.Shutdown();
		}

		[SuppressMessage("Microsoft.Usage", "CA2213", Justification = "Dispose is not a public method.")]
		protected override void Dispose(bool disposing) {
			if (this.disposed)
				return;

			if (disposing && this.Running)
				this.Shutdown();

			this.disposed = true;

			base.Dispose(disposing);
		}

		private class WebSocketConnection : Connection {
			private WebSocket client;
			private bool disposed;

			public WebSocketConnection(WebSocket client) {
				this.client = client;
				this.disposed = false;
			}

			protected override async Task<bool> Send(MemoryStream stream, long offset, long length) {
				await this.client.SendAsync(new ArraySegment<byte>(stream.GetBuffer(), (int)offset, (int)length), WebSocketMessageType.Binary, true, CancellationToken.None);

				return true;
			}

			protected override async Task<long> Receive(MemoryStream stream, long offset, long length) {
				var result = await this.client.ReceiveAsync(new ArraySegment<byte>(stream.GetBuffer(), (int)offset, (int)length), CancellationToken.None);

				return result.MessageType == WebSocketMessageType.Binary ? result.Count : 0;
			}

			protected override void Dispose(bool disposing) {
				if (this.disposed)
					return;

				if (disposing) {
					this.client.Dispose();
					this.client = null;
				}

				this.disposed = true;

				base.Dispose(disposing);
			}
		}
	}
}