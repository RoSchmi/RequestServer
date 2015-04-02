using System;
using System.Runtime.Serialization;

namespace ArkeIndustries.RequestServer {
	[Serializable]
	public class MessageContextSaveFailedException : Exception {
		public bool CanRetryMessage { get; set; }
		public ushort ResponseCode { get; set; }

		public MessageContextSaveFailedException() { }
		public MessageContextSaveFailedException(string message) : base(message) { }
		public MessageContextSaveFailedException(string message, Exception inner) : base(message, inner) { }
		protected MessageContextSaveFailedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			info.AddValue(nameof(this.CanRetryMessage), this.CanRetryMessage, this.CanRetryMessage.GetType());
			info.AddValue(nameof(this.ResponseCode), this.ResponseCode, this.ResponseCode.GetType());

			base.GetObjectData(info, context);
		}
	}

	public abstract class MessageContext {
		public abstract void SaveChanges();
		public abstract void BeginMessage();
		public abstract void EndMessage();
	}
}