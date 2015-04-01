namespace ArkeIndustries.RequestServer {
	public static class ResponseCode {
		public static ushort Success => 0;
		public static ushort NotAuthorized => 1;
		public static ushort WrongMethod => 2;
		public static ushort WrongParameterNumber => 3;
		public static ushort InternalServerError => 4;
		public static ushort ParameterValidationFailed => 5;
		public static ushort AuthenticationFailed => 6;
		public static ushort TryAgainLater => 7;
		public static ushort ConcurrencyFailure => 8;
	}

	public static class NotificationCode {
		public static ushort UpdateStarted => 0;
		public static ushort UpdateFinished => 1;
		public static ushort ServerShuttingDown => 2;
	}
}
