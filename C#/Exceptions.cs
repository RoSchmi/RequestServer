using System;
using System.Runtime.Serialization;

namespace ArkeIndustries.RequestServer {
	[Serializable]
	public class MessageContextSaveFailedException : Exception {
		public bool CanRetryMessage { get; set; }
		public long ResponseCode { get; set; }

		public MessageContextSaveFailedException() { }
		public MessageContextSaveFailedException(string message) : base(message) { }
		public MessageContextSaveFailedException(string message, Exception inner) : base(message, inner) { }
		protected MessageContextSaveFailedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			if (info == null) throw new ArgumentNullException(nameof(info));

			info.AddValue(nameof(this.CanRetryMessage), this.CanRetryMessage, this.CanRetryMessage.GetType());
			info.AddValue(nameof(this.ResponseCode), this.ResponseCode, this.ResponseCode.GetType());

			base.GetObjectData(info, context);
		}
	}

	[Serializable]
	public class MultiplyDefinedMessageHandlerException : Exception {
		public long HandlerId { get; set; }

		public MultiplyDefinedMessageHandlerException() { }
		public MultiplyDefinedMessageHandlerException(string message) : base(message) { }
		public MultiplyDefinedMessageHandlerException(string message, Exception inner) : base(message, inner) { }
		protected MultiplyDefinedMessageHandlerException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			if (info == null) throw new ArgumentNullException(nameof(info));

			info.AddValue(nameof(this.HandlerId), this.HandlerId, this.HandlerId.GetType());

			base.GetObjectData(info, context);
		}
	}
}