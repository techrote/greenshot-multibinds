# Recording

Open **Preferences > Recording** to configure capture and output. The new controls use a draft until **OK**; Cancel discards recording preference edits.

| Shortcut | Action |
|---|---|
| F6 | Start / stop recording the saved region |
| F7 | Select a region on the live screen; Escape cancels |
| F8 | Show / hide its click-through border |
| F9 | Enable / disable the optional timer |

The region is stored in physical screen pixels and persists across recordings and restarts. Select before starting. F7 is unavailable during capture/finalization. Normal screenshot freezing is unchanged. The border is excluded from screenshots; it is hidden if Windows cannot exclude it.

## Time limits and FPS

- Target FPS: integer 1-120, default 30. Actual rate depends on region, CPU and storage.
- Optional timer: disabled by default; default duration `00:01:00`. Enter positive `[days.]hh:mm:ss[.fff]` (for example `00:00:12.500`). F9 changes apply during the current capture, measured from its original start, so enabling an already-expired limit stops on the next capture iteration.
- Universal limit: always enforced, default 10 minutes. Preferences accept positive minutes up to 120, including fractional minutes. The recorder also clamps manual INI values to 120 minutes; invalid values fall back to 10. The shorter enabled limit wins.
- Timer lengths, FPS and output settings are snapshotted at recording start. F9 controls the optional timer's enabled state live. Reaching a limit stops acquisition, then saves queued frames and finalizes the output. ZIP finalization can outlast a time limit.

## Output

Default destination: **Pictures/Greenshot Recordings**. Choose:

| Folder | ZIP | Result |
|---|---|---|
| On | Off | One folder per recording |
| Off | On | One ZIP; temporary loose frames are removed after successful archive creation |
| On | On | Folder and ZIP |
| Off | Off | Individual files in the output root; per-batch CSV/summary names |

ZIP entries retain the original PNG bytes. PNGs are already compressed, so ZIP uses store mode. Working files are written to a unique `.recording-<id>` subfolder and preserved if capture or finalization fails. ZIPs use `.partial` until complete. Collisions receive suffixes; existing files are never overwritten.

## Composable names

Folder/ZIP and frame name templates have live examples in Preferences. Frame templates omit the `.png` extension.

| Token | Meaning / example |
|---|---|
| `{date:yyyy-MM-dd}` | Recording start date |
| `{time:HH-mm-ss-fff}` | Recording start time |
| `{timestamp:yyyy-MM-dd_HH-mm-ss-fff}` | Actual capture time for a frame; start time for a container |
| `{duration:0.000}` | Final capture seconds in folder/ZIP names; elapsed seconds in frame names |
| `{elapsed:0.000}` | Frame capture seconds from start |
| `{window}` | Foreground window title when the region was selected |
| `{fps}` | Target FPS |
| `{count}` | Final saved count in container names; current frame number in frame names |
| `{batch:0000}` | Persistent recording batch counter |
| `{frame:00000000}` | One-based frame number |
| `{timecode}` | Nominal non-drop SMPTE-style `HH-MM-SS-FF`, based on zero-based frame index at target FPS |

Numeric and date/time tokens accept .NET format strings after `:`. Timecode is nominal and does not imply constant actual spacing. `frames.csv` holds the actual monotonic capture timestamps. Invalid filename characters become underscores; reserved Windows names are prefixed. Unknown/malformed tokens are rejected.

The frame queue is bounded at approximately 128 MiB (at least one frame). If PNG storage cannot keep up, acquisition stops and accepted frames drain rather than being silently dropped. The MVP records RGB screen pixels, without cursor, audio, HDR metadata or minimized/offscreen application content. Visible overlapping windows are recorded. Windows 10 2004+ is required for live selection and border exclusion.

## Recording status overlay

During recording, a compact click-through overlay displays elapsed capture time, time remaining to the currently active limit, actual FPS measured over the last second, and captured/saved frame counts. It updates four times per second and follows F9 timer changes. Capture time freezes while queued frames drain; the header changes to Saving frames, then Finalizing output. The overlay closes when the recording finishes. Windows excludes it from captured frames; if exclusion cannot be enabled, the overlay stays hidden. It is placed at the upper-right of the region's monitor, or the lower-right when the upper position overlaps the selected region.

