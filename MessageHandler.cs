using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ArkeIndustries.RequestServer {
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class MessageServerAttribute : Attribute {
		public int ServerId { get; set; }
	}

	[AttributeUsage(AttributeTargets.Field)]
	public class MessageParameterAttribute : Attribute {
		public int Index { get; set; }
	}

	[AttributeUsage(AttributeTargets.Field)]
	public class MessageInputAttribute : MessageParameterAttribute {

	}

	[AttributeUsage(AttributeTargets.Field)]
	public class MessageOutputAttribute : MessageParameterAttribute {

	}

	public abstract class MessageHandler<ContextType> {
		public ulong UserId { get; set; }
		public ContextType Context { get; set; }
		public List<Notification> Notifications { get; set; }

		public abstract ushort Category { get; }
		public abstract ushort Method { get; }

		public abstract ushort Perform();

		public MessageHandler() {
			this.Notifications = new List<Notification>();
			this.UserId = 0;
			this.Context = default(ContextType);
		}

		public void Deserialize<AttributeType>(BinaryReader reader) where AttributeType : MessageParameterAttribute {
            var fields = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.IsDefined(typeof(AttributeType))).OrderBy(p => p.GetCustomAttribute<AttributeType>().Index);

			foreach (var i in fields) {
				if (i.FieldType == typeof(string)) {
					i.SetValue(this, reader.ReadString());
				}
				else if (i.FieldType == typeof(byte)) {
					i.SetValue(this, reader.ReadByte());
				}
				else if (i.FieldType == typeof(sbyte)) {
					i.SetValue(this, reader.ReadSByte());
				}
				else if (i.FieldType == typeof(ushort)) {
					i.SetValue(this, reader.ReadUInt16());
				}
				else if (i.FieldType == typeof(short)) {
					i.SetValue(this, reader.ReadInt16());
				}
				else if (i.FieldType == typeof(uint)) {
					i.SetValue(this, reader.ReadUInt32());
				}
				else if (i.FieldType == typeof(int)) {
					i.SetValue(this, reader.ReadInt32());
				}
				else if (i.FieldType == typeof(ulong)) {
					i.SetValue(this, reader.ReadUInt64());
				}
				else if (i.FieldType == typeof(long)) {
					i.SetValue(this, reader.ReadInt64());
				}
			}
		}

        public void Serialize<AttributeType>(BinaryWriter writer) where AttributeType : MessageParameterAttribute {
			var fields = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.IsDefined(typeof(AttributeType))).OrderBy(p => p.GetCustomAttribute<AttributeType>().Index);

			foreach (var i in fields) {
				if (i.FieldType == typeof(string)) {
					writer.Write((string)i.GetValue(this));
				}
				else if (i.FieldType == typeof(byte)) {
					writer.Write((byte)i.GetValue(this));
				}
				else if (i.FieldType == typeof(sbyte)) {
					writer.Write((sbyte)i.GetValue(this));
				}
				else if (i.FieldType == typeof(ushort)) {
					writer.Write((ushort)i.GetValue(this));
				}
				else if (i.FieldType == typeof(short)) {
					writer.Write((short)i.GetValue(this));
				}
				else if (i.FieldType == typeof(uint)) {
					writer.Write((uint)i.GetValue(this));
				}
				else if (i.FieldType == typeof(int)) {
					writer.Write((int)i.GetValue(this));
				}
				else if (i.FieldType == typeof(ulong)) {
					writer.Write((ulong)i.GetValue(this));
				}
				else if (i.FieldType == typeof(long)) {
					writer.Write((long)i.GetValue(this));
				}
			}
		}

		public static uint GetKey(ushort category, ushort method) {
			return (uint)((category << 16) | method);
		}

		public uint GetKey() {
			return MessageHandler<ContextType>.GetKey(this.Category, this.Method);
		}

		protected void SendNotification(ulong targetUserId, ushort notificationType, ulong objectId = 0) {
			this.Notifications.Add(new Notification(targetUserId, notificationType, objectId));
		}
	}
}