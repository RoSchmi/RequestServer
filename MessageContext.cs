namespace ArkeIndustries.RequestServer {
	public abstract class MessageContext {
		public abstract void SaveChanges();
		public abstract void BeginMessage();
		public abstract void EndMessage();
	}
}