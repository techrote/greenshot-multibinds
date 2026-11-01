using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Greenshot.Base.Core;
using Greenshot.Capturing;
using Xunit;

namespace Greenshot.Tests.Capturing
{
    public class PngFrameRecorderTests
    {
        private readonly ITestOutputHelper _output;
        public PngFrameRecorderTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task SavesEveryAcceptedFrameLosslesslyAndDrainsOnStop()
        {
            string folder = NewFolder();
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(1200));
            int captured = 0;
            int count = await PngFrameRecorder.RecordAsync(new Rectangle(-120, -50, 32, 24), folder, 30, stop.Token, () =>
            {
                var bitmap = new Bitmap(32, 24);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.FromArgb(++captured, 33, 77));
                return bitmap;
            });
            Assert.Equal(captured, count);
            Assert.True(count >= 20);
            Assert.Equal(count, Directory.GetFiles(folder, "*.png").Length);
            for (int i = 1; i <= count; i++)
            {
                using var frame = new Bitmap(Path.Combine(folder, "frame_" + i.ToString("D8") + ".png"));
                Assert.Equal(32, frame.Width);
                Assert.Equal(24, frame.Height);
                Assert.Equal(Color.FromArgb(i, 33, 77).ToArgb(), frame.GetPixel(16, 12).ToArgb());
            }
            Assert.Equal(count + 1, File.ReadAllLines(Path.Combine(folder, "frames.csv")).Length);
            Assert.Contains("Status: Complete", File.ReadAllText(Path.Combine(folder, "recording.txt")));
        }

        [Fact]
        public async Task CaptureFailureStillSavesQueuedFramesAndReportsError()
        {
            string folder = NewFolder();
            int captured = 0;
            await Assert.ThrowsAsync<IOException>(() => PngFrameRecorder.RecordAsync(new Rectangle(0, 0, 32, 24), folder, 120, CancellationToken.None, () =>
            {
                if (++captured == 5) throw new IOException("Simulated capture failure");
                return new Bitmap(32, 24);
            }));
            Assert.Equal(4, Directory.GetFiles(folder, "*.png").Length);
            Assert.Contains("Simulated capture failure", File.ReadAllText(Path.Combine(folder, "recording.txt")));
        }

        [Fact]
        public void NumberRowShortcutsParseCorrectly()
        {
            Assert.Equal(Dapplo.Windows.Input.Enums.VirtualKeyCode.Key9, HotkeySequence.Parse("Ctrl + Alt + 9").Chords[0].Key);
            Assert.Equal(Dapplo.Windows.Input.Enums.VirtualKeyCode.Key7, HotkeySequence.Parse("Ctrl + Alt + 7").Chords[0].Key);
        }

        [Fact]
        public async Task DesktopBenchmarkRecordsAtMoreThanTwentyFps()
        {
            string folder = NewFolder();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            int count = await PngFrameRecorder.RecordAsync(new Rectangle(0, 0, 160, 100), folder, 30, stop.Token);
            var times = File.ReadAllLines(Path.Combine(folder, "frames.csv")).Skip(1)
                .Select(line => double.Parse(line.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
            double rate = (count - 1) * 1000.0 / (times.Last() - times.First());
            _output.WriteLine("160x100 desktop benchmark: " + count + " PNG frames, " + rate.ToString("F2") + " fps; " + folder);
            Assert.True(rate > 20, "Actual capture rate: " + rate);
        }

        private static string NewFolder() => Path.Combine(Path.GetTempPath(), "GreenshotRecorderTest-" + Guid.NewGuid().ToString("N"));
    }
}
