using System;
using System.Threading.Tasks;

namespace ArkeIndustries.RequestServer {
	public abstract class Connection : IDisposable {
		public bool Open { get; private set; }
		public long AuthenticatedId { get; set; }
		public long AuthenticatedLevel { get; set; }
		public IMessageProvider MessageFormat { get; set; }

		protected Connection() {
			this.AuthenticatedId = 0;
			this.AuthenticatedLevel = 0;
			this.Open = true;
			this.MessageFormat = null;
		}

		public async Task<bool> Send(IMessage message) {
			if (message == null) throw new ArgumentNullException(nameof(message));

			if (!this.Open)
				return true;

			message.SerializeHeader();

			if (await this.Send(message.Header.GetBuffer(), 0, this.MessageFormat.HeaderLength)) {
				return await this.Send(message.Body.GetBuffer(), 0, message.BodyLength);
            }
			else {
				return false;
			}
		}

		public async Task<IMessage> Receive() {
			if (!this.Open)
				return null;

			var message = await this.ReceiveHeader();
			var readSoFar = 0L;

			if (message != null) {
				while (true) {
					var read = await this.Receive(message.Body.GetBuffer(), readSoFar, message.BodyLength - readSoFar);

					if (read == 0)
						break;

					readSoFar += read;

					if (readSoFar == message.BodyLength)
						return message;
				}
			}

			this.Open = false;

			return null;
		}

		private async Task<IMessage> ReceiveHeader() {
			var readSoFar = 0L;
			var message = this.MessageFormat.CreateMessage();

			while (true) {
				var read = await this.Receive(message.Header.GetBuffer(), readSoFar, message.Header.Length - readSoFar);

				if (read == 0)
					return null;

				readSoFar += read;

				if (readSoFar != message.Header.Length)
					continue;

				message.DeserializeHeader();

				return message;
			}
		}

		protected abstract Task<bool> Send(byte[] buffer, long offset, long count);
		protected abstract Task<long> Receive(byte[] buffer, long offset, long count);

		protected virtual void Dispose(bool disposing) {

		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}