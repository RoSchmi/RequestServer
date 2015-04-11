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

		public MessageParameterAttribute(long index) {
			this.Index = index;
		}
	}

	public abstract class MessageHandler {
		internal class ParameterNode {
			public PropertyInfo Property { get; set; }
			public Type ListGenericType { get; set; }
			public ISerializationDefinition ListMemberSerializationDefinition { get; set; }
			public ISerializationDefinition SerializationDefinition { get; set; }
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

		internal interface ISerializationDefinition {
			void Serialize(BinaryWriter writer, ParameterNode node, object obj);
			object Deserialize(BinaryReader reader, ParameterNode node);
		}

		internal class SerializationDefinition<T> : ISerializationDefinition {
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
		public object Request { get; set; }
		public object Response { get; set; }

		internal List<Notification> GeneratedNotifications { get; private set; }

		public virtual long Perform() => ResponseCode.Success;

		protected long BindResponse(object source) => this.BindResponse(source, MessageParameterDirection.Output);
		protected void SendNotification(long targetAuthenticatedId, long notificationType) => this.SendNotification(targetAuthenticatedId, notificationType, 0);
		protected void SendNotification(long targetAuthenticatedId, long notificationType, long objectId) => this.GeneratedNotifications.Add(new Notification(targetAuthenticatedId, notificationType, objectId));

		private void AddSerializationDefinition<T>(Action<BinaryWriter, ParameterNode, T> serializer, Func<BinaryReader, ParameterNode, T> deserializer) => this.serializationDefinitions.Add(typeof(T), new SerializationDefinition<T> { Serializer = serializer, Deserializer = deserializer });

		protected MessageHandler(object request, object response) {
			this.Request = request;
			this.Response = response;

			this.GeneratedNotifications = new List<Notification>();

			this.AddSerializationDefinitions();

			this.inputProperties = this.CreateTree(this.Request.GetType());
			this.outputProperties = this.CreateTree(this.Response.GetType());

			this.validationProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(ValidationAttribute)))
				.Select(p => new ValidationProperty() { Property = p, Attributes = p.GetCustomAttributes<ValidationAttribute>().ToList() })
				.ToList();
		}

		[SuppressMessage("Microsoft.Maintainability", "CA1502")]
		private void AddSerializationDefinitions() {
			this.serializationDefinitions = new Dictionary<Type, ISerializationDefinition>();

			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadString());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadBoolean());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadByte());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadSByte());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadUInt16());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadInt16());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadUInt32());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadInt32());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadUInt64());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadInt64());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadSingle());
			this.AddSerializationDefinition((w, p, o) => w.Write(o), (r, p) => r.ReadDouble());
			this.AddSerializationDefinition((w, p, o) => this.SerializeList(w, p, o), (r, p) => this.DeserializeList(r, p));
			this.AddSerializationDefinition((w, p, o) => w.Write((ulong)((o - MessageHandler.DateTimeEpoch).TotalMilliseconds)), (r, p) => MessageHandler.DateTimeEpoch.AddMilliseconds(r.ReadUInt64()));
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

		protected long BindResponse(object source, MessageParameterDirection direction) {
			if (source == null)
				return ResponseCode.ObjectNotFound;

			if (this.boundProperties == null)
				this.boundProperties = MessageHandler.GetPropertiesToBind(source.GetType(), direction == MessageParameterDirection.Input ? this.inputProperties : this.outputProperties);

			foreach (var p in this.boundProperties)
				p.Parameter.Property.SetValue(this, Convert.ChangeType(p.Property.GetValue(source), p.Property.PropertyType, CultureInfo.InvariantCulture));

			return ResponseCode.Success;
		}

		internal static List<BoundProperty> GetPropertiesToBind(Type type, List<ParameterNode> targetProperties) {
			var sourceProperties = type.GetProperties();

			return targetProperties
				.Where(p => sourceProperties.Any(s => s.Name == p.Property.Name))
				.Select(p => new BoundProperty() { Parameter = p, Property = sourceProperties.SingleOrDefault(b => b.Name == p.Property.Name) })
				.ToList();
		}

		internal static List<PropertyInfo> GetProperties(Type type) {
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(MessageParameterAttribute)))
				.OrderBy(p => p.GetCustomAttribute<MessageParameterAttribute>().Index)
				.ToList();
		}

		internal List<ParameterNode> CreateTree(Type type) {
			return MessageHandler.GetProperties(type).Select(p => this.CreateTree(p)).ToList();
		}

		private ParameterNode CreateTree(PropertyInfo property) {
			var node = new ParameterNode() { Property = property };
			var iface = property.PropertyType.GetInterfaces().SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));

			if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>))
				iface = property.PropertyType;

			if (iface != null) {
				node.ListGenericType = iface.GenericTypeArguments.Single();
				node.SerializationDefinition = this.serializationDefinitions[typeof(IList)];
				node.Children = MessageHandler.GetProperties(node.ListGenericType).Select(p => this.CreateTree(p)).ToList();

				if (!node.Children.Any())
					node.ListMemberSerializationDefinition = this.serializationDefinitions[node.ListGenericType];
			}
			else if (property.PropertyType.IsEnum) {
				node.SerializationDefinition = this.serializationDefinitions[Enum.GetUnderlyingType(node.Property.PropertyType)];
			}
			else {
				node.SerializationDefinition = this.serializationDefinitions[node.Property.PropertyType];
				node.Children = MessageHandler.GetProperties(property.PropertyType).Select(p => this.CreateTree(p)).ToList();
			}

			return node;
		}

		private void Serialize(BinaryWriter writer, ParameterNode node, object obj) {
			var value = node.Property.GetValue(obj);

			if (node.SerializationDefinition != null) {
				node.SerializationDefinition.Serialize(writer, node, value);
			}
			else {
				node.Children.ForEach(f => this.Serialize(writer, f, value));
			}
		}

		private void Deserialize(BinaryReader reader, ParameterNode node, object obj) {
			if (node.SerializationDefinition != null) {
				node.Property.SetValue(obj, node.SerializationDefinition.Deserialize(reader, node));
			}
			else {
				node.Children.ForEach(f => this.Deserialize(reader, f, node.Property.GetValue(obj)));
			}
		}

		private void SerializeList(BinaryWriter writer, ParameterNode node, IList collection) {
			writer.Write((ushort)collection.Count);

			if (collection.Count == 0)
				return;

			for (var i = 0; i < collection.Count; i++) {
				if (node.Children.Any()) {
					node.Children.ForEach(f => this.Serialize(writer, f, collection[i]));
				}
				else {
					node.ListMemberSerializationDefinition.Serialize(writer, node, collection[i]);
				}
			}

			collection.Clear();
		}

		private IList DeserializeList(BinaryReader reader, ParameterNode node) {
			var count = reader.ReadUInt16();
			var collectionConstructor = node.Property.PropertyType.GetConstructor(!node.Property.PropertyType.IsArray ? Type.EmptyTypes : new Type[] { typeof(int) });
			var collection = (IList)collectionConstructor.Invoke(!node.Property.PropertyType.IsArray ? null : new object[] { count });
			var adder = !node.Property.PropertyType.IsArray ? (Action<object, int>)((o, i) => collection.Add(o)) : (o, i) => collection[i] = o;

			if (node.Children.Any()) {
				var objectConstructor = node.ListGenericType.GetConstructor(Type.EmptyTypes);

				for (var i = 0; i < count; i++) {
					var newObject = objectConstructor.Invoke(null);

					node.Children.ForEach(f => this.Deserialize(reader, f, newObject));

					adder(newObject, i);
				}
			}
			else {
				for (var i = 0; i < count; i++) {
					adder(node.ListMemberSerializationDefinition.Deserialize(reader, node), i);
				}
			}

			return collection;
		}
	}

	[SuppressMessage("Microsoft.Design", "CA1005")]
	public abstract class MessageHandler<TContext, TRequest, TResponse> : MessageHandler where TContext : MessageContext where TRequest : new() where TResponse : new() {
		protected MessageHandler() : base(new TRequest(), new TResponse()) {

		}

		public new TContext Context {
			get {
				return (TContext)base.Context;
			}
			set {
				base.Context = value;
			}
		}

		public new TRequest Request {
			get {
				return (TRequest)base.Request;
			}
			set {
				base.Request = value;
			}
		}

		public new TResponse Response {
			get {
				return (TResponse)base.Response;
			}
			set {
				base.Response = value;
			}
		}
	}

	public class ListInput {
		[MessageParameter(-4)]
		[AtLeast(0)]
		public int Skip { get; set; }

		[MessageParameter(-3)]
		[AtLeast(0)]
		public int Take { get; set; }

		[MessageParameter(-2)]
		[ApiString(false, 1)]
		public string OrderByField { get; set; }

		[MessageParameter(-1)]
		public bool OrderByAscending { get; set; }
	}

	public class ListOutput<TEntry> where TEntry : new() {
		[MessageParameter(-1)]
		public IList<TEntry> List { get; internal set; }
	}

	[SuppressMessage("Microsoft.Design", "CA1005")]
	public abstract class ListMessageHandler<TContext, TRequest, TResponse, TEntry> : MessageHandler<TContext, TRequest, TResponse> where TContext : MessageContext where TEntry : new() where TRequest : ListInput, new() where TResponse : ListOutput<TEntry>, new() {
		private List<BoundProperty> boundProperties;

		protected long SetResponseFromQuery(IQueryable<TEntry> query) {
			if (query == null) throw new ArgumentNullException(nameof(query));

			var parameter = Expression.Parameter(typeof(TEntry));
			var property = Expression.Property(parameter, this.Request.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.Request.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(TEntry), property.Type }, query.Expression, quote);

			this.Response.List = query.Provider.CreateQuery<TEntry>(call).Skip(this.Request.Skip).Take(this.Request.Take).ToList();

			return ResponseCode.Success;
		}

		protected long BindResponseFromQuery<T>(IQueryable<T> query) {
			if (query == null) throw new ArgumentNullException(nameof(query));

			var result = new List<TEntry>();
			var parameter = Expression.Parameter(typeof(T));
			var property = Expression.Property(parameter, this.Request.OrderByField);
			var sort = Expression.Lambda(property, parameter);
			var quote = Expression.Quote(sort);
			var call = Expression.Call(typeof(Queryable), this.Request.OrderByAscending ? "OrderBy" : "OrderByDescending", new[] { typeof(T), property.Type }, query.Expression, quote);

			if (this.boundProperties == null)
				this.boundProperties = MessageHandler.GetPropertiesToBind(typeof(T), this.CreateTree(typeof(TEntry)));

			foreach (var sourceEntry in query.Provider.CreateQuery<T>(call).Skip(this.Request.Skip).Take(this.Request.Take)) {
				var resultEntry = new TEntry();

				foreach (var p in this.boundProperties)
					p.Parameter.Property.SetValue(resultEntry, Convert.ChangeType(p.Property.GetValue(sourceEntry), p.Property.PropertyType, CultureInfo.InvariantCulture));

				result.Add(resultEntry);
			}

			this.Response.List = result;

			return ResponseCode.Success;
		}
	}
}