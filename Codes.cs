namespace ArkeIndustries.RequestServer {
	public class ResponseCode {
		public static ushort Success { get; } = 0;
		public static ushort NotAuthorized { get; } = 1;
		public static ushort WrongMethod { get; } = 2;
		public static ushort WrongParameterNumber { get; } = 3;
		public static ushort InternalServerError { get; } = 4;
		public static ushort ParameterValidationFailed { get; } = 5;
		public static ushort AuthenticationFailed { get; } = 6;
		public static ushort TryAgainLater { get; } = 7;
		public static ushort ConcurrencyFailure { get; } = 8;
	}

	public class NotificationCode {
		public static ushort UpdateStarted { get; } = 0;
		public static ushort UpdateFinished { get; } = 1;
		public static ushort ServerShuttingDown { get; } = 2;
	}
}
