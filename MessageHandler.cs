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
	public class MessageDefinitionAttribute : Attribute {
		public int ServerId { get; set; }
		public bool AuthenticationRequired { get; set; }
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class MessageParameterAttribute : Attribute {
		public int Index { get; set; }
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class MessageInputAttribute : MessageParameterAttribute {

	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class MessageOutputAttribute : MessageParameterAttribute {

	}

	public abstract class MessageHandler<ContextType> {
		public static DateTime DateTimeEpoch = new DateTime(2015, 1, 1, 0, 0, 0);

		public bool AuthenticationRequired { get; set; }

		public long AuthenticatedId { get; set; }
		public ContextType Context { get; set; }
		public List<Notification> Notifications { get; set; }

		public abstract ushort Category { get; }
		public abstract ushort Method { get; }

		public virtual ushort Perform() {
			return ResponseCode.Success;
		}

		public MessageHandler() {
			this.Notifications = new List<Notification>();
			this.AuthenticatedId = 0;
			this.Context = default(ContextType);
		}

		protected void BindResponseObject(object o) {
			var objectProperties = o.GetType().GetProperties();

			foreach (var p in MessageHandler<ContextType>.GetProperties<MessageOutputAttribute>(this)) {
				var objectProperty = objectProperties.SingleOrDefault(op => op.Name == p.Name);

				if (objectProperty != null)
					p.SetValue(this, Convert.ChangeType(objectProperty.GetValue(o), objectProperty.PropertyType));
			}
		}

		private static List<PropertyInfo> GetProperties<AttributeType>(object o) where AttributeType : MessageParameterAttribute {
			return o.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(AttributeType))).OrderBy(p => p.GetCustomAttribute<AttributeType>().Index).ToList();
		}

		public void Deserialize<AttributeType>(BinaryReader reader) where AttributeType : MessageParameterAttribute {
			MessageHandler<ContextType>.GetProperties<AttributeType>(this).ForEach(f => MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, this));
		}

		public void Serialize<AttributeType>(BinaryWriter writer) where AttributeType : MessageParameterAttribute {
			MessageHandler<ContextType>.GetProperties<AttributeType>(this).ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, this));
		}

		private static void Deserialize<AttributeType>(BinaryReader reader, PropertyInfo property, object o) where AttributeType : MessageParameterAttribute {
			if (property.PropertyType == typeof(string)) {
				property.SetValue(o, reader.ReadString());
			}
			else if (property.PropertyType == typeof(bool)) {
				property.SetValue(o, reader.ReadBoolean());
			}
			else if (property.PropertyType == typeof(byte)) {
				property.SetValue(o, reader.ReadByte());
			}
			else if (property.PropertyType == typeof(sbyte)) {
				property.SetValue(o, reader.ReadSByte());
			}
			else if (property.PropertyType == typeof(ushort)) {
				property.SetValue(o, reader.ReadUInt16());
			}
			else if (property.PropertyType == typeof(short)) {
				property.SetValue(o, reader.ReadInt16());
			}
			else if (property.PropertyType == typeof(uint)) {
				property.SetValue(o, reader.ReadUInt32());
			}
			else if (property.PropertyType == typeof(int)) {
				property.SetValue(o, reader.ReadInt32());
			}
			else if (property.PropertyType == typeof(ulong)) {
				property.SetValue(o, reader.ReadUInt64());
			}
			else if (property.PropertyType == typeof(long)) {
				property.SetValue(o, reader.ReadInt64());
			}
			else if (property.PropertyType == typeof(DateTime)) {
				property.SetValue(o, MessageHandler<ContextType>.DateTimeEpoch.AddMilliseconds(reader.ReadUInt64()));
			}
			else if (property.PropertyType.GetInterfaces().Any(i => i == typeof(IList))) {
				var itemType = property.PropertyType.GenericTypeArguments[0];
				var itemConstructor = itemType.GetConstructor(Type.EmptyTypes);
				var propertyConstructor = property.PropertyType.GetConstructor(Type.EmptyTypes);
				var collection = (IList)propertyConstructor.Invoke(null);

				var fields = MessageHandler<ContextType>.GetProperties<AttributeType>(itemConstructor.Invoke(null));
				var count = reader.ReadUInt16();

				for (var i = 0; i < count; i++) {
					var newObject = itemConstructor.Invoke(null);

					fields.ForEach(f => MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, newObject));

					collection.Add(newObject);
				}

				property.SetValue(o, collection);
			}
			else {
				var child = property.GetValue(o);

				MessageHandler<ContextType>.GetProperties<AttributeType>(child).ForEach(f => MessageHandler<ContextType>.Deserialize<AttributeType>(reader, f, child));
			}
		}

		private static void Serialize<AttributeType>(BinaryWriter writer, PropertyInfo property, object o) where AttributeType : MessageParameterAttribute {
			var child = property.GetValue(o);

			if (property.PropertyType == typeof(string)) {
				writer.Write((string)child);
			}
			else if (property.PropertyType == typeof(bool)) {
				writer.Write((bool)child);
			}
			else if (property.PropertyType == typeof(byte)) {
				writer.Write((byte)child);
			}
			else if (property.PropertyType == typeof(sbyte)) {
				writer.Write((sbyte)child);
			}
			else if (property.PropertyType == typeof(ushort)) {
				writer.Write((ushort)child);
			}
			else if (property.PropertyType == typeof(short)) {
				writer.Write((short)child);
			}
			else if (property.PropertyType == typeof(uint)) {
				writer.Write((uint)child);
			}
			else if (property.PropertyType == typeof(int)) {
				writer.Write((int)child);
			}
			else if (property.PropertyType == typeof(ulong)) {
				writer.Write((ulong)child);
			}
			else if (property.PropertyType == typeof(long)) {
				writer.Write((long)child);
			}
			else if (property.PropertyType == typeof(DateTime)) {
				writer.Write((ulong)((DateTime)child - MessageHandler<ContextType>.DateTimeEpoch).TotalMilliseconds);
			}
			else if (property.PropertyType.GetInterfaces().Any(i => i == typeof(IList))) {
				var collection = (IList)child;

				writer.Write((ushort)collection.Count);

				if (collection.Count == 0)
					return;

				var fields = MessageHandler<ContextType>.GetProperties<AttributeType>(collection[0]);

				for (var i = 0; i < collection.Count; i++)
					fields.ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, collection[i]));

				collection.Clear();
			}
			else {
				MessageHandler<ContextType>.GetProperties<AttributeType>(child).ForEach(f => MessageHandler<ContextType>.Serialize<AttributeType>(writer, f, child));
			}
		}

		public virtual bool IsValid() {
			return !this.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ValidationAttribute))).Any(f => f.GetCustomAttributes<ValidationAttribute>().Any(a => !a.IsValid(f.GetValue(this))));
		}

		public static uint GetKey(ushort category, ushort method) {
			return (uint)((category << 16) | method);
		}

		public uint GetKey() {
			return MessageHandler<ContextType>.GetKey(this.Category, this.Method);
		}

		protected void SendNotification(long targetAuthenticatedId, ushort notificationType) {
			this.Notifications.Add(new Notification(targetAuthenticatedId, notificationType, 0));
		}

		protected void SendNotification(long targetAuthenticatedId, ushort notificationType, long objectId) {
			this.Notifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));
		}
	}

	public abstract class ListQueryMessageHandler<ContextType, EntryType> : MessageHandler<ContextType> {
		public const int InputStartIndex = 4;
		public const int OutputStartIndex = 1;

		[MessageInput(Index = 0)]
		[DataAnnotations.AtLeast(0)]
		public uint Skip { get; set; }

		[MessageInput(Index = 1)]
		[DataAnnotations.AtLeast(0)]
		public uint Take { get; set; }

		[MessageInput(Index = 2)]
		[DataAnnotations.ApiString(MinLength = 1)]
		public string OrderByField { get; set; }

		[MessageInput(Index = 3)]
		public bool OrderByAscending { get; set; }

		[MessageOutput(Index = 0)]
		public List<EntryType> Entries { get; set; }

		protected void OrderTakeAndSet(IQueryable<EntryType> query) {
			var parameter = Expression.Parameter(typeof(EntryType));
			var property = Expression.Property(parameter, this.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(EntryType), property.Type }, query.Expression, quote);

			this.Entries = query.Provider.CreateQuery<EntryType>(call).Skip((int)this.Skip).Take((int)this.Take).ToList();
		}
	}
}