using System.Text;

namespace HopInLine.Data.Line
{
	public class ParticipantFactory
	{

		private static readonly char[] _characters =
			"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
		private static readonly Random _random = new Random();

		public static string NewParticipantID(int length = 11)
		{
			var result = new StringBuilder(length);
			for (int i = 0; i < length; i++)
			{
				result.Append(_characters[_random.Next(_characters.Length)]);
			}
			return result.ToString();
		}

		public static string GenerateUniqueColor()
		{
			Random random = new Random();
			int hue = random.Next(360); // Random hue between 0 and 360
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

		public Participant Create(string name = "Waiter")
		{
            string newId = NewParticipantID();
            return new Participant()
			{
				Name = name,
				Id = newId,
				Color = GenerateUniqueColor()
			};
		}
	}
}
