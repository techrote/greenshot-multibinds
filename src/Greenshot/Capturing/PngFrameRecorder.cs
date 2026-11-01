using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Greenshot.Capturing
{
    /// <summary>Bounded, lossless frame sequence writer. Stop drains every accepted frame.</summary>
    public static class PngFrameRecorder
    {
        public static Task<int> RecordAsync(Rectangle region, string folder, int fps, CancellationToken stop,
            Func<Bitmap> capture = null, RecordingNames names = null, string frameTemplate = "frame_{frame:00000000}",
            TimeSpan? hardLimit = null, Func<TimeSpan?> optionalLimit = null, RecordingProgress progress = null)
        {
            if (region.Width <= 0 || region.Height <= 0) throw new ArgumentOutOfRangeException(nameof(region));
            if (fps < 1 || fps > 120) throw new ArgumentOutOfRangeException(nameof(fps));
            Directory.CreateDirectory(folder);
            int capacity = (int)Math.Max(1, Math.Min(120, 128L * 1024 * 1024 / ((long)region.Width * region.Height * 4)));
            return RunAsync(region, folder, fps, capacity, stop, capture ?? (() => Capture(region)), names ?? new RecordingNames { Fps = fps }, frameTemplate,
                hardLimit.HasValue ? RecordingNames.HardLimit(hardLimit.Value.TotalMinutes) : TimeSpan.FromMinutes(10), optionalLimit, progress);
        }

        private static async Task<int> RunAsync(Rectangle region, string folder, int fps, int capacity,
            CancellationToken stop, Func<Bitmap> capture, RecordingNames names, string frameTemplate, TimeSpan hardLimit, Func<TimeSpan?> optionalLimit, RecordingProgress progress)
        {
            using var queue = new BlockingCollection<Frame>(capacity);
            using var failed = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, failed.Token);
            var watch = Stopwatch.StartNew();
            progress?.Begin(watch);
            DateTime started = DateTime.UtcNow;
            double capturedDuration = 0;
            int saved = 0;
            Exception failure = null;
            var writer = Task.Factory.StartNew(() =>
            {
                try
                {
                    using var index = new StreamWriter(Path.Combine(folder, "frames.csv"));
                    index.WriteLine("file,elapsed_ms");
                    foreach (var frame in queue.GetConsumingEnumerable())
                    {
                        using (frame.Image)
                        {
                            int number = saved + 1;
                            string basename = names.Render(frameTemplate, number, frame.Milliseconds / 1000.0);
                            string candidate = Path.Combine(folder, basename + ".png");
                            string path = File.Exists(candidate) || Directory.Exists(candidate)
                                ? RecordingOutput.UniquePath(folder, basename + "_" + number.ToString("D8", CultureInfo.InvariantCulture), ".png") : candidate;
                            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) frame.Image.Save(stream, ImageFormat.Png);
                            saved++;
                            progress?.Saved();
                            string name = Path.GetFileName(path);
                            index.WriteLine("\"" + name.Replace("\"", "\"\"") + "\"," + frame.Milliseconds.ToString("F3", CultureInfo.InvariantCulture));
                        }
                    }
                }
                catch
                {
                    failed.Cancel();
                    throw;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var producer = Task.Factory.StartNew(() =>
            {
                try
                {
                    double due = 0;
                    while (!linked.IsCancellationRequested)
                    {
                        TimeSpan limit = hardLimit;
                        var optional = optionalLimit?.Invoke();
                        if (optional.HasValue && optional.Value < limit) limit = optional.Value;
                        if (watch.Elapsed >= limit) break;
                        double wait = Math.Min(due - watch.Elapsed.TotalMilliseconds, (limit - watch.Elapsed).TotalMilliseconds);
                        // R1 exception: this dedicated LongRunning capture thread waits between frames, never the UI or a pool thread.
#pragma warning disable RS0030
                        if (wait > 0 && linked.Token.WaitHandle.WaitOne((int)Math.Ceiling(wait))) break;
#pragma warning restore RS0030
                        if (linked.IsCancellationRequested || watch.Elapsed >= limit) break;
                        double timestamp = watch.Elapsed.TotalMilliseconds;
                        var bitmap = capture();
                        if (!queue.TryAdd(new Frame(bitmap, timestamp)))
                        {
                            bitmap.Dispose();
                            throw new IOException("PNG storage could not keep up; recording stopped. All queued frames were saved.");
                        }
                        progress?.Captured(timestamp);
                        // No bursts of duplicate frames to catch up after a slow capture.
                        due = Math.Max(due + 1000.0 / fps, watch.Elapsed.TotalMilliseconds);
                    }
                }
                finally { capturedDuration = watch.Elapsed.TotalSeconds; progress?.CaptureStopped(capturedDuration); queue.CompleteAdding(); }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try { await Task.WhenAll(producer, writer).ConfigureAwait(false); }
            catch (Exception ex) { failure = ex; }
            finally
            {
                while (queue.TryTake(out var frame)) frame.Image.Dispose();
                names.Count = saved;
                names.Duration = capturedDuration;
                File.WriteAllText(Path.Combine(folder, "recording.txt"),
                    "Started UTC: " + started.ToString("O") + Environment.NewLine +
                    "Region (physical screen pixels): " + region + Environment.NewLine +
                    "Target fps: " + fps + Environment.NewLine + "Saved frames: " + saved + Environment.NewLine +
                    "Capture duration seconds: " + capturedDuration.ToString("F3", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "Status: " + (failure?.Message ?? "Complete") + Environment.NewLine +
                    "Actual capture times are in frames.csv. PNG is lossless RGB; audio and cursor are not recorded.");
            }
            if (failure != null) throw new IOException("Recording failed: " + failure.Message + " Frames already saved are in " + folder, failure);
            return saved;
        }

        private static Bitmap Capture(Rectangle region)
        {
            var image = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
            try
            {
                using var graphics = Graphics.FromImage(image);
                graphics.CopyFromScreen(region.Location, Point.Empty, region.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch { image.Dispose(); throw; }
        }

        private sealed class Frame
        {
            public Frame(Bitmap image, double milliseconds) { Image = image; Milliseconds = milliseconds; }
            public Bitmap Image { get; }
            public double Milliseconds { get; }
        }
    }
}
