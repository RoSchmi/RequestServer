using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

	public enum MessageParameterDirection {
		Input,
		Output
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class MessageDefinitionAttribute : Attribute {
		public ushort Category { get; set; }
		public ushort Method { get; set; }
		public int ServerId { get; set; }
		public int AuthenticationLevelRequired { get; set; }
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class MessageParameterAttribute : Attribute {
		public int Index { get; set; }
		public MessageParameterDirection Direction { get; set; }
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class MessageInputAttribute : MessageParameterAttribute {

	}

	[AttributeUsage(AttributeTargets.Property)]
	public class MessageOutputAttribute : MessageParameterAttribute {

	}

	public abstract class MessageHandler<TContext> {
		public static DateTime DateTimeEpoch { get; set; } = new DateTime(2015, 1, 1, 0, 0, 0);

		private List<PropertyInfo> inputProperties;
		private List<PropertyInfo> outputProperties;
		private List<Tuple<PropertyInfo, PropertyInfo>> bindProperties;
		private List<Tuple<ValidationAttribute, PropertyInfo>> validationProperties;

		public long AuthenticatedId { get; set; }
		public TContext Context { get; set; }
		public List<Notification> GeneratedNotifications { get; private set; }

		public virtual ushort Perform() {
			return ResponseCode.Success;
		}

		public MessageHandler() {
			this.AuthenticatedId = 0;
			this.Context = default(TContext);
			this.GeneratedNotifications = new List<Notification>();

			this.inputProperties = this.GetProperties(this.GetType(), MessageParameterDirection.Input);
			this.outputProperties = this.GetProperties(this.GetType(), MessageParameterDirection.Output);
			this.validationProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ValidationAttribute))).Select(p => Tuple.Create(p.GetCustomAttribute<ValidationAttribute>(), p)).ToList();
		}

		private List<PropertyInfo> GetProperties(Type type, MessageParameterDirection direction) {
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(MessageParameterAttribute)) && p.GetCustomAttribute<MessageParameterAttribute>().Direction == direction)
				.OrderBy(p => p.GetCustomAttribute<MessageParameterAttribute>().Index)
				.ToList();
		}

		public void Deserialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var reader = new BinaryReader(stream)) {
				var properties = direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties;

				foreach (var property in properties)
					this.Deserialize(direction, reader, property, this);
			}
		}

		public void Serialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var writer = new BinaryWriter(stream)) {
				var properties = direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties;

				foreach (var property in properties)
					this.Serialize(direction, writer, property, this);
			}
		}

		protected void BindResponseObject(object obj) {
			if (this.bindProperties == null) {
				this.bindProperties = new List<Tuple<PropertyInfo, PropertyInfo>>();

				var boundProperties = obj.GetType().GetProperties();

				foreach (var property in this.outputProperties) {
					var bound = boundProperties.SingleOrDefault(b => b.Name == property.Name);

					if (bound != null)
						this.bindProperties.Add(Tuple.Create(property, bound));
				}
			}

			foreach (var p in this.bindProperties)
				p.Item1.SetValue(this, Convert.ChangeType(p.Item2.GetValue(obj), p.Item2.PropertyType));
		}

		private void Deserialize(MessageParameterDirection direction, BinaryReader reader, PropertyInfo property, object o) {
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
				property.SetValue(o, MessageHandler<TContext>.DateTimeEpoch.AddMilliseconds(reader.ReadUInt64()));
			}
			else if (property.PropertyType.GetInterfaces().Any(i => i == typeof(IList))) {
				var itemType = property.PropertyType.GenericTypeArguments[0];
				var itemConstructor = itemType.GetConstructor(Type.EmptyTypes);
				var propertyConstructor = property.PropertyType.GetConstructor(Type.EmptyTypes);
				var collection = (IList)propertyConstructor.Invoke(null);

				var fields = this.GetProperties(itemType, direction);
				var count = reader.ReadUInt16();

				for (var i = 0; i < count; i++) {
					var newObject = itemConstructor.Invoke(null);

					fields.ForEach(f => this.Deserialize(direction, reader, f, newObject));

					collection.Add(newObject);
				}

				property.SetValue(o, collection);
			}
			else {
				var child = property.GetValue(o);

				this.GetProperties(property.PropertyType, direction).ForEach(f => this.Deserialize(direction, reader, f, child));
			}
		}

		private void Serialize(MessageParameterDirection direction, BinaryWriter writer, PropertyInfo property, object o) {
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
				writer.Write((ulong)((DateTime)child - MessageHandler<TContext>.DateTimeEpoch).TotalMilliseconds);
			}
			else if (property.PropertyType.GetInterfaces().Any(i => i == typeof(IList))) {
				var collection = (IList)child;

				writer.Write((ushort)collection.Count);

				if (collection.Count == 0)
					return;

				var fields = this.GetProperties(property.PropertyType.GenericTypeArguments[0], direction);

				for (var i = 0; i < collection.Count; i++)
					fields.ForEach(f => this.Serialize(direction, writer, f, collection[i]));

				collection.Clear();
			}
			else {
				this.GetProperties(property.PropertyType, direction).ForEach(f => this.Serialize(direction, writer, f, child));
			}
		}

		public virtual bool IsValid() {
			return !this.validationProperties.Any(v => !v.Item1.IsValid(v.Item2.GetValue(this)));
		}

		protected void SendNotification(long targetAuthenticatedId, ushort notificationType) {
			this.SendNotification(targetAuthenticatedId, notificationType, 0);
		}

		protected void SendNotification(long targetAuthenticatedId, ushort notificationType, long objectId) {
			this.GeneratedNotifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));
		}
	}

	public abstract class ListMessageHandler<TContext, TEntry> : MessageHandler<TContext> {
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
		public List<TEntry> Entries { get; set; }

		protected void OrderTakeAndSet(IQueryable<TEntry> query) {
			var parameter = Expression.Parameter(typeof(TEntry));
			var property = Expression.Property(parameter, this.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(TEntry), property.Type }, query.Expression, quote);

			this.Entries = query.Provider.CreateQuery<TEntry>(call).Skip((int)this.Skip).Take((int)this.Take).ToList();
		}
	}
}