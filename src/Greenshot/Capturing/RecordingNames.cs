using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Greenshot.Capturing
{
    public sealed class RecordingNames
    {
        public DateTime Started { get; set; } = DateTime.Now;
        public int Batch { get; set; }
        public int Fps { get; set; } = 30;
        public string Window { get; set; } = "Desktop";
        public double Duration { get; set; }
        public int Count { get; set; }

        public string Render(string template, int frame = 0, double elapsed = 0)
        {
            if (string.IsNullOrWhiteSpace(template)) throw new FormatException("A recording name template cannot be empty.");
            string result = Regex.Replace(template, @"\{(?<token>[a-zA-Z]+)(?::(?<format>[^{}]+))?\}", match =>
            {
                string token = match.Groups["token"].Value.ToLowerInvariant();
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var culture = CultureInfo.InvariantCulture;
                switch (token)
                {
                    case "date": return Started.ToString(format ?? "yyyy-MM-dd", culture);
                    case "time": return Started.ToString(format ?? "HH-mm-ss-fff", culture);
                    case "timestamp": return Started.AddSeconds(elapsed).ToString(format ?? "yyyy-MM-dd_HH-mm-ss-fff", culture);
                    case "batch": return Batch.ToString(format ?? "0000", culture);
                    case "frame": return frame.ToString(format ?? "00000000", culture);
                    case "fps": return Fps.ToString(format, culture);
                    case "count": return (frame > 0 ? frame : Count).ToString(format, culture);
                    case "duration": return (frame > 0 ? elapsed : Duration).ToString(format ?? "0.000", culture);
                    case "elapsed": return elapsed.ToString(format ?? "0.000", culture);
                    case "window": return Window ?? "Desktop";
                    case "timecode":
                        int n = Math.Max(0, frame - 1), rate = Math.Max(1, Fps);
                        return string.Format(culture, "{0:00}-{1:00}-{2:00}-{3:00}", n / rate / 3600, n / rate / 60 % 60, n / rate % 60, n % rate);
                    default: throw new FormatException("Unknown recording name token: " + token);
                }
            });
            if (result.Contains("{") || result.Contains("}")) throw new FormatException("Unmatched braces in recording name template.");
            foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
            result = result.Trim().TrimEnd('.', ' ');
            if (result.Length == 0 || result == "." || result == "..") throw new FormatException("The name template produces an empty filename.");
            if (Regex.IsMatch(result, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\.)", RegexOptions.IgnoreCase)) result = "_" + result;
            return result.Length <= 160 ? result : result.Substring(0, 160);
        }

        public static TimeSpan HardLimit(double minutes)
        {
            if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes <= 0) minutes = 10;
            return TimeSpan.FromMinutes(Math.Min(minutes, 120));
        }

        public static TimeSpan ParseLimit(string value)
        {
            if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration) || duration <= TimeSpan.Zero)
                throw new FormatException("Enter a positive time limit as [days.]hours:minutes:seconds, for example 00:01:30.500.");
            return duration;
        }
    }
}
