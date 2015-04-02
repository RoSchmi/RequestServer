using System;
using System.ComponentModel.DataAnnotations;

namespace ArkeIndustries.RequestServer {
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class ApiStringAttribute : ValidationAttribute {
		public int MaxLength { get; set; }
		public int MinLength { get; }
		public bool AllowWhiteSpace { get; }

		public ApiStringAttribute(int minLength, bool allowWhiteSpace) {
			this.MinLength = minLength;
			this.AllowWhiteSpace = allowWhiteSpace;
			this.MaxLength = 100;
		}

		public override bool IsValid(object value) {
			var str = (string)value;

			return str.Length >= this.MinLength && str.Length <= this.MaxLength && (this.AllowWhiteSpace || !string.IsNullOrWhiteSpace(str));
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class AtLeastAttribute : ValidationAttribute {
		public long Value { get; } = 0;
		public bool Inclusive { get; set; } = true;

		public AtLeastAttribute(long value) {
			this.Value = value;
		}

		public override bool IsValid(object value) {
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
	public sealed class ObjectIdAttribute : ValidationAttribute {
		public bool Optional { get; set; } = false;

		public override bool IsValid(object value) {
			return this.Optional ? (long)value >= 0 : (long)value > 0;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class InEnumAttribute : ValidationAttribute {
		public Type EnumType { get; }

		public InEnumAttribute(Type enumType) {
			this.EnumType = enumType;
		}

		public override bool IsValid(object value) {
			return Enum.IsDefined(this.EnumType, value);
		}
	}
}