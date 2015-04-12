using System;
using System.IO;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		private byte[] block;
		private bool disposed;

		public bool Open { get; private set; }
		public long AuthenticatedId { get; set; }
		public long AuthenticatedLevel { get; set; }
		public IMessageProvider MessageFormat { get; set; }

		protected Connection() {
			this.AuthenticatedId = 0;
			this.AuthenticatedLevel = 0;
			this.Open = true;
			this.MessageFormat = null;

			this.disposed = false;
			this.block = new byte[512];
		}

		public async Task<bool> Send(IResponse response) {
			if (this.disposed) throw new ObjectDisposedException(nameof(Connection));
			if (response == null) throw new ArgumentNullException(nameof(response));

			if (!this.Open)
				return true;

			response.Header.SetLength(this.MessageFormat.HeaderLength);

			response.SerializeHeader();

			if (await this.Send(response.Header, this.MessageFormat.HeaderLength, false)) {
				return await this.Send(response.Body, response.BodyLength, true);
			}
			else {
				return false;
			}
		}

		public async Task<bool> Send(INotification notification) {
			if (this.disposed) throw new ObjectDisposedException(nameof(Connection));
			if (notification == null) throw new ArgumentNullException(nameof(notification));

			if (!this.Open)
				return true;

			notification.Header.SetLength(this.MessageFormat.HeaderLength);

			notification.SerializeHeader();

			return await this.Send(notification.Header, this.MessageFormat.HeaderLength, true);
		}

		public async Task<IRequest> Receive() {
			if (this.disposed) throw new ObjectDisposedException(nameof(Connection));

			if (!this.Open)
				return null;

			var message = this.MessageFormat.CreateRequest(this);

			message.Header.SetLength(this.MessageFormat.HeaderLength);

			if (await this.Receive(message.Header, this.MessageFormat.HeaderLength, false) == false)
				return null;

			message.DeserializeHeader();

			if (await this.Receive(message.Body, message.BodyLength, true) == false)
				return null;

			return message;
		}

		private async Task<bool> Send(Stream stream, long count, bool isEndOfMessage) {
			stream.Seek(0, SeekOrigin.Begin);

			for (var i = 0; i < count && count - i >= this.block.Length; i += this.block.Length) {
				stream.Read(this.block, 0, this.block.Length);

				if (await Connection.SendReceive(this.block, this.block.Length, isEndOfMessage, this.Send) == false)
					return false;
			}

			stream.Read(this.block, 0, (int)count % this.block.Length);

			stream.Seek(0, SeekOrigin.Begin);

			return await Connection.SendReceive(this.block, count % this.block.Length, isEndOfMessage, this.Send);
		}

		private async Task<bool> Receive(Stream stream, long count, bool isEndOfMessage) {
			stream.Seek(0, SeekOrigin.Begin);

			for (var i = 0; i < count && count - i >= this.block.Length; i += this.block.Length) {
				if (await Connection.SendReceive(this.block, this.block.Length, isEndOfMessage, this.Receive) == false)
					return false;

				stream.Write(this.block, 0, this.block.Length);
			}

			if (await Connection.SendReceive(this.block, count % this.block.Length, isEndOfMessage, this.Receive) == false)
				return false;

			stream.Write(this.block, 0, (int)count % this.block.Length);

			stream.Seek(0, SeekOrigin.Begin);

			return true;
		}

		private static async Task<bool> SendReceive(byte[] buffer, long count, bool isEndOfMessage, Func<byte[], long, long, bool, Task<long>> which) {
			var soFar = 0L;
			var current = 0L;

			while (soFar != count) {
				current = await which(buffer, soFar, count - soFar, isEndOfMessage);

				if (current == 0)
					return false;

				soFar += current;
			}

			return true;
		}

		protected abstract Task<long> Send(byte[] buffer, long offset, long count, bool isEndOfMessage);
		protected abstract Task<long> Receive(byte[] buffer, long offset, long count, bool isEndOfMessage);

		protected virtual void Dispose(bool disposing) {
			this.disposed = true;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}