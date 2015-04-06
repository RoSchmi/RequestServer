using System;

namespace ArkeIndustries.RequestServer {
	[AttributeUsage(AttributeTargets.Property)]
	public abstract class ValidationAttribute : Attribute {
		public abstract long IsValid(object value, MessageContext context);
	}

	[AttributeUsage(AttributeTargets.Property)]
	public abstract class BasicValidationAttribute : ValidationAttribute {
		public override long IsValid(object value, MessageContext context) {
			return this.IsBasicValid(value) ? ResponseCode.Success : ResponseCode.ParameterValidationFailed;
		}

		protected abstract bool IsBasicValid(object value);
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class ApiStringAttribute : BasicValidationAttribute {
		public int MaxLength { get; }
		public int MinLength { get; }
		public bool AllowWhiteSpace { get; }

		public ApiStringAttribute(bool allowWhiteSpace, int minLength) : this(allowWhiteSpace, minLength, 100) {

		}

		public ApiStringAttribute(bool allowWhiteSpace, int minLength, int maxLength) {
			this.MinLength = minLength;
			this.AllowWhiteSpace = allowWhiteSpace;
			this.MaxLength = maxLength;
		}

		protected override bool IsBasicValid(object value) {
			var str = (string)value;

			return str.Length >= this.MinLength && str.Length <= this.MaxLength && (this.AllowWhiteSpace || !string.IsNullOrWhiteSpace(str));
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class AtLeastAttribute : BasicValidationAttribute {
		public long Value { get; }
		public bool Inclusive { get; }

		public AtLeastAttribute(long value) : this(value, true) {

		}

		public AtLeastAttribute(long value, bool inclusive) {
			this.Value = value;
			this.Inclusive = inclusive;
		}

		protected override bool IsBasicValid(object value) {
			if (value == null) throw new ArgumentNullException(nameof(value));

			var t = value.GetType();

			if (t == typeof(sbyte)) {
				return this.Inclusive ? (sbyte)value >= this.Value : (sbyte)value > this.Value;
			}
			else if (t == typeof(byte)) {
				return this.Inclusive ? (byte)value >= this.Value : (byte)value > this.Value;
			}
			else if (t == typeof(short)) {
				return this.Inclusive ? (short)value >= this.Value : (short)value > this.Value;
			}
			else if (t == typeof(ushort)) {
				return this.Inclusive ? (ushort)value >= this.Value : (ushort)value > this.Value;
			}
			else if (t == typeof(int)) {
				return this.Inclusive ? (int)value >= this.Value : (int)value > this.Value;
			}
			else if (t == typeof(uint)) {
				return this.Inclusive ? (uint)value >= this.Value : (uint)value > this.Value;
			}
			else if (t == typeof(long)) {
				return this.Inclusive ? (long)value >= this.Value : (long)value > this.Value;
			}
			else if (t == typeof(ulong)) {
				return this.Inclusive ? (ulong)value >= (ulong)this.Value : (ulong)value > (ulong)this.Value;
			}
			else if (t == typeof(float)) {
				return this.Inclusive ? (float)value >= this.Value : (float)value > this.Value;
			}
			else if (t == typeof(double)) {
				return this.Inclusive ? (double)value >= this.Value : (double)value > this.Value;
			}

			return false;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class ObjectIdAttribute : BasicValidationAttribute {
		public bool Optional { get; }

		public ObjectIdAttribute() : this(false) {

		}

		public ObjectIdAttribute(bool optional) {
			this.Optional = optional;
		}

		protected override bool IsBasicValid(object value) {
			return this.Optional ? (long)value >= 0 : (long)value > 0;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class InEnumAttribute : BasicValidationAttribute {
		public Type EnumType { get; }

		public InEnumAttribute(Type enumType) {
			this.EnumType = enumType;
		}

		protected override bool IsBasicValid(object value) {
			return Enum.IsDefined(this.EnumType, value);
		}
	}
}