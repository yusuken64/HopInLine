using System.Text;

namespace HopInLine.Data.Line
{
	public class ParticipantFactory
	{

		private static readonly char[] _characters =
			"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

		public static string NewParticipantID(int length = 11)
		{
			var result = new StringBuilder(length);
			for (int i = 0; i < length; i++)
			{
				result.Append(_characters[Random.Shared.Next(_characters.Length)]);
			}
			return result.ToString();
		}

		public static string GenerateUniqueColor()
		{
			int hue = Random.Shared.Next(360); // Random hue between 0 and 360
			double saturation = 0.7;    // Fixed saturation for vibrancy (70%)
			double lightness = 0.7;     // Fixed lightness for brightness (70%)

			// Convert HSL to RGB
			(int r, int g, int b) = HslToRgb(hue, saturation, lightness);

			// Return the color in hex format
			return $"#{r:X2}{g:X2}{b:X2}";
		}

		private static (int r, int g, int b) HslToRgb(int hue, double saturation, double lightness)
		{
			double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
			double x = chroma * (1 - Math.Abs((hue / 60.0) % 2 - 1));
			double m = lightness - chroma / 2;

			double r1, g1, b1;
			if (hue < 60)
			{
				r1 = chroma; g1 = x; b1 = 0;
			}
			else if (hue < 120)
			{
				r1 = x; g1 = chroma; b1 = 0;
			}
			else if (hue < 180)
			{
				r1 = 0; g1 = chroma; b1 = x;
			}
			else if (hue < 240)
			{
				r1 = 0; g1 = x; b1 = chroma;
			}
			else if (hue < 300)
			{
				r1 = x; g1 = 0; b1 = chroma;
			}
			else
			{
				r1 = chroma; g1 = 0; b1 = x;
			}

			int r = (int)((r1 + m) * 255);
			int g = (int)((g1 + m) * 255);
			int b = (int)((b1 + m) * 255);

			return (r, g, b);
		}

		public static Participant Create(string name = "Waiter")
		{
            string newId = NewParticipantID();
            return new Participant()
			{
				Name = name,
				Id = newId,
				Color = GenerateUniqueColor()
			};
		}

		public static string GetForegroundColor(string backgroundHex)
		{
			// Parse the hex color string, tolerate missing #
			if (string.IsNullOrWhiteSpace(backgroundHex))
			{
				return "#000000";
			}

			string hex = backgroundHex.StartsWith("#") ? backgroundHex : "#" + backgroundHex;

			// Parse hex to RGB
			if (!int.TryParse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) ||
			    !int.TryParse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) ||
			    !int.TryParse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
			{
				return "#000000";
			}

			// Convert RGB to 0-1 values
			double rLinear = r / 255.0;
			double gLinear = g / 255.0;
			double bLinear = b / 255.0;

			// Linearise each channel using WCAG formula
			rLinear = rLinear <= 0.03928 ? rLinear / 12.92 : Math.Pow((rLinear + 0.055) / 1.055, 2.4);
			gLinear = gLinear <= 0.03928 ? gLinear / 12.92 : Math.Pow((gLinear + 0.055) / 1.055, 2.4);
			bLinear = bLinear <= 0.03928 ? bLinear / 12.92 : Math.Pow((bLinear + 0.055) / 1.055, 2.4);

			// Compute relative luminance
			double luminance = 0.2126 * rLinear + 0.7152 * gLinear + 0.0722 * bLinear;

			// Return black text for light backgrounds (luminance > 0.179), white for dark
			return luminance > 0.179 ? "#000000" : "#FFFFFF";
		}
	}
}
