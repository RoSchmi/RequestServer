namespace ArkeIndustries.RequestServer {
    internal class Notification {
        public Connection Connection { get; set; }
        public long TargetAuthenticatedId { get; set; }
        public long Type { get; set; }
        public long ObjectId { get; set; }

        public Notification(long targetAuthenticatedId, long type, long objectId) {
            this.TargetAuthenticatedId = targetAuthenticatedId;
            this.Type = type;
            this.ObjectId = objectId;
        }

        public Notification(Connection connection, long type, long objectId) {
            this.Connection = connection;
            this.Type = type;
            this.ObjectId = objectId;
        }
    }
}
