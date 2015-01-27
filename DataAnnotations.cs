using System;
using System.ComponentModel.DataAnnotations;

namespace ArkeIndustries.RequestServer.DataAnnotations {
	public class ApiStringAttribute : ValidationAttribute {
		public int MinLength { get; set; } = 5;
		public int MaxLength { get; set; } = 100;
		public bool AllowWhiteSpace { get; set; } = false;

		public override bool IsValid(object value) {
			var str = (string)value;

			return str.Length >= this.MinLength && str.Length <= this.MaxLength && (this.AllowWhiteSpace || !string.IsNullOrWhiteSpace(str));
		}
	}

	public class AtLeastAttribute : ValidationAttribute {
		public long Value { get; set; } = 0;
		public bool Inclusive { get; set; } = true;

		public AtLeastAttribute(long value) {
			this.Value = value;
		}

		public override bool IsValid(object value) {
			return this.Inclusive ? (long)value >= this.Value : (long)value > this.Value;
		}
	}

	public class ObjectIdAttribute : ValidationAttribute {
		public bool Optional { get; set; } = false;

		public override bool IsValid(object value) {
			return this.Optional ? (long)value >= 0 : (long)value > 0;
		}
	}
}