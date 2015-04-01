using System;
using System.IO;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		public long AuthenticatedId { get; set; } = 0;
		public long AuthenticatedLevel { get; set; } = 0;
		public IMessageFormat MessageFormat { get; set; }

		public async Task<bool> Send(IMessage message) {
			message.SerializeHeader();

			return await this.Send(message.Header, 0, message.Header.Length);
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
		}
	}
}