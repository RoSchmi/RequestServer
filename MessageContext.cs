namespace ArkeIndustries.RequestServer {
	public abstract class MessageContext {
		public long AuthenticatedId { get; set; }
		public long AuthenticatedLevel { get; set; }

		public abstract void SaveChanges();
		public abstract void BeginMessage();
		public abstract void EndMessage();
	}
}