using System;

namespace ArkeIndustries.RequestServer {
    public class MessageContextSaveFailedException : Exception {
        public bool CanRetryMessage { get; set; }
        public long ResponseCode { get; set; }

        public MessageContextSaveFailedException() { }
        public MessageContextSaveFailedException(string message) : base(message) { }
        public MessageContextSaveFailedException(string message, Exception inner) : base(message, inner) { }
    }

    public class MultiplyDefinedMessageHandlerException : Exception {
        public long HandlerId { get; set; }

        public MultiplyDefinedMessageHandlerException() { }
        public MultiplyDefinedMessageHandlerException(string message) : base(message) { }
        public MultiplyDefinedMessageHandlerException(string message, Exception inner) : base(message, inner) { }
    }
}