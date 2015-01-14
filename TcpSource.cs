﻿using System;
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
			catch (SocketException e) if (e.SocketErrorCode == SocketError.Interrupted) {
				return null;
			}
		}

		public override void Start() {
			this.listener.Start();

			base.Start();
		}

		public override void Stop() {
			base.Stop();

			this.listener.Stop();
		}
	}

	public class TcpConnection : Connection {
		private TcpClient client;
		private NetworkStream stream;

		public TcpConnection(CancellationToken token, TcpClient client) : base(token) {
			this.client = client;
			this.stream = client.GetStream();
		}

		protected override bool Send(byte[] buffer, int offset, int length) {
			if (this.disposed)
				return true;

			if (!this.stream.CanWrite)
				return false;

			try {
				this.stream.Write(buffer, offset, length);
			}
			catch (IOException) {

			}
			catch (ObjectDisposedException) {

			}

			return true;
		}

		protected override async Task<int> Receive(byte[] buffer, int offset, int length) {
			try {
				return await this.stream.ReadAsync(buffer, offset, length, this.cancellationToken);
			}
			catch (IOException) {

			}

			return 0;
		}

		protected override void Dispose(bool disposing) {
			if (!this.disposed) {
				if (disposing) {
					this.stream.Dispose();
					this.client.Dispose();
				}
			}

			base.Dispose(disposing);
		}
	}
}