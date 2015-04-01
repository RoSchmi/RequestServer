namespace ArkeIndustries.RequestServer {
	internal class Notification {
		public long TargetAuthenticatedId { get; set; }
		public long NotificationType { get; set; }
		public long ObjectId { get; set; }

		public Notification(long targetAuthenticatedId, long notificationType, long objectId) {
			this.TargetAuthenticatedId = targetAuthenticatedId;
			this.NotificationType = notificationType;
			this.ObjectId = objectId;
		}
	}
}