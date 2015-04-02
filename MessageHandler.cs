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
	public enum MessageParameterDirection {
		Input,
		Output
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class MessageDefinitionAttribute : Attribute {
		public long Id { get; set; }
		public long ServerId { get; set; }
		public long AuthenticationLevelRequired { get; set; }
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class MessageParameterAttribute : Attribute {
		public long Index { get; set; }
		public MessageParameterDirection Direction { get; set; }
	}

	public abstract class MessageHandler {
		private class ParameterNode {
			public PropertyInfo Property { get; set; }
			public Type ListType { get; set; }
			public List<ParameterNode> Children { get; set; }
		}

		private class BoundProperty {
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

		public long AuthenticatedId { get; set; }
		public long AuthenticatedLevel { get; set; }
		public MessageContext Context { get; set; }

		internal List<Notification> GeneratedNotifications { get; private set; }

		public virtual bool Valid => this.validationProperties.All(p => p.Attributes.All(a => a.IsValid(p.Property.GetValue(this))));
		public virtual long Perform() => ResponseCode.Success;

		public abstract long PrepareAndPerform();

		protected void BindResponse(object obj) => this.BindObject(obj, MessageParameterDirection.Output);
		protected void SendNotification(long targetAuthenticatedId, long notificationType) => this.SendNotification(targetAuthenticatedId, notificationType, 0);
		protected void SendNotification(long targetAuthenticatedId, long notificationType, long objectId) => this.GeneratedNotifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));

		private void AddSerializationDefinition<T>(Action<BinaryWriter, ParameterNode, T> serializer, Func<BinaryReader, ParameterNode, T> deserializer) => this.serializationDefinitions.Add(typeof(T), new SerializationDefinition<T> { Serializer = serializer, Deserializer = deserializer });

		protected MessageHandler() {
			this.AuthenticatedId = 0;
			this.GeneratedNotifications = new List<Notification>();

			this.inputProperties = this.CreateTree(MessageParameterDirection.Input);
			this.outputProperties = this.CreateTree(MessageParameterDirection.Output);

			this.validationProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(ValidationAttribute)))
				.Select(p => new ValidationProperty() { Property = p, Attributes = p.GetCustomAttributes<ValidationAttribute>().ToList() })
				.ToList();

			this.serializationDefinitions = new Dictionary<Type, ISerializationDefinition>();
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
			this.AddSerializationDefinition((r, p, o) => r.Write((ulong)((o - MessageHandler.DateTimeEpoch).TotalMilliseconds)), (w, p) => MessageHandler.DateTimeEpoch.AddMilliseconds(w.ReadUInt64()));
		}

		public void Serialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var writer = new BinaryWriter(stream))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Serialize(writer, property, this);
		}

		public void Deserialize(MessageParameterDirection direction, MemoryStream stream) {
			using (var reader = new BinaryReader(stream))
				foreach (var property in direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties)
					this.Deserialize(reader, property, this);
		}

		protected void BindObject(object obj, MessageParameterDirection direction) {
			if (this.boundProperties == null) {
				var sourceProperties = obj.GetType().GetProperties();
				var targetProperties = direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties;

				this.boundProperties = targetProperties
					.Where(p => sourceProperties.Any(s => s.Name == p.Property.Name))
					.Select(p => new BoundProperty() { Parameter = p, Property = sourceProperties.SingleOrDefault(b => b.Name == p.Property.Name) })
					.ToList();
			}

			foreach (var p in this.boundProperties)
				p.Parameter.Property.SetValue(this, Convert.ChangeType(p.Property.GetValue(obj), p.Property.PropertyType));
		}

		private List<PropertyInfo> GetProperties(Type type, MessageParameterDirection direction) {
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(MessageParameterAttribute)))
				.Where(p => p.GetCustomAttribute<MessageParameterAttribute>().Direction == direction)
				.OrderBy(p => p.GetCustomAttribute<MessageParameterAttribute>().Index)
				.ToList();
		}

		private List<ParameterNode> CreateTree(MessageParameterDirection direction) {
			return this.GetProperties(this.GetType(), direction).Select(p => this.CreateTree(direction, p)).ToList();
		}

		private ParameterNode CreateTree(MessageParameterDirection direction, PropertyInfo property) {
			var node = new ParameterNode();

			if (!property.PropertyType.GetInterfaces().Any(i => i == typeof(IList<>))) {
				node.ListType = null;
				node.Property = property;
				node.Children = this.GetProperties(property.PropertyType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}
			else {
				node.ListType = property.PropertyType.GenericTypeArguments.Single();
				node.Property = property;
				node.Children = this.GetProperties(node.ListType, direction).Select(p => this.CreateTree(direction, p)).ToList();
			}

			return node;
		}

		private void Serialize(BinaryWriter writer, ParameterNode node, object obj) {
			ISerializationDefinition def;

			if (this.serializationDefinitions.TryGetValue(node.Property.PropertyType, out def)) {
				def.Serialize(writer, node, obj);
			}
			else if (node.ListType != null) {
				this.SerializeList(writer, node, obj);
			}
			else {
				node.Children.ForEach(f => this.Serialize(writer, f, node.Property.GetValue(obj)));
			}
		}

		private void Deserialize(BinaryReader reader, ParameterNode node, object obj) {
			ISerializationDefinition def;
				
			if (this.serializationDefinitions.TryGetValue(node.Property.PropertyType, out def)) {
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
		public new T Context { get; set; }
		
		public override long PrepareAndPerform() {
			this.Context = (T)base.Context;

			return this.Perform();
		}
	}

	public abstract class ListMessageHandler<TContext, TEntry> : MessageHandler<TContext> where TContext : MessageContext {
		public static long InputStartIndex => 4;
		public static long OutputStartIndex => 1;

		[MessageParameter(Direction = MessageParameterDirection.Input, Index = 0)]
		[DataAnnotations.AtLeast(0)]
		public uint Skip { get; set; }

		[MessageParameter(Direction = MessageParameterDirection.Input, Index = 1)]
		[DataAnnotations.AtLeast(0)]
		public uint Take { get; set; }

		[MessageParameter(Direction = MessageParameterDirection.Input, Index = 2)]
		[DataAnnotations.ApiString(MinLength = 1)]
		public string OrderByField { get; set; }

		[MessageParameter(Direction = MessageParameterDirection.Input, Index = 3)]
		public bool OrderByAscending { get; set; }

		[MessageParameter(Direction = MessageParameterDirection.Output, Index = 0)]
		public List<TEntry> List { get; set; }

		protected void SetResponse(IQueryable<TEntry> query) {
			var parameter = Expression.Parameter(typeof(TEntry));
			var property = Expression.Property(parameter, this.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(TEntry), property.Type }, query.Expression, quote);

			this.List = query.Provider.CreateQuery<TEntry>(call).Skip((int)this.Skip).Take((int)this.Take).ToList();
		}
	}
}