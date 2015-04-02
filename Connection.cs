using System;
using System.IO;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		public long AuthenticatedId { get; set; }
		public long AuthenticatedLevel { get; set; }
		public bool Open { get; set; } = true;
		public IMessageFormat MessageFormat { get; set; }

		protected Connection() {
			this.AuthenticatedId = 0;
			this.AuthenticatedLevel = 0;
			this.Open = true;
			this.MessageFormat = null;
		}

		public async Task<bool> Send(IMessage message) {
			if (!this.Open)
				return true;

			message.SerializeHeader();

			if (await this.Send(message.Header, 0, this.MessageFormat.HeaderLength)) {
				return await this.Send(message.Body, 0, message.BodyLength);
            }
			else {
				return false;
			}
		}

		public async Task<IMessage> Receive() {
			var message = await this.ReceiveHeader();
			var readSoFar = 0L;

			if (message == null)
				return null;

			while (true) {
				var read = await this.Receive(message.Body, readSoFar, message.BodyLength - readSoFar);

				if (read == 0)
					return null;

				readSoFar += read;

				if (readSoFar == message.BodyLength)
					return message;
			}
		}

		private async Task<IMessage> ReceiveHeader() {
			var readSoFar = 0L;
			var message = this.MessageFormat.CreateMessage();

			while (true) {
				var read = await this.Receive(message.Header, readSoFar, message.Header.Length - readSoFar);

				if (read == 0)
					return null;

				readSoFar += read;

				if (readSoFar != message.Header.Length)
					continue;

				message.DeserializeHeader();

				return message;
			}
		}

		protected abstract Task<bool> Send(MemoryStream stream, long offset, long length);
		protected abstract Task<long> Receive(MemoryStream stream, long offset, long length);

		protected virtual void Dispose(bool disposing) {

		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}