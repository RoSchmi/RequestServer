namespace ArkeIndustries.RequestServer {
	public static class ResponseCode {
		public static long Success => 0;
		public static long NotAuthorized => 1;
		public static long WrongRequestId => 2;
		public static long WrongParameterNumber => 3;
		public static long InternalServerError => 4;
		public static long ParameterValidationFailed => 5;
		public static long AuthenticationFailed => 6;
		public static long TryAgainLater => 7;
		public static long ConcurrencyFailure => 8;
		public static long ObjectNotFound => 9;
	}

	public static class NotificationCode {
		public static long UpdateStarted => 0;
		public static long UpdateFinished => 1;
		public static long ServerShuttingDown => 2;
	}
}
