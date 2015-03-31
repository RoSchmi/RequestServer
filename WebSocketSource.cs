using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer.Sources {
	public class WebSocketSource : MessageSource {
		private HttpListener listener;
		private IPEndPoint endpoint;

        public WebSocketSource(IPEndPoint endpoint) {
			this.endpoint = endpoint;
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
			this.listener = new HttpListener();
			this.listener.Prefixes.Add(string.Format("http://*:{0}/", endpoint.Port));
			this.listener.Start();

			base.Start();
		}

		public override void Stop() {
			this.listener.Stop();

			base.Stop();
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

			protected override async Task<int> Receive(MemoryStream stream, long offset, long length) {
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