namespace ArkeIndustries.RequestServer {
	public class Notification {
		public Message Message { get; set; }
		public long TargetAuthenticatedId { get; set; }

		public Notification(long targetAuthenticatedId, ushort notificationType, long objectId) {
			this.Message = Message.CreateNotification(notificationType, objectId);
			this.TargetAuthenticatedId = targetAuthenticatedId;
		}
	}
}
