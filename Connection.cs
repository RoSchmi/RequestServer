using System;
using System.IO;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		public long AuthenticatedId { get; set; }

		public async Task<bool> Send(Message message) {
			message.SerializeHeader();

			return await this.Send(message.Header, 0, message.Header.Length);
		}

		public async Task<Message> Receive() {
			var message = await this.ReceiveHeader();
			var readSoFar = 0;

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

		private async Task<Message> ReceiveHeader() {
			var readSoFar = 0;
			var message = new Message();

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
		protected abstract Task<int> Receive(MemoryStream stream, long offset, long length);

		protected virtual void Dispose(bool disposing) {

		}

		public void Dispose() {
			this.Dispose(true);
		}
	}
}