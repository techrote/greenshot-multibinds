using System;
using System.ComponentModel;
using System.Globalization;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Capturing;

namespace Greenshot.Settings.ViewModels
{
    public sealed class RecordingSettingsViewModel : INotifyPropertyChanged
    {
        private string _fps, _maximum, _limit, _root, _container, _frame, _toggleKey, _selectKey, _borderKey, _timerKey;
        public RecordingSettingsViewModel(ICoreConfiguration config)
        {
            _fps = config.RecordingFramesPerSecond.ToString(CultureInfo.InvariantCulture);
            _maximum = config.RecordingMaximumMinutes.ToString(CultureInfo.InvariantCulture);
            _limit = config.RecordingTimeLimit;
            _root = config.RecordingOutputDirectory;
            _container = config.RecordingContainerTemplate;
            _frame = config.RecordingFrameTemplate;
            _toggleKey = config.RecordingStartHotkey;
            _selectKey = config.RecordingSelectHotkey;
            _borderKey = config.RecordingBorderHotkey;
            _timerKey = config.RecordingTimerHotkey;
            TimerEnabled = config.RecordingTimeLimitEnabled;
            CreateFolder = config.RecordingCreateFolder;
            CreateZip = config.RecordingCreateZip;
            ShowBorder = config.RecordingShowBorder;
        }
        public string Fps { get => _fps; set { _fps = value; Changed(); } }
        public string MaximumMinutes { get => _maximum; set { _maximum = value; Changed(); } }
        public string TimeLimit { get => _limit; set { _limit = value; Changed(); } }
        public string OutputDirectory { get => _root; set { _root = value; Changed(); } }
        public string ContainerTemplate { get => _container; set { _container = value; Changed(); } }
        public string FrameTemplate { get => _frame; set { _frame = value; Changed(); } }
        public string ToggleKey { get => _toggleKey; set { _toggleKey = value; Changed(); } }
        public string SelectKey { get => _selectKey; set { _selectKey = value; Changed(); } }
        public string BorderKey { get => _borderKey; set { _borderKey = value; Changed(); } }
        public string TimerKey { get => _timerKey; set { _timerKey = value; Changed(); } }
        public bool TimerEnabled { get; set; }
        public bool CreateFolder { get; set; }
        public bool CreateZip { get; set; }
        public bool ShowBorder { get; set; }
        private RecordingNames Example => new RecordingNames { Started = new DateTime(2026, 11, 1, 14, 5, 6, 123), Batch = 12, Fps = int.TryParse(Fps, out int fps) && fps > 0 ? fps : 30, Window = "Cybersand", Duration = 12.345, Count = 370 };
        public string ContainerPreview { get { try { return Example.Render(ContainerTemplate); } catch (Exception ex) { return ex.Message; } } }
        public string FramePreview { get { try { return Example.Render(FrameTemplate, 31, 1.025) + ".png"; } catch (Exception ex) { return ex.Message; } } }
        public string Validate()
        {
            if (!int.TryParse(Fps, out int fps) || fps < 1 || fps > 120) return "Target FPS must be an integer between 1 and 120.";
            if (!double.TryParse(MaximumMinutes, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) || double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes <= 0 || minutes > 120)
                return "The universal limit must be greater than 0 and no more than 120 minutes.";
            try { RecordingNames.ParseLimit(TimeLimit); Example.Render(ContainerTemplate); Example.Render(FrameTemplate, 1); }
            catch (Exception ex) { return ex.Message; }
            if (!string.IsNullOrWhiteSpace(OutputDirectory))
            {
                try { System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(OutputDirectory)); }
                catch (Exception ex) { return "Output directory: " + ex.Message; }
            }
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string text in new[] { ToggleKey, SelectKey, BorderKey, TimerKey })
            {
                var key = HotkeySequence.Parse(text);
                if (!key.Validate(out string reason)) return "Recording shortcut: " + reason;
                if (!keys.Add(key.ToString())) return "Recording shortcuts must be different from each other.";
            }
            return null;
        }
        public void Save(ICoreConfiguration config)
        {
            string error = Validate();
            if (error != null) throw new FormatException(error);
            void Set(string key, Action write) { if (!config.IsConstant(key)) write(); }
            Set(nameof(config.RecordingFramesPerSecond), () => config.RecordingFramesPerSecond = int.Parse(Fps));
            Set(nameof(config.RecordingMaximumMinutes), () => config.RecordingMaximumMinutes = double.Parse(MaximumMinutes, CultureInfo.InvariantCulture));
            Set(nameof(config.RecordingTimeLimit), () => config.RecordingTimeLimit = RecordingNames.ParseLimit(TimeLimit).ToString("c", CultureInfo.InvariantCulture));
            Set(nameof(config.RecordingTimeLimitEnabled), () => config.RecordingTimeLimitEnabled = TimerEnabled);
            Set(nameof(config.RecordingOutputDirectory), () => config.RecordingOutputDirectory = OutputDirectory?.Trim());
            Set(nameof(config.RecordingCreateFolder), () => config.RecordingCreateFolder = CreateFolder);
            Set(nameof(config.RecordingCreateZip), () => config.RecordingCreateZip = CreateZip);
            Set(nameof(config.RecordingShowBorder), () => config.RecordingShowBorder = ShowBorder);
            Set(nameof(config.RecordingContainerTemplate), () => config.RecordingContainerTemplate = ContainerTemplate);
            Set(nameof(config.RecordingFrameTemplate), () => config.RecordingFrameTemplate = FrameTemplate);
            Set(nameof(config.RecordingStartHotkey), () => config.RecordingStartHotkey = ToggleKey);
            Set(nameof(config.RecordingSelectHotkey), () => config.RecordingSelectHotkey = SelectKey);
            Set(nameof(config.RecordingBorderHotkey), () => config.RecordingBorderHotkey = BorderKey);
            Set(nameof(config.RecordingTimerHotkey), () => config.RecordingTimerHotkey = TimerKey);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContainerPreview)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FramePreview)));
        }
    }
}
