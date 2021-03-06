﻿using System;
using System.Diagnostics.CodeAnalysis;
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
            if (this.disposed) throw new ObjectDisposedException(nameof(WebSocketSource));
            if (this.Running) throw new InvalidOperationException("Already started.");

            this.listener = new HttpListener();
            this.listener.Prefixes.Add($"http://*:{this.endpoint.Port}/");
            this.listener.Start();

            base.Start();
        }

        public override void Shutdown() {
            if (this.disposed) throw new ObjectDisposedException(nameof(WebSocketSource));
            if (!this.Running) throw new InvalidOperationException("Not started.");

            this.BeginShutdown();

            this.listener.Stop();
            this.listener.Close();
            this.listener = null;

            this.EndShutdown();
        }

        [SuppressMessage("Microsoft.Usage", "CA2213", Justification = "HttpListener.Dispose is not public.")]
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

            protected override async Task<long> Send(byte[] buffer, long offset, long count, bool isEndOfMessage) {
                await this.client.SendAsync(new ArraySegment<byte>(buffer, (int)offset, (int)count), WebSocketMessageType.Binary, isEndOfMessage, CancellationToken.None);

                return count;
            }

            protected override async Task<long> Receive(byte[] buffer, long offset, long count, bool isEndOfMessage) {
                try {
                    var result = await this.client.ReceiveAsync(new ArraySegment<byte>(buffer, (int)offset, (int)count), CancellationToken.None);

                    return result.MessageType == WebSocketMessageType.Binary ? result.Count : 0;
                }
                catch (WebSocketException e) when ((e.InnerException as HttpListenerException)?.NativeErrorCode == 995) {
                    return 0;
                }
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
