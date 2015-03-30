using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer.Sources {
	public class WebSocketSource : MessageSource {
		private HttpListener listener;

		public WebSocketSource(IPEndPoint endpoint) {
			this.listener = new HttpListener();
			this.listener.Prefixes.Add(string.Format("http://*:{0}/", endpoint.Port));
		}

		public override Connection AcceptConnection() {
			try {
				var ws = this.listener.GetContext().AcceptWebSocketAsync(null);

				ws.Wait();

				return new WebSocketConnection(this.CancellationToken, ws.Result.WebSocket);
			}
			catch (HttpListenerException e) when (e.ErrorCode == 995) {
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

	public class WebSocketConnection : Connection {
		private WebSocket client;

		public WebSocketConnection(CancellationToken token, WebSocket client) : base(token) {
			this.client = client;
		}

		protected override async Task<bool> Send(MemoryStream stream, long offset, long length) {
			if (this.Disposed || this.client.State != WebSocketState.Open)
				return false;

			try {
				await this.client.SendAsync(new ArraySegment<byte>(stream.GetBuffer(), (int)offset, (int)length), WebSocketMessageType.Binary, true, this.CancellationToken);

				return true;
			}
			catch (Exception) {

			}

			return false;
		}

		protected override async Task<int> Receive(MemoryStream stream, long offset, long length) {
			if (this.Disposed || this.client.State != WebSocketState.Open)
				return 0;

			try {
				var result = await this.client.ReceiveAsync(new ArraySegment<byte>(stream.GetBuffer(), (int)offset, (int)length), CancellationToken.None);

				return result.MessageType == WebSocketMessageType.Binary ? result.Count : 0;
			}
			catch (Exception) {

			}

			return 0;
		}

		protected override void Dispose(bool disposing) {
			if (!this.Disposed) {
				if (disposing) {
					this.client.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server shutting down.", CancellationToken.None).Wait(1000);
					this.client.Dispose();
				}
			}

			base.Dispose(disposing);
		}
	}
}