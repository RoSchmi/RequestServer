using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ArkeIndustries.RequestServer {
	[AttributeUsage(AttributeTargets.Field)]
	public class MessageDirectionAttribute : Attribute {
		public int Index { get; set; }
		public bool IsInput { get; set; }
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class MessageServerAttribute : Attribute {
		public int ServerId { get; set; }
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

		public void Deserialize(BinaryReader request, bool deserializeInputFields = true) {
			var fields = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.IsDefined(typeof(MessageDirectionAttribute)) && p.GetCustomAttribute<MessageDirectionAttribute>().IsInput == deserializeInputFields).OrderBy(p => p.GetCustomAttribute<MessageDirectionAttribute>().Index);

			foreach (var i in fields) {
				if (i.FieldType == typeof(string)) {
					i.SetValue(this, request.ReadString());
				}
				else if (i.FieldType == typeof(byte)) {
					i.SetValue(this, request.ReadByte());
				}
				else if (i.FieldType == typeof(sbyte)) {
					i.SetValue(this, request.ReadSByte());
				}
				else if (i.FieldType == typeof(ushort)) {
					i.SetValue(this, request.ReadUInt16());
				}
				else if (i.FieldType == typeof(short)) {
					i.SetValue(this, request.ReadInt16());
				}
				else if (i.FieldType == typeof(uint)) {
					i.SetValue(this, request.ReadUInt32());
				}
				else if (i.FieldType == typeof(int)) {
					i.SetValue(this, request.ReadInt32());
				}
				else if (i.FieldType == typeof(ulong)) {
					i.SetValue(this, request.ReadUInt64());
				}
				else if (i.FieldType == typeof(long)) {
					i.SetValue(this, request.ReadInt64());
				}
			}
		}

		public void Serialize(BinaryWriter response, bool serializeOutputFields = true) {
			var fields = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.IsDefined(typeof(MessageDirectionAttribute)) && p.GetCustomAttribute<MessageDirectionAttribute>().IsInput != serializeOutputFields).OrderBy(p => p.GetCustomAttribute<MessageDirectionAttribute>().Index);

			foreach (var i in fields) {
				if (i.FieldType == typeof(string)) {
					response.Write((string)i.GetValue(this));
				}
				else if (i.FieldType == typeof(byte)) {
					response.Write((byte)i.GetValue(this));
				}
				else if (i.FieldType == typeof(sbyte)) {
					response.Write((sbyte)i.GetValue(this));
				}
				else if (i.FieldType == typeof(ushort)) {
					response.Write((ushort)i.GetValue(this));
				}
				else if (i.FieldType == typeof(short)) {
					response.Write((short)i.GetValue(this));
				}
				else if (i.FieldType == typeof(uint)) {
					response.Write((uint)i.GetValue(this));
				}
				else if (i.FieldType == typeof(int)) {
					response.Write((int)i.GetValue(this));
				}
				else if (i.FieldType == typeof(ulong)) {
					response.Write((ulong)i.GetValue(this));
				}
				else if (i.FieldType == typeof(long)) {
					response.Write((long)i.GetValue(this));
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