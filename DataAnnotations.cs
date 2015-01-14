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
}