using System.IO;

namespace ArkeIndustries.RequestServer {
	public class Message {
		private static int HeaderLength { get; } = 24;

		public ushort Version { get; set; }
		public ushort BodyLength { get; set; }
		public uint MessageId { get; set; }

		public ushort RequestCategory { get; set; }
		public ushort RequestMethod { get; set; }

		public ushort ResponseCode { get; set; }

		public ushort NotificationType { get; set; }
		public long NotificationObjectId { get; set; }

		public MemoryStream Header { get; private set; }
		public MemoryStream Body { get; private set; }

		public Connection Connection { get; set; }
		public int SendAttempts { get; set; }
		public int ProcessAttempts { get; set; }

		public Message() {
			this.Header = new MemoryStream(new byte[Message.HeaderLength], 0, Message.HeaderLength, true, true);
			this.Body = new MemoryStream();
		}

		public Message(Connection connection) : this() {
			this.Connection = connection;
		}

		public Message(Connection connection, MemoryStream header) : this() {
			this.Connection = connection;

			header.CopyTo(this.Header);
		}

		public void DeserializeHeader() {
			using (var reader = new BinaryReader(this.Header)) {
				this.Version = reader.ReadUInt16();
				this.BodyLength = reader.ReadUInt16();
				this.MessageId = reader.ReadUInt32();
				this.RequestCategory = reader.ReadUInt16();
				this.RequestMethod = reader.ReadUInt16();

				this.Body.Capacity = this.BodyLength;
			}
		}

		public void SerializeHeader() {
			using (var writer = new BinaryWriter(this.Header)) {
				writer.Write(this.Version);
				writer.Write(this.BodyLength);
				writer.Write(this.MessageId);

				if (this.MessageId != 0) {
					writer.Write(this.RequestCategory);
					writer.Write(this.RequestMethod);
				}
				else {
					writer.Write(this.NotificationType);
					writer.Write(this.NotificationObjectId);
				}
			}
		}

		public static Message CreateNotification(ushort notificationType, long objectId) {
			var message = new Message();

			message.Version = 0;
			message.BodyLength = 0;
			message.MessageId = 0;
			message.NotificationType = notificationType;
			message.NotificationObjectId = objectId;

			return message;
		}
	}
}