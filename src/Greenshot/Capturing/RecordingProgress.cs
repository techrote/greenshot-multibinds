using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Greenshot.Capturing
{
    public sealed class RecordingProgress
    {
        private readonly object _gate = new object();
        private readonly Queue<double> _times = new Queue<double>();
        private Stopwatch _clock;
        private double? _stopped;
        private int _captured, _saved;
        public void Begin(Stopwatch clock) { lock (_gate) { _clock = clock; _stopped = null; _captured = _saved = 0; _times.Clear(); } }
        public void Captured(double milliseconds) { lock (_gate) { _captured++; _times.Enqueue(milliseconds); Trim(milliseconds); } }
        public void Saved() { lock (_gate) _saved++; }
        public void CaptureStopped(double seconds) { lock (_gate) _stopped = seconds; }
        private void Trim(double now) { while (_times.Count > 0 && _times.Peek() < now - 1000) _times.Dequeue(); }
        public RecordingProgressSnapshot Snapshot()
        {
            lock (_gate)
            {
                double elapsed = _stopped ?? _clock?.Elapsed.TotalSeconds ?? 0;
                Trim(elapsed * 1000);
                double[] times = _times.ToArray();
                double fps = times.Length < 2 ? 0 : (times.Length - 1) * 1000 / Math.Max(0.001, times[times.Length - 1] - times[0]);
                return new RecordingProgressSnapshot(TimeSpan.FromSeconds(elapsed), fps, _captured, _saved, _stopped.HasValue);
            }
        }
    }
    public sealed class RecordingProgressSnapshot
    {
        public RecordingProgressSnapshot(TimeSpan elapsed, double fps, int captured, int saved, bool stopped)
        { Elapsed = elapsed; Fps = fps; Captured = captured; Saved = saved; CaptureStopped = stopped; }
        public TimeSpan Elapsed { get; }
        public double Fps { get; }
        public int Captured { get; }
        public int Saved { get; }
        public bool CaptureStopped { get; }
    }
}
