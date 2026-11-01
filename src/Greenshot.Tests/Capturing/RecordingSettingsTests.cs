using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Greenshot.Base.Core;
using Greenshot.Capturing;
using Greenshot.Settings.ViewModels;
using Xunit;

namespace Greenshot.Tests.Capturing
{
    public class RecordingSettingsTests
    {
        public RecordingSettingsTests() { TestEnvironment.EnsureInitialized(); }
        [Theory]
        [InlineData(10, 10)] [InlineData(120, 120)] [InlineData(10000, 120)] [InlineData(0, 10)] [InlineData(-1, 10)]
        public void UniversalLimitIsAlwaysPositiveAndCapped(double value, double expected)
            => Assert.Equal(TimeSpan.FromMinutes(expected), RecordingNames.HardLimit(value));

        [Fact]
        public void DefaultsUseF6ToF9AndTenMinuteUniversalLimit()
        {
            var config = NewConfig();
            Assert.Equal("F6", config.RecordingStartHotkey);
            Assert.Equal("F7", config.RecordingSelectHotkey);
            Assert.Equal("F8", config.RecordingBorderHotkey);
            Assert.Equal("F9", config.RecordingTimerHotkey);
            Assert.Equal(10, config.RecordingMaximumMinutes);
            Assert.False(config.RecordingTimeLimitEnabled);
            Assert.True(config.RecordingCreateFolder);
        }

        [Fact]
        public void DraftSettingsAreValidatedAndOnlyWrittenOnSave()
        {
            var config = NewConfig();
            var draft = new RecordingSettingsViewModel(config) { Fps = "60", MaximumMinutes = "121" };
            Assert.NotNull(draft.Validate());
            Assert.Equal(30, config.RecordingFramesPerSecond);
            draft.MaximumMinutes = "120";
            draft.TimeLimit = "4.12:00:00";
            draft.CreateZip = true;
            draft.OpenCompletedOutput = false;
            Assert.Null(draft.Validate());
            draft.Save(config);
            Assert.Equal(60, config.RecordingFramesPerSecond);
            Assert.Equal(120, config.RecordingMaximumMinutes);
            Assert.True(config.RecordingCreateZip);
            Assert.False(config.RecordingOpenCompletedOutput);
            Assert.Equal(TimeSpan.FromHours(108), RecordingNames.ParseLimit(config.RecordingTimeLimit));
            draft.FrameTemplate = "{unknown}";
            Assert.NotNull(draft.Validate());
            draft.FrameTemplate = "{frame}";
            draft.SelectKey = draft.ToggleKey;
            Assert.NotNull(draft.Validate());
        }

        [Fact]
        public void TemplatesComposeTimesWindowDurationBatchAndNominalTimecode()
        {
            var names = new RecordingNames { Started = new DateTime(2026, 11, 1, 13, 2, 3), Batch = 8, Fps = 30, Window = "Test/Window:1", Duration = 2.5, Count = 75 };
            Assert.Equal("2026-11-01_13-02-03_008_Test_Window_1_30_75_2.50", names.Render("{date}_{time:HH-mm-ss}_{batch:000}_{window}_{fps}_{count}_{duration:0.00}"));
            Assert.Equal("00-00-01-00_00031_1.250_13-02-04-250", names.Render("{timecode}_{frame:00000}_{duration:0.000}_{timestamp:HH-mm-ss-fff}", 31, 1.25));
            Assert.Throws<FormatException>(() => names.Render("{bogus}"));
            Assert.Equal("_CON", names.Render("CON"));
        }

        [Theory]
        [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
        public void AllFolderZipCombinationsPreserveOutputWithoutOverwriting(bool folder, bool zip)
        {
            string root = NewFolder();
            Directory.CreateDirectory(root);
            string working = Path.Combine(root, ".recording-test");
            Directory.CreateDirectory(working);
            File.WriteAllText(Path.Combine(working, "image.png"), "lossless bytes");
            File.WriteAllText(Path.Combine(working, "frames.csv"), "index");
            File.WriteAllText(Path.Combine(root, "image.png"), "existing");
            string result = RecordingOutput.Finish(working, root, "batch", folder, zip, 1);
            Assert.Equal("existing", File.ReadAllText(Path.Combine(root, "image.png")));
            if (zip)
            {
                using var archive = ZipFile.OpenRead(Path.Combine(root, "batch.zip"));
                using var text = new StreamReader(archive.GetEntry("image.png").Open());
                Assert.Equal("lossless bytes", text.ReadToEnd());
            }
            if (folder) Assert.Equal("lossless bytes", File.ReadAllText(Path.Combine(result, "image.png")));
            else
            {
                Assert.False(Directory.Exists(working));
                if (!zip) { Assert.Equal("lossless bytes", File.ReadAllText(Path.Combine(root, "image_2.png"))); Assert.True(File.Exists(Path.Combine(root, "batch0001_frames.csv"))); }
            }
        }

        [Fact]
        public async Task UniversalLimitStopsCaptureWithoutUserCancellation()
        {
            var names = new RecordingNames();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            int frames = await PngFrameRecorder.RecordAsync(new Rectangle(0, 0, 8, 8), NewFolder(), 60, timeout.Token,
                capture: () => new Bitmap(8, 8), names: names, hardLimit: TimeSpan.FromMilliseconds(250));
            Assert.True(frames > 0);
            Assert.InRange(names.Duration, 0.15, 1.5);
            Assert.False(timeout.IsCancellationRequested);
        }

        [Fact]
        public async Task OptionalLimitCanBeToggledDuringRecording()
        {
            var names = new RecordingNames();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            int calls = 0;
            int frames = await PngFrameRecorder.RecordAsync(new Rectangle(0, 0, 8, 8), NewFolder(), 60, timeout.Token,
                capture: () => new Bitmap(8, 8), names: names, hardLimit: TimeSpan.FromSeconds(2),
                optionalLimit: () => Interlocked.Increment(ref calls) < 15 ? null : (TimeSpan?)TimeSpan.FromMilliseconds(50));
            Assert.InRange(frames, 8, 20);
            Assert.InRange(names.Duration, 0.1, 1.5);
            Assert.False(timeout.IsCancellationRequested);
        }

        [Fact]
        public async Task RepeatingImageTemplateNeverOverwritesFrames()
        {
            string folder = NewFolder();
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            int count = await PngFrameRecorder.RecordAsync(new Rectangle(0, 0, 8, 8), folder, 60, stop.Token, capture: () => new Bitmap(8, 8), frameTemplate: "same");
            Assert.True(count > 1);
            Assert.Equal(count, Directory.GetFiles(folder, "*.png").Length);
        }
        private static CoreConfigurationImpl NewConfig() { var config = new CoreConfigurationImpl(); config.ResetToDefaults(); return config; }
        private static string NewFolder() => Path.Combine(Path.GetTempPath(), "GreenshotRecordingSettingsTest-" + Guid.NewGuid().ToString("N"));
    }
}
