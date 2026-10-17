# D3Vision

Windows 10/11 x64 desktop overlay for Diablo III, built with .NET 8 and WPF.

**[Download the v1.1.1 Windows installer](https://github.com/Christopher-Kokkinos/D3_Vision/releases/tag/v1.1.1)**

**Stricken remains experimental:** a controlled solo capture validated the observed proc estimate rising from 0 to 16 and clearing after the guardian disappeared. This is not an exact enemy stack count. See [live validation details](docs/Portrait-buff-validation.md).

Close D3Vision before installing or updating. See [release notes for v1.1.1 and v1.1.0](CHANGELOG.md). The installer includes the .NET runtime,
adds Start-menu shortcuts, and preserves settings and history. It is unsigned;
Windows may display an unknown-publisher warning. See the feature notes and validation
limits below before using the experimental readers and input automation.

Experimental nearby map: enable **NEARBY MAP (EXPERIMENTAL)** under F12 settings, then Apply and save. **Align nearby map with game minimap** follows the local player at 10 Hz and draws available walkable geometry over the native top-right minimap. Geometry refreshes once per second; scale and horizontal/vertical calibration are saved in settings. Alignment uses game client coordinates and the overlay's DPI transform, and hides when the game loses focus or the player/world cannot be validated. Disable alignment for the draggable/resizable standalone panel with dashed cached-area outlines. The reader supports only the validated 2.8.0.99920 executable. Complete unexplored-floor reveal and the expanded Tab map are not supported. Mixed-DPI hardware and moving-player alignment still need live validation. See [reader implementation, validation and probe commands](docs/Map-reader-reverse-engineering.md).

Minimap markers: nearby champions are blue, rare elites yellow, rare minions amber, uniques purple and bosses orange-red. Ground legendaries use orange stars and set items green stars. Ancients have larger, brighter stars with a halo; Primals use a red circled pentagram. Closely stacked loot spreads slightly with leader lines to the actual drop positions. F12 has separate elite/loot toggles and live reader status. Markers refresh at 5 Hz and are limited to actors currently available to the client. Normal/Ancient set items and normal legendary loot were checked live; a live blue champion was verified after correcting the GameBalance-type filter; live Primal validation remains pending. Marker styling can be reviewed with `dotnet run --project tests/D3Vision.Tests -c Release -- --marker-preview` (writes `artifacts/map-marker-preview.png`).

Kadala hover buying: in F12 settings enable **Enable Kadala multi-buy**, Apply and save, then use F11 to lock the overlay/click-through. Choose **Hover for 3 seconds** or **Single left-click** under Kadala activation. Open Kadala with inventory visible, then hover over the same Mystery offer for three seconds or left-click it once. Keep the pointer on the offer in either mode. Click mode listens only to physical left-button releases, so generated purchase inputs cannot rearm the macro. Buying stops when the cursor leaves, game loses focus, settings open, overlay is hidden/unlocked, or a purchase adds no inventory item within two seconds (full inventory, insufficient shards or unavailable offer). Move off and hover again to rearm hover mode; click mode permits a new deliberate left-click after completion. Purchases are confirmed through the read-only backpack count and dispatched one at a time as balanced foreground right-clicks; the mouse never moves. Kadala mode pauses skill auto-cast. English shop text and the standard height-scaled Kadala panel are required; unrecognized UI sends no input. Detection and inventory reads were checked against the user's open shop; no live purchase was issued during validation. Hover, rejection-stop and dispatch behavior have synthetic coverage.

Myriam build guide: enabled by default under F12. Opening the English Enchant screen automatically shows a centred gear/primary-affix reference for the detected local hero class. The source is Blizzard's EU seasonal normal solo-GR board; the current season is discovered from official navigation. If rank one has no accessible profile, the highest accessible profile among the first 20 entries is used and its actual rank is prominently displayed (this fallback is configurable). Empty/unpublished gear slots and unavailable affixes are labelled. The shown affixes are references from published current gear, not an optimizer or the historical loadout at the ranked clear. All 13 gear slots fit in a responsive inventory-style layout without scrolling or unlocking the overlay; closing Enchant, losing game focus, opening settings or hiding the overlay dismisses the panel. Retrieval runs asynchronously, reuses the existing OCR feed, checks profile/tooltip hero identity and caps HTML size. Per-class JSON caches refresh every 15 minutes; offline references up to 24 hours old carry an offline label. No browser, embedded HTML, API credentials or additional capture session is required. See [current EU rankings](https://eu.diablo3.blizzard.com/en-us/rankings/season/40/rift-barbarian). A rendered live-data layout check is saved locally as `artifacts/myriam-guide-preview.png`.



Windows 10 2004+ / Windows 11, .NET 8, x64, WPF and C# 12. All five requested projects are implemented. There are no paid or third-party runtime libraries; Windows SDK projections are restored by the .NET SDK.

## Build and launch

Install the .NET 8 SDK (a newer SDK can also target .NET 8). Visual Studio is optional; if used, install the .NET desktop development workload. Restore needs access to NuGet for Microsoft's Windows targeting packages.

```powershell
Set-Location C:\Projects\D3_Buddy
dotnet restore D3Vision.sln
dotnet build D3Vision.sln -c Release
dotnet run --project src/D3Vision.UI -c Release
```

Run these commands from the project folder, not `C:\Windows\system32`. Alternatively, use the full solution path: `dotnet build C:\Projects\D3_Buddy\D3Vision.sln -c Release`.

Settings opens at launch. Run Diablo III in windowed or borderless mode. An independent desktop overlay is not guaranteed to appear above exclusive fullscreen games.

If only a yellow border appears over the game, use **Diablo III → Options → Video → Display Mode → Windowed (Fullscreen)**, then apply the change. The yellow border is Windows Graphics Capture's notification, independent of the WPF overlay. Exclusive fullscreen can hide desktop windows even when they are topmost. D3Vision reasserts its topmost Z order once per second while Diablo III is foreground, without activating the overlay, to recover after game restores and topmost-window changes. This cannot make a desktop window render inside exclusive fullscreen. The game's saved `DisplayModeWindowMode "0"` indicates fullscreen; `"2"` is Windowed (Fullscreen). Changing the option in-game applies it immediately and avoids editing preferences while the running game might overwrite them.

Windows OCR is officially supported for desktop applications **with package identity**. The unpackaged executable is useful for UI development and manual widgets, but package the application for supported OCR deployment. Install a Windows language with OCR support through Windows Settings. The engine uses the first supported user-profile language; the game's parsing rules currently recognize English messages.

### Windows installer

The standard installer is `artifacts/installers/D3Vision-Setup-1.1.1-win-x64.exe`.
Close the overlay before running it. It supports Intel/AMD x64 Windows 10
version 2004 (build 19041) or newer and Windows 11, includes the .NET 8 Desktop
Runtime, and installs for the current user without elevation. Start-menu shortcuts,
an optional desktop shortcut, and an Apps & Features uninstaller are included.
Settings and history in `%LOCALAPPDATA%\D3Vision` survive updates and uninstall.
The generated installer is unsigned; Windows may show an unknown-publisher warning.

Rebuild from the repository root with the .NET SDK and Internet access:

```powershell
.\scripts\Build-Installer.ps1 -Verify
```

The script downloads a checksum-pinned portable NSIS compiler if absent and selects
the latest .NET 8 runtime from Microsoft's release metadata. Pass `-RuntimeVersion
8.0.32` to pin a release. Output includes a SHA-256 checksum and payload manifest.
Verification checks every installed payload hash and uninstall/user-file retention
in a workspace directory. Its validation build suppresses shortcuts, registry writes
and running-app checks; those integration points still require interactive validation.
Windows 10 hardware validation and release code signing remain separate release tasks.
See `packaging/INSTALL.txt` for runtime features and supported game build details.

### Optional MSIX deployment

Install the Windows 10/11 SDK, including MakeAppx and SignTool. Then:

```powershell
powershell -NoProfile -File scripts/Package.ps1
# Sign using an already installed code-signing certificate whose subject is CN=D3Vision:
powershell -NoProfile -File scripts/Package.ps1 -CertificateThumbprint YOUR_THUMBPRINT
Add-AppxPackage -Path artifacts/D3Vision.msix
```

The package contains a self-contained x64 publish. Install only after signing with a certificate trusted by the target machine. Certificate creation, certificate trust changes and package installation are intentionally separate deployment steps. For another publisher, update the manifest Publisher and use a matching certificate. No administrator access is needed by the application itself. Windows SDK packaging tools are not installed in the development environment used here, so the MSIX creation/signing step has not been executed here.

## Operation

### Hover-to-toggle automatic skill casting

With `VisualRead` selected, skills calibrated, settings closed and the overlay locked/click-through, hover over its calibrated in-game skill artwork continuously for **three seconds**. A small fill indicator shows the dwell. A green outline and `AUTO` mark an armed slot. Move away to let it cast. Leave and hover again for three seconds to turn that slot off. Old calibrations must be saved again to record the capture mode. Hover tests use the latest physical capture bounds, reject overlapping boxes and changed aspect ratios/capture modes/monitors, and never use movable overlay badges. Keeping the pointer there toggles only once; moving to another slot restarts the dwell timer. Hovering over any skill pauses dispatch while you change toggles.

Armed slots send a short 1–4/LMB/RMB press only when the visual reader reports confirmed ready. Unknown, stale, inferred-zero and cooling readings do not trigger casts. The system sends at most one global attempt every 200 ms and one attempt per skill per second. Ready skills rotate fairly. Mouse casts use the existing cursor position; the cursor is never moved. Held keys/buttons and modifiers are left alone. Auto-cast requires Diablo III in the foreground and the pointer inside its window.

**Escape stops all auto-casting.** Enter and the standard menu/chat/portal keys I/C/S/P/J/M/O/T also disarm it while passing through normally. Switching away from the game, F10/F11, opening settings, calibration or quitting disarms every slot. Settings also includes **Stop all auto-casting**. Toggles are session-only and always off at launch. After a stop, hover again to re-arm the desired skills. The feature uses the six default skill bindings and does not detect arbitrary mouse-clicked game panels, cast success, resource availability or alternate bindings. Real-game input acceptance and visual readiness accuracy still need gameplay validation.

### HUD placement

The new preset uses a 26-pixel stats row at the top, an unboxed rift percentage on the right, six countdown badges over the skill bar, and four live portrait buff frames. Muted bronze text and dark shadows replace the large panels. The preset is applied once to existing settings; later launches preserve your calibrated positions. **Reset to game HUD layout** restores the starting alignment.

Press F11 to reveal editing outlines. Drag each widget's top edge to reposition it and its lower-right corner to resize it. This is a starting alignment for windowed/borderless play: calibrate it for the game's resolution and UI scale. Screen anchors keep the top row and bottom skill group aligned when the monitor resolution changes.

**Automatic visual skill reading** is now the default (`VisualRead`). Press F12, wait until the skills are ready, then choose **Calibrate ready skill icons...**. In the captured image, select a slot and draw a box around its square skill artwork, excluding its frame and key label. Repeat for the other slots and save. In locked VisualRead mode, countdowns, hover progress and AUTO outlines now anchor directly to the six calibrated artwork boxes; movable display badges are only used for editing/manual fallback. Recalibrate after changing skills, UI scale, aspect ratio or capture mode. Normalised coordinates support resolution changes with the same aspect ratio.

The reader compares a calibrated ready icon with its current shaded sector at up to 10 Hz. It estimates seconds from the observed rate of sweep movement, rather than a configured duration or key press. `~` marks inferred seconds; `?` means uncalibrated, unreadable, stale, or not enough movement to infer a rate. Long cooldowns can require several seconds of observations. Two ready frames confirm a reset. Noisy non-radial changes are rejected, and missing capture becomes unknown after one second. Calibration rejects icons with insufficient bright detail. This is an experimental visual estimate, **not exact game telemetry**: animations, lighting, charges, icon changes and very small/low-contrast skills can cause errors. A shaded fraction alone does not reveal exact remaining seconds. Only synthetic sweep fixtures have been validated; live Diablo III accuracy still needs testing. Visual reading does not require Windows text OCR or package identity.

`InputEstimate` retains the previous manual fallback. Configure effective durations for 1–4/LMB/RMB; 0 disables a timer. Key/button input starts estimates only when Diablo III is foreground and continues to reach the game. Holding a key does not restart an active countdown. Click a badge while unlocked to test it. Configured durations and input starts are ignored in `VisualRead`. **Clear skill countdowns** resets both modes.

**Live portrait indicators:** F12 enables live buffs and selects `PortraitGlow` (default) or `RingIcon` for Convention of Elements. The glow follows the observed active element, without a simulated cycle. Squirt uses a necklace symbol: gold with stacks while active, red cross at zero stacks when worn equipment is confirmed, and `?` for unknown readings. A `*` beside remote stacks means the power is observed but remote gear is not available to confirm wearing versus a cubed power. Stricken displays `ready` while equipped outside a guardian encounter, and `~` observed proc counts near a single identified live boss; this is an estimate, not exact per-target stacks, and resets when the encounter, sampling, area or focus changes. F11 aligns/resizes portrait frames. Local hero maps to the first frame automatically; other party frames follow game slot order, with explicit slot overrides available in settings. Out-of-world, stale and absent players have no live glow. CoE cycling was observed live on Barbarian and Crusader; Squirt stacks and a Stricken proc flag were observed on party members. Exact Stricken target stacks are not available from this reader. Live Stricken proc transitions and Squirt recovery from 6 to 10 stacks were observed. A controlled solo capture then observed confirmed worn Squirt stacks 0 through 10 and Stricken proc estimates increasing 0 through 16, followed by reset after guardian disappearance. Exact enemy stack totals and multi-enemy attribution remain unverified. The element types follow [Blizzard's Convention of Elements description](https://eu.diablo3.blizzard.com/en-us/item/convention-of-elements-P2_Unique_Ring_04). Compact XP/hour uses K/M/B abbreviations.

Generate an illustrative preview with the actual HUD renderer (sample skill times, not a game screenshot):

```powershell
dotnet run --project tests/D3Vision.Tests -c Release -- --hud-preview
```

The image is written to `artifacts/hud-preview.png`.

| Key | Action |
| --- | --- |
| F10 | Show/hide the widgets; capture and tracking continue |
| F11 | Toggle click-through; unlocked widgets accept interaction |
| F12 | Open or focus settings |

Drag a widget's top edge (up to 10 pixels) to move it; drag its lower-right corner to resize. Widgets snap within 12 device-independent pixels of the selected monitor's edges. Configure each widget's enabled state, opacity and accent colour in settings. Settings also provides theme, installed font, monitor, whole-screen capture and build notes. Choose **Apply and save** after changing settings. Monitor indices follow Windows enumeration order; after unplugging a monitor, the index is clamped to an available screen. Positions are stored in device-independent pixels relative to each widget's screen anchor.

Death, elite-pack and goblin counters use the `+` button while unlocked. OCR supplies recognized game timers, rift completion, percentages, XP messages and legendary/set-item text notifications. It does not infer deaths, packs or skill activation from game memory. XP/hour is recognized XP divided by elapsed session hours. Build notes are edited in settings. Item text is retained with the session, capped at 200 entries.

Detection is deliberately based on visible text. Hidden item names, missing XP notifications, non-English messages, OCR errors and unrelated visible percentages/timers can affect the result. Persistent identical lines are deduplicated between consecutive OCR results; an identical message that disappears and reappears can count again. These are heuristics, not game telemetry. Images exceeding `OcrEngine.MaxImageDimension` are downscaled with preserved aspect ratio before recognition. Small text can become harder to recognize at high desktop resolutions.

Use **Exit D3Vision** in settings for a clean shutdown and a session-history entry. An unfinished session resumes after an interrupted run; a clean exit starts a new session next launch. A named mutex prevents two instances from competing for hotkeys or configuration files.

## Architecture

The dependency direction is `UI -> Services / Infrastructure / Rendering -> Core`. Core has no WPF dependency. Construction and service lifetime belong to `App`; UI-facing state belongs to `MainViewModel`. Widgets expose bindable configuration through `INotifyPropertyChanged`. Window code handles input geometry and Win32 window behaviour; OCR interpretation and persistence live outside the views.

| Project | Responsibility |
| --- | --- |
| D3Vision.Core | Observable models, settings, sessions, capture/OCR/storage abstractions |
| D3Vision.Infrastructure | Atomic JSON replacement, native APIs, global keyboard hook, D3D11 Windows Graphics Capture, optional GDI capture backend |
| D3Vision.Services | Windows OCR adapter, 10 Hz capture worker, throttled OCR and text interpretation |
| D3Vision.Rendering | DrawingContext HUD renderer for compact text, skill countdowns and element strips |
| D3Vision.UI | WPF application, composition/lifetime, MVVM state, transparent overlay, settings |

`GraphicsCapture` uses a hardware D3D11 device, a two-frame free-threaded Windows Graphics Capture pool and a reusable CPU BGRA buffer. It targets the Diablo III HWND, including when occluded, or the selected monitor in whole-screen mode. The window is rediscovered every two seconds, minimizing process-enumeration overhead while the game is absent. Resizing recreates the frame pool. Minimized games pause usable capture. Device/capture failures dispose capture resources and retry after five seconds. `ScreenCapture` is an alternative GDI implementation for integration scenarios; it is not the default and only sees visible pixels.

`TrackingService` consumes frames at 10 Hz when capture/readback/OCR can keep up. OCR runs at most once per configured interval (default one second), serially, without overlapping recognitions or a growing work queue. Slow OCR naturally reduces the effective capture rate. The worker dispatches recognized text to the UI thread before modifying statistics. Capture resources are also disposed on that worker before shutdown, avoiding a UI-thread stall when Windows Graphics Capture is active. The HUD checks values at 10 Hz for smooth countdowns, invalidating each visual only when its displayed content changes. Cooldown arithmetic uses a monotonic clock.

Rendering uses WPF's Direct3D composition pipeline, with WPF's software fallback when acceleration is unavailable. This is not a custom D3D swap-chain renderer or an injected overlay. Text shadows, thin bronze rules, countdown badges and element segments use DrawingContext primitives. Resize controls and editing outlines disappear while locked. The Slate theme adds a subtle translucent backing.

The overlay uses topmost, layered, tool-window and no-activate extended styles. F11 controls `WS_EX_TRANSPARENT`. F10/F11/F12 are handled with a short low-level keyboard hook; actions are queued onto the dispatcher. F12 cannot be registered through RegisterHotKey because Windows reserves it for debuggers. The hook consumes these keys while the app is running and is removed on window closure. The overlay requests exclusion from capture so its own text does not feed back into OCR. Per-monitor-v2 awareness and native physical-pixel window placement support different monitor DPI values and negative desktop coordinates. One selected monitor hosts the overlay at a time.

Configuration and data are stored under `%LOCALAPPDATA%\D3Vision` (Windows may redirect this path for packaged applications):

- `settings.json`: widget geometry, appearance, activation, capture and user settings.
- `current-session.json`: active/latest session statistics and item observations.
- `history.json`: last 100 cleanly completed sessions.

Writes serialize through a semaphore, write a sibling temporary file and replace the destination. Settings/statistics autosave every 30 seconds and on clean exit. Invalid JSON falls back to defaults; I/O errors are surfaced in settings. Full capture images are never saved. Settings retain only each calibrated skill's 64×64 BGRA ready-icon patch and its normalised rectangle. Startup exceptions are written to `%TEMP%\D3Vision-startup.log`.

## Validation and performance

```powershell
dotnet run --project tests/D3Vision.Tests -c Release
# Optional live monitor-capture integration check, without saving pixels:
dotnet run --project tests/D3Vision.Tests -c Release -- --capture
# Launch, find settings via UI Automation, invoke Exit, verify process termination:
powershell -NoProfile -File scripts/Smoke-Test.ps1
# Run while the game, overlay and OCR are active:
powershell -NoProfile -File scripts/Measure-Performance.ps1 -ProcessId YOUR_D3VISION_PID -Seconds 30
```

Validated here: Release build and 172 checks covering map/cache validation and geometry, layout migration, parsing/layout, synthetic visual cooldowns, hover timing/latching, interrupted dwell, ready-state gating, rate limiting, stop behaviour, and input dispatch using a fake backend (including cancellation/release and focus changes); live 1920x1080 D3D11 monitor capture; F10/F11/F12 interaction, UI startup and orderly shutdown. Earlier HUD smoke tests sampled about 166–171 MB working set; the latest capture-active UI test sampled about 229 MB, above the requested memory target. These are not full gameplay benchmarks. The <2% CPU and <200 MB RAM values remain performance targets, not verified guarantees. CPU in the profiling script is normalized over all logical processors. Resolution, driver, OCR language, WPF rendering tier and capture mode affect resource use. GPU allocations are not completely reflected in process working set.

Still requiring release validation: packaged OCR, real Diablo III capture and recognition accuracy, mixed-DPI/multiple-monitor hardware, extended gameplay performance and signed MSIX installation. No game screenshots or fixtures were supplied, so no game-specific detection accuracy is claimed.

## Source tree

```text
D3Vision.sln
Directory.Build.props
.editorconfig
.gitignore
README.md
packaging/
  AppxManifest.xml
  Assets/
    Square44x44Logo.png
    Square150x150Logo.png
    StoreLogo.png
scripts/
  Package.ps1
  Smoke-Test.ps1
  Measure-Performance.ps1
src/
  D3Vision.Core/
    D3Vision.Core.csproj
    Models.cs
    HudLayout.cs
    SkillTimers.cs
    VisualCooldowns.cs
    AutoCastController.cs
  D3Vision.Infrastructure/
    D3Vision.Infrastructure.csproj
    GraphicsCapture.cs
    GameInput.cs
    GameSkillCaster.cs
    JsonStore.cs
    KeyboardHotkeys.cs
    Native.cs
    ScreenCapture.cs
  D3Vision.Rendering/
    D3Vision.Rendering.csproj
    WidgetVisual.cs
  D3Vision.Services/
    D3Vision.Services.csproj
    FrameScaler.cs
    CooldownSweepReader.cs
    NoTextOcrService.cs
    TrackingService.cs
    WindowsOcrService.cs
  D3Vision.UI/
    D3Vision.UI.csproj
    App.xaml
    App.xaml.cs
    app.manifest
    MainViewModel.cs
    OverlayWindow.cs
    SettingsWindow.cs
    SkillCalibrationWindow.cs
tests/
  D3Vision.Tests/
    D3Vision.Tests.csproj
    Program.cs
    HudPreview.cs
    VisualCooldownChecks.cs
    AutoCastChecks.cs
    SkillInputChecks.cs
```

## Platform references

- [Windows OCR namespace and desktop package identity](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr)
- [Windows Graphics Capture HWND interop](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow)
- [WPF hardware rendering](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)
- [RegisterHotKey and the reserved F12 key](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Window capture exclusion](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)
