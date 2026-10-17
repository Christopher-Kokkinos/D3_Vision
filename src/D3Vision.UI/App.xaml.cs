using System.IO;
using System.Windows;
using System.Windows.Threading;
using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

namespace D3Vision.UI;

public partial class App : Application
{
    readonly JsonStore store = new();
    readonly CancellationTokenSource stop = new();
    readonly GraphicsCapture capture = new();
    readonly GameSkillCaster skillCaster = new();
    MainViewModel vm = null!;
    OverlayWindow overlay = null!;
    SettingsWindow? settings;
    Task? tracking;
    DispatcherTimer? refresh,
        autosave;
    bool exiting;
    Mutex? instanceMutex;
    bool ownsMutex;
    bool calibrating;
    Task? autoCastTask;
    Task? mapTracking;
    Task? kadalaTracking;
    readonly KadalaRunner kadalaRunner = new();
    readonly DiabloMapReader mapReader = new();
    BlizzardBuildService buildProvider = null!;
    CancellationTokenSource? myriamCancellation;
    readonly List<Task> myriamTasks = [];
    long myriamSeen,
        nextMyriamRefresh;
    int myriamGeneration;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instanceMutex = new Mutex(true, "Local\\D3Vision.Desktop", out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show("D3Vision is already running. Press F12 to open settings.", "D3Vision");
            Shutdown();
            return;
        }
        try
        {
            vm = new(
                await store.LoadAsync("settings", () => new Settings()),
                await store.LoadAsync("current-session", () => new SessionStats())
            );
            if (vm.Stats.Ended is not null)
                vm = new(vm.Settings, new SessionStats());
            HudLayout.Upgrade(vm.Settings);
            buildProvider = new BlizzardBuildService(store);
            overlay = new(vm);
            MainWindow = overlay;
            overlay.Hotkey += id =>
            {
                vm.AutoCast.StopAll();
                if (id == 0)
                {
                    if (overlay.IsVisible)
                        overlay.Hide();
                    else
                        overlay.Show();
                }
                else if (id == 1)
                {
                    vm.Settings.ClickThrough = !vm.Settings.ClickThrough;
                    overlay.SetClickThrough();
                }
                else if (id == 2)
                    OpenSettings();
            };
            overlay.SkillInput += key =>
            {
                if (!vm.Settings.TrackSkillInput || !GameInput.IsDiabloForeground())
                    return;
                int slot = key switch
                {
                    0x31 => 0,
                    0x32 => 1,
                    0x33 => 2,
                    0x34 => 3,
                    1 => 4,
                    2 => 5,
                    _ => -1,
                };
                if (slot >= 0)
                    vm.StartSkill(slot);
            };
            overlay.PhysicalLeftClick += (x, y) =>
            {
                if (
                    !exiting
                    && !calibrating
                    && settings is null
                    && overlay.IsVisible
                    && vm.Settings.ClickThrough
                    && vm.Settings.KadalaHoverBuy
                    && vm.Settings.KadalaMode == KadalaActivationMode.SingleClick
                )
                    kadalaRunner.RecordLeftClick(x, y);
            };
            overlay.Show();
            mapTracking = TrackMapAsync(stop.Token);
            kadalaTracking = Task.Run(() =>
                kadalaRunner.RunAsync(
                    () =>
                        Dispatcher.Invoke(() =>
                            !exiting
                            && !calibrating
                            && settings is null
                            && overlay.IsVisible
                            && vm.Settings.ClickThrough
                            && vm.Settings.KadalaHoverBuy
                        ),
                    () => Dispatcher.Invoke(() => vm.Settings.KadalaMode),
                    message => Dispatcher.BeginInvoke(() => vm.KadalaStatus = message),
                    stop.Token
                )
            );
            refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            var nextXpSample = DateTimeOffset.UtcNow;
            refresh.Tick += (_, _) =>
            {
                if (DateTimeOffset.UtcNow >= nextXpSample)
                {
                    vm.XpSamples.Add(vm.XpRate);
                    if (vm.XpSamples.Count > 60)
                        vm.XpSamples.RemoveAt(0);
                    nextXpSample = DateTimeOffset.UtcNow.AddSeconds(1);
                }
                if (
                    vm.MyriamDetected
                    && (
                        !vm.Settings.MyriamGuideEnabled
                        || settings is not null
                        || !overlay.IsVisible
                        || !GameInput.IsDiabloForeground()
                        || Environment.TickCount64 - myriamSeen
                            > Math.Clamp(vm.Settings.OcrIntervalMs * 2 + 1000, 3000, 10000)
                    )
                )
                    DismissMyriam();
                UpdateAutoCast();
                overlay.MaintainTopmost();
                overlay.Refresh();
            };
            refresh.Start();
            autosave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            autosave.Tick += async (_, _) => await SaveSafe();
            autosave.Start();
            try
            {
                IOcrService textOcr;
                string? ocrError = null;
                try
                {
                    textOcr = new WindowsOcrService();
                }
                catch (Exception ex)
                {
                    textOcr = new NoTextOcrService();
                    ocrError = ex.Message;
                }
                var service = new TrackingService(capture, textOcr);
                service.CooldownsRead += observations =>
                    Dispatcher.Invoke(() =>
                    {
                        if (vm.Settings.CooldownMode != CooldownMode.VisualRead)
                            return;
                        foreach (var observation in observations)
                            vm.VisualCooldowns.Apply(observation);
                    });
                service.TextRecognized += text =>
                    Dispatcher.Invoke(() =>
                    {
                        ObserveMyriam(text);
                        service.Apply(text, vm.Stats);
                        overlay.Refresh();
                    });
                service.StatusChanged += status =>
                    Dispatcher.Invoke(() =>
                        vm.Status = ocrError is null
                            ? status
                            : status + " • text OCR unavailable: " + ocrError
                    );
                tracking = Task.Run(async () =>
                {
                    try
                    {
                        await service.RunAsync(
                            () =>
                                (
                                    vm.Settings.WholeScreen,
                                    vm.Settings.Monitor,
                                    vm.Settings.OcrIntervalMs
                                ),
                            stop.Token,
                            () =>
                                Dispatcher.Invoke<IReadOnlyList<SkillCalibration?>>(() =>
                                    vm.Settings.CooldownMode == CooldownMode.VisualRead
                                        ? vm
                                            .Settings.Skills.Take(6)
                                            .Select(s => s.Calibration)
                                            .ToArray()
                                        : []
                                )
                        );
                    }
                    finally
                    {
                        capture.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                vm.Status = ex.Message;
            }
            OpenSettings();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "D3Vision-startup.log"),
                    ex.ToString() + Environment.NewLine
                );
            }
            catch (IOException) { }
            MessageBox.Show(ex.ToString(), "D3Vision startup failed");
            Shutdown(1);
        }
    }

    void OpenSettings()
    {
        vm.AutoCast.StopAll();
        if (settings is not null)
        {
            settings.Activate();
            return;
        }
        settings = new(
            vm,
            () =>
            {
                overlay.ApplyMonitor();
                overlay.SetClickThrough();
                _ = SaveSafe();
            },
            () => _ = QuitAsync(),
            () => _ = CalibrateSkillsAsync()
        );
        settings.Closed += (_, _) => settings = null;
        settings.Show();
    }

    async Task SaveSafe()
    {
        try
        {
            await store.SaveAsync("settings", vm.Settings);
            await store.SaveAsync("current-session", vm.Stats);
        }
        catch (Exception ex)
        {
            vm.Status = "Save failed: " + ex.Message;
        }
    }

    void ObserveMyriam(string text)
    {
        if (exiting)
            return;
        bool detected =
            vm.Settings.MyriamGuideEnabled
            && overlay.IsVisible
            && settings is null
            && GameInput.IsDiabloForeground()
            && MyriamDetection.IsEnchantScreen(text);
        if (!detected)
        {
            DismissMyriam();
            return;
        }
        myriamSeen = Environment.TickCount64;
        bool fresh = !vm.MyriamDetected;
        vm.MyriamDetected = true;
        if (fresh || Environment.TickCount64 >= nextMyriamRefresh)
        {
            myriamCancellation?.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            myriamCancellation = cancellation;
            int generation = ++myriamGeneration;
            vm.MyriamBuild = null;
            vm.MyriamStatus = "Reading current hero and loading EU seasonal gear...";
            nextMyriamRefresh = Environment.TickCount64 + 900000;
            myriamTasks.RemoveAll(t => t.IsCompleted);
            myriamTasks.Add(
                LoadMyriamAsync(generation, cancellation, vm.Settings.MyriamAllowFallback)
            );
        }
    }

    void DismissMyriam()
    {
        if (!vm.MyriamDetected)
            return;
        vm.MyriamDetected = false;
        ++myriamGeneration;
        myriamCancellation?.Cancel();
    }

    async Task LoadMyriamAsync(
        int generation,
        CancellationTokenSource cancellation,
        bool allowFallback
    )
    {
        try
        {
            var hero = await Task.Run(
                () =>
                {
                    var snapshot = mapReader.Capture();
                    return mapReader.ReadHeroClass(snapshot);
                },
                cancellation.Token
            );
            var build = await buildProvider.GetAsync(hero, allowFallback, cancellation.Token);
            if (generation == myriamGeneration && !exiting)
            {
                vm.MyriamBuild = build;
                vm.MyriamStatus = build.Notice;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (generation == myriamGeneration && !exiting)
                vm.MyriamStatus = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(myriamCancellation, cancellation))
                myriamCancellation = null;
            cancellation.Dispose();
            if (!exiting)
                overlay.Refresh();
        }
    }

    void UpdateAutoCast()
    {
        if (vm.Settings.KadalaHoverBuy || vm.MyriamDetected)
        {
            vm.AutoCast.StopAll();
            return;
        }
        bool allowed =
            !exiting
            && !calibrating
            && settings is null
            && overlay.IsVisible
            && vm.Settings.ClickThrough
            && vm.Settings.CooldownMode == CooldownMode.VisualRead
            && vm.Settings.Widgets.Any(w => w.Kind == WidgetKind.Cooldowns && w.Enabled);
        if (
            !allowed
            || !GameInput.TryGetGameBounds(out var gameBounds)
            || !GameInput.TryGetCursor(out var cursor)
        )
        {
            vm.AutoCast.StopAll();
            return;
        }
        var captureBounds = gameBounds;
        if (vm.Settings.WholeScreen)
        {
            var monitors = Native.Monitors();
            if (monitors.Count == 0)
            {
                vm.AutoCast.StopAll();
                return;
            }
            captureBounds = monitors[Math.Clamp(vm.Settings.Monitor, 0, monitors.Count - 1)];
            if (
                captureBounds.Right <= gameBounds.Left
                || captureBounds.Left >= gameBounds.Right
                || captureBounds.Bottom <= gameBounds.Top
                || captureBounds.Top >= gameBounds.Bottom
            )
            {
                vm.AutoCast.StopAll();
                return;
            }
        }
        vm.AutoCast.UpdateHover(overlay.HoveredSkill(cursor, captureBounds), true);
        bool cursorInside =
            cursor.X >= gameBounds.Left
            && cursor.X < gameBounds.Right
            && cursor.Y >= gameBounds.Top
            && cursor.Y < gameBounds.Bottom;
        if (autoCastTask is { IsCompleted: false })
            return;
        var slot = vm.AutoCast.TakeReadySkill(vm.CooldownSnapshot(), cursorInside);
        if (slot is { } ready)
            autoCastTask = CastSkillAsync(ready);
    }

    async Task CastSkillAsync(int slot)
    {
        try
        {
            if (await skillCaster.CastAsync(slot, stop.Token) == SkillCastResult.Failed)
            {
                vm.AutoCast.StopAll();
                vm.Status = "Auto-cast stopped: the input could not be sent to the game.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            vm.AutoCast.StopAll();
            vm.Status = "Auto-cast stopped: " + ex.Message;
        }
    }

    async Task CalibrateSkillsAsync()
    {
        if (calibrating)
            return;
        calibrating = true;
        try
        {
            bool whole = vm.Settings.WholeScreen;
            int monitor = vm.Settings.Monitor;
            var frame = await Task.Run(async () =>
            {
                using var calibrationCapture = new GraphicsCapture();
                for (int i = 0; i < 30; i++)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    var captured = calibrationCapture.Capture(whole, monitor);
                    if (captured is not null)
                        return captured;
                    await Task.Delay(100, stop.Token);
                }
                return null;
            });
            if (frame is null)
            {
                MessageBox.Show(
                    "No game image is available. Open Diablo III in windowed/borderless mode, then try again.",
                    "Skill calibration"
                );
                return;
            }
            new SkillCalibrationWindow(
                frame,
                vm.Settings.Skills,
                calibrations =>
                {
                    for (int i = 0; i < Math.Min(6, calibrations.Count); i++)
                        vm.Settings.Skills[i].Calibration = calibrations[i];
                    vm.Settings.CooldownMode = CooldownMode.VisualRead;
                    vm.ResetCooldowns();
                    _ = SaveSafe();
                    overlay.Refresh();
                }
            )
            {
                Owner = settings,
            }.ShowDialog();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Skill calibration failed");
        }
        finally
        {
            calibrating = false;
        }
    }

    async Task TrackMapAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        RiftMapSnapshot? snapshot = null;
        RiftMapDrawing drawing = RiftMapDrawing.Waiting;
        long nextGeometry = 0,
            nextMarkers = 0;
        MapMarkerSnapshot markers = new([], "");
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!vm.Settings.Widgets.Any(w => w.Kind == WidgetKind.RiftMap && w.Enabled))
                {
                    vm.RiftMap = RiftMapDrawing.Waiting;
                    continue;
                }
                try
                {
                    bool align = vm.Settings.AlignMapToMinimap;
                    bool elites = vm.Settings.ShowEliteMarkers,
                        loot = vm.Settings.ShowLootMarkers;
                    var result = await Task.Run(
                        () =>
                        {
                            if (snapshot is null || Environment.TickCount64 >= nextGeometry)
                            {
                                snapshot = mapReader.Capture();
                                drawing = RiftMapGeometry.Build(snapshot);
                                nextMarkers = 0;
                                nextGeometry = Environment.TickCount64 + 1000;
                            }
                            if (Environment.TickCount64 >= nextMarkers)
                            {
                                try
                                {
                                    markers = mapReader.ReadMarkers(snapshot, elites, loot);
                                }
                                catch (Exception ex)
                                    when (ex
                                            is System.ComponentModel.Win32Exception
                                                or IOException
                                                or InvalidOperationException
                                                or ArgumentException
                                    )
                                {
                                    markers = new([], "Markers unavailable: " + ex.Message);
                                }
                                nextMarkers = Environment.TickCount64 + 200;
                            }
                            return drawing with
                            {
                                Player = align ? mapReader.ReadPlayer(snapshot) : null,
                                Markers = markers.Markers,
                                Message = drawing.Message + " | " + markers.Status,
                            };
                        },
                        cancellationToken
                    );
                    if (exiting)
                        break;
                    vm.RiftMap = result;
                }
                catch (Exception ex)
                    when (ex
                            is System.ComponentModel.Win32Exception
                                or IOException
                                or InvalidOperationException
                                or UnauthorizedAccessException
                                or ArgumentException
                    )
                {
                    if (exiting)
                        break;
                    vm.RiftMap = new([], ex.Message);
                    markers = new([], "");
                    nextMarkers = 0;
                }
                overlay.Refresh();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    async Task QuitAsync()
    {
        if (exiting)
            return;
        exiting = true;
        vm.AutoCast.StopAll();
        refresh?.Stop();
        autosave?.Stop();
        stop.Cancel();
        if (mapTracking is not null)
            await mapTracking;
        if (kadalaTracking is not null)
            await kadalaTracking;
        await Task.WhenAll(myriamTasks);
        buildProvider.Dispose();
        if (autoCastTask is not null)
            await autoCastTask;
        if (tracking is not null)
            try
            {
                await tracking;
            }
            catch (OperationCanceledException) { }
        if (tracking is null)
            await Task.Run(capture.Dispose);
        vm.Stats.Ended = DateTimeOffset.UtcNow;
        await SaveSafe();
        try
        {
            var history = await store.LoadAsync("history", () => new List<SessionStats>());
            history.Add(vm.Stats);
            await store.SaveAsync("history", history.TakeLast(100).ToList());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Session history could not be saved");
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        stop.Cancel();
        if (ownsMutex)
            instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
