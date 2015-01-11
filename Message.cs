using System;
using System.IO;

namespace ArkeIndustries.RequestServer {
	public class Message {
		public static int HeaderLength { get; } = 24;
		public static int MaxBodyLength { get; } = 0xFFF;

		public byte[] Data { get; }
		public Connection Connection { get; }
		public int SendAttempts { get; set; }

		public Message(Connection connection, byte[] data, int bodyLength) {
			this.SendAttempts = 0;
			this.Connection = connection;
			this.Data = new byte[bodyLength];

			Array.Copy(data, this.Data, bodyLength);
		}
	}

	public class RequestHeader {
		public ushort Version { get; private set; }
		public ushort BodyLength { get; private set; }
		public uint Id { get; private set; }
		public ushort Category { get; private set; }
		public ushort Method { get; private set; }

		public void Deserialize(BinaryReader reader) {
			this.Version = reader.ReadUInt16();
			this.BodyLength = reader.ReadUInt16();
			this.Id = reader.ReadUInt32();
			this.Category = reader.ReadUInt16();
			this.Method = reader.ReadUInt16();
		}

		public void Serialize(BinaryWriter writer) {
			writer.Write(this.Version);
			writer.Write(this.BodyLength);
			writer.Write(this.Id);
			writer.Write(this.Category);
			writer.Write(this.Method);
		}

		public static int ExtractBodyLength(byte[] buffer) {
			return (buffer[3] << 8) | buffer[2];
		}
	}

	public class ResponseHeader {
		public ushort BodyLength { get; set; }
		public uint Id { get; set; }
		public ushort ResponseCode { get; set; }

		public void Deserialize(BinaryReader reader) {
			this.BodyLength = reader.ReadUInt16();
			this.Id = reader.ReadUInt32();
			this.ResponseCode = reader.ReadUInt16();
		}

		public void Serialize(BinaryWriter writer) {
			writer.Write(this.BodyLength);
			writer.Write(this.Id);
			writer.Write(this.ResponseCode);
		}
	}

	public class Notification {
		public ulong TargetUserId { get; set; }
		public ushort NotificationType { get; set; }
		public ulong ObjectId { get; set; }

		public Notification(ulong targetUserId, ushort notificationType, ulong objectId) {
			this.TargetUserId = targetUserId;
			this.NotificationType = notificationType;
			this.ObjectId = objectId;
		}

		public void Serialize(BinaryWriter writer) {
			writer.Write((ushort)0);
			writer.Write((uint)0);
			writer.Write((ushort)0);
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
