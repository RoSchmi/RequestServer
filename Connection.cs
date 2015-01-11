using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		private byte[] buffer;
		private int readSoFar;
		private int expectedLength;
		private bool hasHeader;

		protected bool disposed;
		protected CancellationToken cancellationToken;

		public ulong UserId { get; set; }

		public Connection(CancellationToken token) {
			this.cancellationToken = token;
			this.buffer = new byte[Message.MaxBodyLength + Message.HeaderLength];
			this.readSoFar = 0;
			this.expectedLength = 0;
			this.hasHeader = false;
			this.disposed = false;
		}

		public bool Send(Message message) {
			return this.Send(message.Data, 0, message.Data.Length);
		}

		public async Task<Message> Receive() {
			this.expectedLength = 0;
			this.hasHeader = false;

			while (true) {
				var read = 0;

				try {
					read = await this.Receive(this.buffer, this.readSoFar, this.buffer.Length - this.readSoFar);
				}
				catch (IOException) {
					break;
				}

				if (read == 0)
					break;

				this.readSoFar += read;
				this.hasHeader = this.readSoFar >= Message.HeaderLength;

				if (!this.hasHeader)
					continue;

				if (this.expectedLength == 0)
					this.expectedLength = RequestHeader.ExtractBodyLength(this.buffer) + Message.HeaderLength;

				if (this.readSoFar < this.expectedLength)
					continue;

				if (this.expectedLength > Message.MaxBodyLength + Message.HeaderLength)
					break;

				var message = new Message(this, this.buffer, this.expectedLength);

				Array.Copy(this.buffer, this.expectedLength, this.buffer, 0, this.readSoFar - this.expectedLength);

				return message;
			}

			return null;
		}

		protected abstract bool Send(byte[] buffer, int offset, int length);
		protected abstract Task<int> Receive(byte[] buffer, int offset, int length);


		protected virtual void Dispose(bool disposing) {
			this.disposed = true;
		}

		public void Dispose() {
			this.Dispose(true);
		}

	}
}
