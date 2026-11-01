# Region recording MVP

Press **Ctrl+Alt+9** (top number row), drag the ordinary Greenshot region selector on the live screen, then release to start.
The underlying application keeps updating while selecting. Escape cancels selection. **Ctrl+Alt+7** stops recording. Start is ignored while a recording is selecting, capturing or finishing its writes.

Frames are independent, numbered lossless RGB PNGs in a unique subfolder of **Pictures/Greenshot Recordings**. The folder
opens when all frames have been saved. `frames.csv` gives capture timestamps in milliseconds from recording start;
`recording.txt` records the region in physical screen pixels, target rate, frame count and completion/error status.

The target is 30 fps, subject to capture, CPU and storage speed. The bounded PNG writer queue stops with an error when it
cannot keep up; it drains accepted frames rather than silently dropping them. Slow captures are not filled with duplicate
frames. Inspect timestamps for actual timing. Frames capture the visible desktop region, including overlapping windows.
The MVP records no cursor, audio, HDR metadata, or offscreen/minimized application content. It requires Windows 10 2004+.

The tray menu also provides start/stop and **Open last recording folder**. During capture the tray tooltip says recording.
Normal screenshot freezing and destination choices are unchanged; this workflow always uses live selection and bypasses
the destination picker. Closing Greenshot stops capture and waits for queued frames to finish saving.

Expert settings / `[Core]` in greenshot.ini (edit with Greenshot closed):

```
RecordingStartHotkey=Ctrl + Alt + 9
RecordingStopHotkey=Ctrl + Alt + 7
RecordingFramesPerSecond=30
RecordingOutputDirectory=
```

An empty output directory uses Pictures/Greenshot Recordings. FPS accepts 1-120. Configuration is applied when hotkeys are
restarted or Greenshot is restarted. Conflicting shortcuts are logged; tray actions remain available.
