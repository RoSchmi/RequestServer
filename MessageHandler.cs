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

	public abstract class MessageHandler<TContext> {
		private class Node {
			public PropertyInfo Property { get; set; }
			public Type ListType { get; set; }
			public List<Node> Children { get; set; }
		}

		public static DateTime DateTimeEpoch { get; set; } = new DateTime(2015, 1, 1, 0, 0, 0);

		private List<Node> inputProperties;
		private List<Node> outputProperties;
		private List<Tuple<Node, PropertyInfo>> bindProperties;
		private List<Tuple<ValidationAttribute, PropertyInfo>> validationProperties;

		public long AuthenticatedId { get; set; }
		public int AuthenticatedLevel { get; set; }
		public TContext Context { get; set; }
		public List<Notification> GeneratedNotifications { get; private set; }

		public virtual ushort Perform() {
			return ResponseCode.Success;
		}

		public MessageHandler() {
			this.AuthenticatedId = 0;
			this.Context = default(TContext);
			this.GeneratedNotifications = new List<Notification>();

			this.inputProperties = this.CreateTree(MessageParameterDirection.Input);
			this.outputProperties = this.CreateTree(MessageParameterDirection.Output);
			this.validationProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ValidationAttribute))).Select(p => Tuple.Create(p.GetCustomAttribute<ValidationAttribute>(), p)).ToList();
		}

		private List<PropertyInfo> GetProperties(Type type, MessageParameterDirection direction) {
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(MessageParameterAttribute)) && p.GetCustomAttribute<MessageParameterAttribute>().Direction == direction)
				.OrderBy(p => p.GetCustomAttribute<MessageParameterAttribute>().Index)
				.ToList();
		}

		public void Deserialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var reader = new BinaryReader(stream))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Deserialize(reader, property, this);
		}

		public void Serialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var writer = new BinaryWriter(stream))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Serialize(writer, property, this);
		}

		protected void BindResponseObject(object obj) {
			if (this.bindProperties == null) {
				this.bindProperties = new List<Tuple<Node, PropertyInfo>>();

				var boundProperties = obj.GetType().GetProperties();

				foreach (var property in this.outputProperties) {
					var bound = boundProperties.SingleOrDefault(b => property.ListType == null && b.Name == property.Property.Name);

					if (bound != null)
						this.bindProperties.Add(Tuple.Create(property, bound));
				}
			}

			foreach (var p in this.bindProperties)
				p.Item1.Property.SetValue(this, Convert.ChangeType(p.Item2.GetValue(obj), p.Item2.PropertyType));
		}

		private List<Node> CreateTree(MessageParameterDirection direction) {
			return this.GetProperties(this.GetType(), direction).Select(p => this.CreateTree(direction, p)).ToList();
		}

		private Node CreateTree(MessageParameterDirection direction, PropertyInfo property) {
			var node = new Node();

			if (!property.PropertyType.GetInterfaces().Any(i => i == typeof(IList))) {
				node.ListType = null;
				node.Property = property;
				node.Children = this.GetProperties(property.PropertyType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}
			else {
				node.ListType = property.PropertyType.GenericTypeArguments[0];
				node.Property = property;
				node.Children = this.GetProperties(node.ListType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}

			return node;
		}

		private void Deserialize(BinaryReader reader, Node node, object obj) {
			if (node.ListType != null) {
				var constructor = node.ListType.GetConstructor(Type.EmptyTypes);
				var propertyConstructor = node.Property.PropertyType.GetConstructor(Type.EmptyTypes);
				var collection = (IList)propertyConstructor.Invoke(null);

				var fields = node.Children;
				var count = reader.ReadUInt16();

				for (var i = 0; i < count; i++) {
					var newObject = constructor.Invoke(null);

					fields.ForEach(f => this.Deserialize(reader, f, newObject));

					collection.Add(newObject);
				}

				node.Property.SetValue(obj, collection);
			}
			else if (node.Property.PropertyType == typeof(string)) {
				node.Property.SetValue(obj, reader.ReadString());
			}
			else if (node.Property.PropertyType == typeof(bool)) {
				node.Property.SetValue(obj, reader.ReadBoolean());
			}
			else if (node.Property.PropertyType == typeof(byte)) {
				node.Property.SetValue(obj, reader.ReadByte());
			}
			else if (node.Property.PropertyType == typeof(sbyte)) {
				node.Property.SetValue(obj, reader.ReadSByte());
			}
			else if (node.Property.PropertyType == typeof(ushort)) {
				node.Property.SetValue(obj, reader.ReadUInt16());
			}
			else if (node.Property.PropertyType == typeof(short)) {
				node.Property.SetValue(obj, reader.ReadInt16());
			}
			else if (node.Property.PropertyType == typeof(uint)) {
				node.Property.SetValue(obj, reader.ReadUInt32());
			}
			else if (node.Property.PropertyType == typeof(int)) {
				node.Property.SetValue(obj, reader.ReadInt32());
			}
			else if (node.Property.PropertyType == typeof(ulong)) {
				node.Property.SetValue(obj, reader.ReadUInt64());
			}
			else if (node.Property.PropertyType == typeof(long)) {
				node.Property.SetValue(obj, reader.ReadInt64());
			}
			else if (node.Property.PropertyType == typeof(DateTime)) {
				node.Property.SetValue(obj, MessageHandler<TContext>.DateTimeEpoch.AddMilliseconds(reader.ReadUInt64()));
			}
			else {
				node.Children.ForEach(f => this.Deserialize(reader, f, node.Property.GetValue(obj)));
			}
		}

		private void Serialize(BinaryWriter writer, Node node, object obj) {
			var child = node.Property.GetValue(obj);

			if (node.ListType != null) {
				var collection = (IList)child;

				writer.Write((ushort)collection.Count);

				if (collection.Count == 0)
					return;

				for (var i = 0; i < collection.Count; i++)
					node.Children.ForEach(f => this.Serialize(writer, f, collection[i]));

				collection.Clear();
			}
			else if (node.Property.PropertyType == typeof(string)) {
				writer.Write((string)child);
			}
			else if (node.Property.PropertyType == typeof(bool)) {
				writer.Write((bool)child);
			}
			else if (node.Property.PropertyType == typeof(byte)) {
				writer.Write((byte)child);
			}
			else if (node.Property.PropertyType == typeof(sbyte)) {
				writer.Write((sbyte)child);
			}
			else if (node.Property.PropertyType == typeof(ushort)) {
				writer.Write((ushort)child);
			}
			else if (node.Property.PropertyType == typeof(short)) {
				writer.Write((short)child);
			}
			else if (node.Property.PropertyType == typeof(uint)) {
				writer.Write((uint)child);
			}
			else if (node.Property.PropertyType == typeof(int)) {
				writer.Write((int)child);
			}
			else if (node.Property.PropertyType == typeof(ulong)) {
				writer.Write((ulong)child);
			}
			else if (node.Property.PropertyType == typeof(long)) {
				writer.Write((long)child);
			}
			else if (node.Property.PropertyType == typeof(DateTime)) {
				writer.Write((ulong)((DateTime)child - MessageHandler<TContext>.DateTimeEpoch).TotalMilliseconds);
			}
			else {
				node.Children.ForEach(f => this.Serialize(writer, f, child));
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

		[MessageParameterAttribute(Direction = MessageParameterDirection.Input, Index = 0)]
		[DataAnnotations.AtLeast(0)]
		public uint Skip { get; set; }

		[MessageParameterAttribute(Direction = MessageParameterDirection.Input, Index = 1)]
		[DataAnnotations.AtLeast(0)]
		public uint Take { get; set; }

		[MessageParameterAttribute(Direction = MessageParameterDirection.Input, Index = 2)]
		[DataAnnotations.ApiString(MinLength = 1)]
		public string OrderByField { get; set; }

		[MessageParameterAttribute(Direction = MessageParameterDirection.Input, Index = 3)]
		public bool OrderByAscending { get; set; }

		[MessageParameterAttribute(Direction = MessageParameterDirection.Output, Index = 0)]
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