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

		public async Task<bool> Send(IResponse response) {
			if (response == null) throw new ArgumentNullException(nameof(response));

			if (!this.Open)
				return true;

			response.SerializeHeader();

			if (await this.Send(response.Header.GetBuffer(), 0, this.MessageFormat.HeaderLength)) {
				return await this.Send(response.Body.GetBuffer(), 0, response.BodyLength);
            }
			else {
				return false;
			}
		}

		public async Task<bool> Send(INotification notification) {
			if (notification == null) throw new ArgumentNullException(nameof(notification));

			if (!this.Open)
				return true;

			notification.SerializeHeader();

			return await this.Send(notification.Header.GetBuffer(), 0, this.MessageFormat.HeaderLength);
		}

		public async Task<IRequest> Receive() {
			if (!this.Open)
				return null;

			var request = await this.ReceiveHeader();
			var readSoFar = 0L;

			if (request != null) {
				while (true) {
					var read = await this.Receive(request.Body.GetBuffer(), readSoFar, request.BodyLength - readSoFar);

					if (read == 0)
						break;

					readSoFar += read;

					if (readSoFar == request.BodyLength)
						return request;
				}
			}

			this.Open = false;

			return null;
		}

		private async Task<IRequest> ReceiveHeader() {
			var readSoFar = 0L;
			var message = this.MessageFormat.CreateRequest();

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