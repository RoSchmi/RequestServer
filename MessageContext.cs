using System;
using System.Runtime.Serialization;

namespace ArkeIndustries.RequestServer {
	[Serializable]
	public class MessageContextSaveFailedException : Exception {
		public bool CanRetry { get; set; }
		public ushort ResponseCode { get; set; }

		public MessageContextSaveFailedException() { }
		public MessageContextSaveFailedException(string message) : base(message) { }
		public MessageContextSaveFailedException(string message, Exception inner) : base(message, inner) { }
		protected MessageContextSaveFailedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

	public abstract class MessageContext {
		public abstract void SaveChanges();
		public abstract void BeginMessage();
		public abstract void EndMessage();
	}
}
