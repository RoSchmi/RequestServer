namespace ArkeIndustries.RequestServer {
	internal class Notification {
		public long TargetAuthenticatedId { get; set; }
		public long Type { get; set; }
		public long ObjectId { get; set; }

		public Notification(long targetAuthenticatedId, long type, long objectId) {
			this.TargetAuthenticatedId = targetAuthenticatedId;
			this.Type = type;
			this.ObjectId = objectId;
		}
	}
}