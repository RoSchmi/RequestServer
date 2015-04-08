using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ArkeIndustries.RequestServer {
	public enum MessageParameterDirection {
		Input,
		Output
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class MessageDefinitionAttribute : Attribute {
		public long Id { get; }
		public long ServerId { get; }
		public long AuthenticationLevelRequired { get; }

		public MessageDefinitionAttribute(long id, long serverId, long authenticationLevelRequired) {
			this.Id = id;
			this.ServerId = serverId;
			this.AuthenticationLevelRequired = authenticationLevelRequired;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class MessageParameterAttribute : Attribute {
		public long Index { get; }
		public MessageParameterDirection Direction { get; }

		public MessageParameterAttribute(long index, MessageParameterDirection direction) {
			this.Index = index;
			this.Direction = direction;
		}
	}

	public abstract class MessageHandler {
		internal class ParameterNode {
			public PropertyInfo Property { get; set; }
			public Type ListType { get; set; }
			public List<ParameterNode> Children { get; set; }
		}

		internal class BoundProperty {
			public PropertyInfo Property { get; set; }
			public ParameterNode Parameter { get; set; }
		}

		private class ValidationProperty {
			public List<ValidationAttribute> Attributes { get; set; }
			public PropertyInfo Property { get; set; }
		}

		private interface ISerializationDefinition {
			void Serialize(BinaryWriter writer, ParameterNode node, object obj);
			object Deserialize(BinaryReader reader, ParameterNode node);
		}

		private class SerializationDefinition<T> : ISerializationDefinition {
			public Action<BinaryWriter, ParameterNode, T> Serializer { get; set; }
			public Func<BinaryReader, ParameterNode, T> Deserializer { get; set; }

			public void Serialize(BinaryWriter writer, ParameterNode node, object obj) => this.Serializer(writer, node, (T)obj);
			public object Deserialize(BinaryReader reader, ParameterNode node) => this.Deserializer(reader, node);
		}

		public static DateTime DateTimeEpoch { get; set; } = new DateTime(2015, 1, 1, 0, 0, 0);

		private List<ParameterNode> inputProperties;
		private List<ParameterNode> outputProperties;
		private List<BoundProperty> boundProperties;
		private List<ValidationProperty> validationProperties;
		private Dictionary<Type, ISerializationDefinition> serializationDefinitions;

		public MessageContext Context { get; set; }

		internal List<Notification> GeneratedNotifications { get; private set; }

		public virtual long Perform() => ResponseCode.Success;

		protected void BindObjectToResponse(object source) => this.BindObjectToResponse(source, MessageParameterDirection.Output);
		protected void SendNotification(long targetAuthenticatedId, long notificationType) => this.SendNotification(targetAuthenticatedId, notificationType, 0);
		protected void SendNotification(long targetAuthenticatedId, long notificationType, long objectId) => this.GeneratedNotifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));

		private void AddSerializationDefinition<T>(Action<BinaryWriter, ParameterNode, T> serializer, Func<BinaryReader, ParameterNode, T> deserializer) => this.serializationDefinitions.Add(typeof(T), new SerializationDefinition<T> { Serializer = serializer, Deserializer = deserializer });

		protected MessageHandler() {
			this.GeneratedNotifications = new List<Notification>();

			this.serializationDefinitions = new Dictionary<Type, ISerializationDefinition>();
			this.inputProperties = this.CreateTree(MessageParameterDirection.Input);
			this.outputProperties = this.CreateTree(MessageParameterDirection.Output);

			this.validationProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(ValidationAttribute)))
				.Select(p => new ValidationProperty() { Property = p, Attributes = p.GetCustomAttributes<ValidationAttribute>().ToList() })
				.ToList();

			this.AddSerializationDefinitions();
		}

		[SuppressMessage("Microsoft.Maintainability", "CA1502")]
		private void AddSerializationDefinitions() {
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadString());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadBoolean());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadByte());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadSByte());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadUInt16());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadInt16());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadUInt32());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadInt32());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadUInt64());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadInt64());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadSingle());
			this.AddSerializationDefinition((r, p, o) => r.Write(o), (w, p) => w.ReadDouble());
			this.AddSerializationDefinition((r, p, o) => r.Write((ulong)((o - MessageHandler.DateTimeEpoch).TotalMilliseconds)), (w, p) => MessageHandler.DateTimeEpoch.AddMilliseconds(w.ReadUInt64()));
		}

		public long IsValid() {
			foreach (var p in this.validationProperties) {
				var value = p.Property.GetValue(this);

				foreach (var v in p.Attributes) {
					var valid = v.IsValid(value, this.Context);

					if (valid != ResponseCode.Success)
						return valid;
				}
			}

			return ResponseCode.Success;
		}

		public void Serialize(MessageParameterDirection direction, Stream stream) {
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Serialize(writer, property, this);
		}

		public void Deserialize(MessageParameterDirection direction, Stream stream) {
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Deserialize(reader, property, this);
		}

		protected void BindObjectToResponse(object source, MessageParameterDirection direction) {
			if (source == null) throw new ArgumentNullException(nameof(source));

			if (this.boundProperties == null)
				this.boundProperties = MessageHandler.GetPropertiesToBind(source.GetType(), direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties);

			foreach (var p in this.boundProperties)
				p.Parameter.Property.SetValue(this, Convert.ChangeType(p.Property.GetValue(source), p.Property.PropertyType, CultureInfo.InvariantCulture));
		}

		internal static List<BoundProperty> GetPropertiesToBind(Type type, List<ParameterNode> targetProperties) {
			var sourceProperties = type.GetProperties();

			return targetProperties
				.Where(p => sourceProperties.Any(s => s.Name == p.Property.Name))
				.Select(p => new BoundProperty() { Parameter = p, Property = sourceProperties.SingleOrDefault(b => b.Name == p.Property.Name) })
				.ToList();
		}

		internal static List<PropertyInfo> GetProperties(Type type, MessageParameterDirection direction) {
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(MessageParameterAttribute)))
				.Where(p => p.GetCustomAttribute<MessageParameterAttribute>().Direction == direction)
				.OrderBy(p => p.GetCustomAttribute<MessageParameterAttribute>().Index)
				.ToList();
		}

		private List<ParameterNode> CreateTree(MessageParameterDirection direction) {
			return this.CreateTree(direction, this.GetType());
		}

		internal List<ParameterNode> CreateTree(MessageParameterDirection direction, Type type) {
			return MessageHandler.GetProperties(type, direction).Select(p => this.CreateTree(direction, p)).ToList();
		}

		private ParameterNode CreateTree(MessageParameterDirection direction, PropertyInfo property) {
			var node = new ParameterNode();

			if (property.PropertyType.GetInterfaces().Any(i => i == typeof(IList<>))) {
				node.ListType = property.PropertyType.GenericTypeArguments.Single();
				node.Property = property;
				node.Children = MessageHandler.GetProperties(node.ListType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}
			else {
				node.ListType = null;
				node.Property = property;
				node.Children = MessageHandler.GetProperties(property.PropertyType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}

			return node;
		}

		private void Serialize(BinaryWriter writer, ParameterNode node, object obj) {
			ISerializationDefinition def;
			object value = node.Property.GetValue(obj);

            if (!node.Property.PropertyType.IsEnum) {
				this.serializationDefinitions.TryGetValue(node.Property.PropertyType, out def);
			}
			else {
				this.serializationDefinitions.TryGetValue(Enum.GetUnderlyingType(node.Property.PropertyType), out def);
			}

			if (def != null) {
				def.Serialize(writer, node, value);
			}
			else if (node.ListType != null) {
				this.SerializeList(writer, node, value);
			}
			else {
				node.Children.ForEach(f => this.Serialize(writer, f, value));
			}
		}

		private void Deserialize(BinaryReader reader, ParameterNode node, object obj) {
			ISerializationDefinition def;
			
			if (!node.Property.PropertyType.IsEnum) {
				this.serializationDefinitions.TryGetValue(node.Property.PropertyType, out def);
            } else {
				this.serializationDefinitions.TryGetValue(Enum.GetUnderlyingType(node.Property.PropertyType), out def);
			}

			if (def != null) {
				node.Property.SetValue(obj, def.Deserialize(reader, node));
			}
			else if (node.ListType != null) {
				node.Property.SetValue(obj, this.DeserializeList(reader, node));
			}
			else {
				node.Children.ForEach(f => this.Deserialize(reader, f, node.Property.GetValue(obj)));
			}
		}

		private void SerializeList(BinaryWriter writer, ParameterNode node, object obj) {
			var child = node.Property.GetValue(obj);
			var collection = (IList)child;

			writer.Write((ushort)collection.Count);

			if (collection.Count == 0)
				return;

			for (var i = 0; i < collection.Count; i++)
				node.Children.ForEach(f => this.Serialize(writer, f, collection[i]));

			collection.Clear();
		}

		private object DeserializeList(BinaryReader reader, ParameterNode node) {
			var objectConstructor = node.ListType.GetConstructor(Type.EmptyTypes);
			var collectionConstructor = node.Property.PropertyType.GetConstructor(Type.EmptyTypes);
			var collection = (IList)collectionConstructor.Invoke(null);
			var count = reader.ReadUInt16();

			for (var i = 0; i < count; i++) {
				var newObject = objectConstructor.Invoke(null);

				node.Children.ForEach(f => this.Deserialize(reader, f, newObject));

				collection.Add(newObject);
			}

			return collection;
		}
	}

	public abstract class MessageHandler<T> : MessageHandler where T : MessageContext {
		public new T Context {
			get {
				return (T)base.Context;
			}
			set {
				base.Context = value;
			}
		}
	}

	public abstract class ListMessageHandler<TContext, TEntry> : MessageHandler<TContext> where TContext : MessageContext where TEntry : new() {
		private List<BoundProperty> boundProperties;

		[MessageParameter(-4, MessageParameterDirection.Input)]
		[AtLeast(0)]
		public int Skip { get; set; }

		[MessageParameter(-3, MessageParameterDirection.Input)]
		[AtLeast(0)]
		public int Take { get; set; }

		[MessageParameter(-2, MessageParameterDirection.Input)]
		[ApiString(false, 1)]
		public string OrderByField { get; set; }

		[MessageParameter(-1, MessageParameterDirection.Input)]
		public bool OrderByAscending { get; set; }

		[MessageParameter(-1, MessageParameterDirection.Output)]
		public IReadOnlyList<TEntry> List { get; private set; }

		protected void SetResponse(IQueryable<TEntry> query) {
			if (query == null) throw new ArgumentNullException(nameof(query));

			var parameter = Expression.Parameter(typeof(TEntry));
			var property = Expression.Property(parameter, this.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(TEntry), property.Type }, query.Expression, quote);

			this.List = query.Provider.CreateQuery<TEntry>(call).Skip(this.Skip).Take(this.Take).ToList();
		}

		protected void BindListToResponse<T>(IQueryable<T> query) {
			if (query == null) throw new ArgumentNullException(nameof(query));

			var parameter = Expression.Parameter(typeof(T));
			var property = Expression.Property(parameter, this.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(T), property.Type }, query.Expression, quote);

			if (this.boundProperties == null)
				this.boundProperties = MessageHandler.GetPropertiesToBind(typeof(T), this.CreateTree(MessageParameterDirection.Output, typeof(TEntry)));

			var result = new List<TEntry>();
			foreach (var sourceEntry in query.Provider.CreateQuery<T>(call).Skip(this.Skip).Take(this.Take)) {
				var resultEntry = new TEntry();

				foreach (var p in this.boundProperties)
					p.Parameter.Property.SetValue(resultEntry, Convert.ChangeType(p.Property.GetValue(sourceEntry), p.Property.PropertyType, CultureInfo.InvariantCulture));

				result.Add(resultEntry);
			}

			this.List = result;
		}
	}
}