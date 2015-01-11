using System;
using System.IO;

namespace ArkeIndustries.RequestServer {
	public class Message {
		public byte[] Data { get; }
		public Connection Connection { get; }
		public int SendAttempts { get; set; }

		public Message(Connection connection, byte[] data, int length) {
			this.SendAttempts = 0;
			this.Connection = connection;
			this.Data = new byte[length];

			Array.Copy(data, this.Data, length);
		}
	}

	public class MessageHeader {
		public static int Length { get; } = 24;
		public static int MaxBodyLength { get; } = 0xFFF;

		public ushort BodyLength { get; set; }
		public uint Id { get; set; }
		public ushort Version { get; set; }

		public virtual void Deserialize(BinaryReader reader) {
			this.BodyLength = reader.ReadUInt16();
			this.Id = reader.ReadUInt32();
			this.Version = reader.ReadUInt16();
		}

		public virtual void Serialize(BinaryWriter writer) {
			writer.Write(this.BodyLength);
			writer.Write(this.Id);
			writer.Write(this.Version);
		}

		public static int ExtractBodyLength(byte[] buffer) {
			return (buffer[1] << 8) | buffer[0];
		}
	}

	public class RequestHeader : MessageHeader {
		public ushort Category { get; set; }
		public ushort Method { get; set; }

		public override void Deserialize(BinaryReader reader) {
			base.Deserialize(reader);

			this.Category = reader.ReadUInt16();
			this.Method = reader.ReadUInt16();
		}

		public override void Serialize(BinaryWriter writer) {
			base.Serialize(writer);

			writer.Write(this.Category);
			writer.Write(this.Method);
		}
	}

	public class ResponseHeader : MessageHeader {
		public ushort ResponseCode { get; set; }

		public override void Deserialize(BinaryReader reader) {
			base.Deserialize(reader);

			this.ResponseCode = reader.ReadUInt16();
		}

		public override void Serialize(BinaryWriter writer) {
			base.Serialize(writer);

			writer.Write(this.ResponseCode);
		}
	}

	public class Notification : MessageHeader {
		public ulong TargetUserId { get; set; }

		public ushort NotificationType { get; set; }
		public ulong ObjectId { get; set; }

		public Notification() : this(0, 0, 0) {

		}

		public Notification(ulong targetUserId, ushort notificationType, ulong objectId) {
			this.TargetUserId = targetUserId;
			this.NotificationType = notificationType;
			this.ObjectId = objectId;
		}

		public override void Deserialize(BinaryReader reader) {
			base.Deserialize(reader);

			this.NotificationType = reader.ReadUInt16();
			this.ObjectId = reader.ReadUInt64();
		}

		public override void Serialize(BinaryWriter writer) {
			base.Serialize(writer);

			writer.Write(this.NotificationType);
			writer.Write(this.ObjectId);
		}
	}

	public class ResponseCode {
		public static ushort Success { get; } = 0;
		public static ushort NotAuthorized { get; } = 1;
		public static ushort InvalidMethod { get; } = 2;
		public static ushort InvalidParameters { get; } = 3;
		public static ushort InternalServerError { get; } = 4;
	}
}
