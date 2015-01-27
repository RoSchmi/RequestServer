using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace ArkeIndustries.RequestServer {
	[Serializable]
	public class ListQueryValidationFailedException : Exception {
		public ListQueryValidationFailedException() { }
		public ListQueryValidationFailedException(string message) : base(message) { }
		public ListQueryValidationFailedException(string message, Exception inner) : base(message, inner) { }
		protected ListQueryValidationFailedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}

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
		public static DateTime DateTimeEpoch = new DateTime(2015, 1, 1, 0, 0, 0);

		public long AuthenticatedId { get; set; }
		public ContextType Context { get; set; }
		public List<Notification> Notifications { get; set; }

		public abstract ushort Category { get; }
		public abstract ushort Method { get; }

		public abstract ushort Perform();

		public MessageHandler() {
			this.Notifications = new List<Notification>();
			this.AuthenticatedId = 0;
			this.Context = default(ContextType);
		}

		private static List<FieldInfo> GetFields<AttributeType>(object o) where AttributeType : MessageParameterAttribute {
			return o.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(AttributeType))).OrderBy(p => p.GetCustomAttribute<AttributeType>().Index).ToList();
		}

		public void Deserialize<AttributeType>(BinaryReader reader) where AttributeType : MessageParameterAttribute {
			MessageHandler<ContextType>.GetFields<AttributeType>(this).ForEach(f => MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, this));
		}

        public void Serialize<AttributeType>(BinaryWriter writer) where AttributeType : MessageParameterAttribute {
			MessageHandler<ContextType>.GetFields<AttributeType>(this).ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, this));
		}

		private static void Deserialize<AttributeType>(BinaryReader reader, FieldInfo field, object o) where AttributeType : MessageParameterAttribute {
			if (field.FieldType == typeof(string)) {
				field.SetValue(o, reader.ReadString());
			}
			else if (field.FieldType == typeof(byte)) {
				field.SetValue(o, reader.ReadByte());
			}
			else if (field.FieldType == typeof(sbyte)) {
				field.SetValue(o, reader.ReadSByte());
			}
			else if (field.FieldType == typeof(ushort)) {
				field.SetValue(o, reader.ReadUInt16());
			}
			else if (field.FieldType == typeof(short)) {
				field.SetValue(o, reader.ReadInt16());
			}
			else if (field.FieldType == typeof(uint)) {
				field.SetValue(o, reader.ReadUInt32());
			}
			else if (field.FieldType == typeof(int)) {
				field.SetValue(o, reader.ReadInt32());
			}
			else if (field.FieldType == typeof(ulong)) {
				field.SetValue(o, reader.ReadUInt64());
			}
			else if (field.FieldType == typeof(long)) {
				field.SetValue(o, reader.ReadInt64());
			}
			else if (field.FieldType == typeof(DateTime)) {
				field.SetValue(o, MessageHandler<ContextType>.DateTimeEpoch.AddMilliseconds(reader.ReadUInt64() * TimeSpan.TicksPerMillisecond));
			}
			else {
				if (field.FieldType.GetInterfaces().Any(i => i == typeof(IList))) {
					var collection = (IList)field.GetValue(o);
					var generic = collection.GetType().GenericTypeArguments[0];
					var constructor = generic.GetConstructor(Type.EmptyTypes);

					collection.Clear();

					var fields = MessageHandler<ContextType>.GetFields<AttributeType>(constructor.Invoke(null));
					var count = reader.ReadUInt16();

					for (var i = 0; i < count; i++) {
						foreach (var f in fields) {
							var newObject = constructor.Invoke(null);

							MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, newObject);

							collection.Add(newObject);
						}
					}
				}
				else {
					var child = field.GetValue(o);

					MessageHandler<ContextType>.GetFields<AttributeType>(child).ForEach(f => MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, child));
				}
			}
		}

		private static void Serialize<AttributeType>(BinaryWriter writer, FieldInfo field, object o) where AttributeType : MessageParameterAttribute {
			var child = field.GetValue(o);

			if (field.FieldType == typeof(string)) {
				writer.Write((string)child);
			}
			else if (field.FieldType == typeof(byte)) {
				writer.Write((byte)child);
			}
			else if (field.FieldType == typeof(sbyte)) {
				writer.Write((sbyte)child);
			}
			else if (field.FieldType == typeof(ushort)) {
				writer.Write((ushort)child);
			}
			else if (field.FieldType == typeof(short)) {
				writer.Write((short)child);
			}
			else if (field.FieldType == typeof(uint)) {
				writer.Write((uint)child);
			}
			else if (field.FieldType == typeof(int)) {
				writer.Write((int)child);
			}
			else if (field.FieldType == typeof(ulong)) {
				writer.Write((ulong)child);
			}
			else if (field.FieldType == typeof(long)) {
				writer.Write((long)child);
			}
			else if (field.FieldType == typeof(DateTime)) {
				writer.Write((ulong)((DateTime)child - MessageHandler<ContextType>.DateTimeEpoch).TotalMilliseconds);
			}
			else {
				if (field.FieldType.GetInterfaces().Any(i => i == typeof(IList))) {
					var collection = (IList)child;

					writer.Write((ushort)collection.Count);

					if (collection.Count == 0)
						return;

					var fields = MessageHandler<ContextType>.GetFields<AttributeType>(collection[0]);

					for (var i = 0; i < collection.Count; i++)
					    fields.ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, collection[i]));

					collection.Clear();
				}
				else {
					MessageHandler<ContextType>.GetFields<AttributeType>(child).ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, child));
				}
			}
		}

		public virtual bool IsValid() {
			return !this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ValidationAttribute))).Any(f => f.GetCustomAttributes<ValidationAttribute>().Any(a => !a.IsValid(f.GetValue(this))));
		}

		public static uint GetKey(ushort category, ushort method) {
			return (uint)((category << 16) | method);
		}

		public uint GetKey() {
			return MessageHandler<ContextType>.GetKey(this.Category, this.Method);
		}

		protected void SendNotification(long targetAuthenticatedId, ushort notificationType, long objectId = 0) {
			this.Notifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));
		}
	}

	public abstract class ListQueryMessageHandler<ContextType, QueryType> : MessageHandler<ContextType> {
		[MessageInput(Index = 0)]
		[DataAnnotations.AtLeast(0)]
		protected uint skip;

		[MessageInput(Index = 1)]
		[DataAnnotations.AtLeast(0)]
		protected uint take;

		[MessageInput(Index = 2)]
		[DataAnnotations.ApiString]
		protected string orderByField;

		[MessageInput(Index = 3)]
		protected bool orderByAscending;

		[MessageOutput(Index = 0)]
		protected List<QueryType> entries;

		protected void OrderTakeAndSet(IQueryable<QueryType> query) {
			var prop = typeof(QueryType).GetProperty(this.orderByField);

			if (prop == null)
				throw new ListQueryValidationFailedException();

			query = this.orderByAscending ? query.OrderBy(e => prop.GetValue(e)) : query.OrderByDescending(e => prop.GetValue(e));
				
			this.entries = query.Skip((int)this.skip).Take((int)this.take).ToList();
		}
	}
}