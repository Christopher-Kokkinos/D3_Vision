using D3Vision.Core;
using D3Vision.Infrastructure;
using D3Vision.Services;

namespace D3Vision.UI;

/// <summary>One inventory operation at a time; fresh identity, rank and screen checks before every item.</summary>
internal sealed class InventoryAutomationRunner(
    MainViewModel vm,
    Func<bool> allowed,
    CancellationToken lifetime
)
{
    readonly DiabloMapReader reader = new();
    readonly GameInventoryInput input = new();
    CancellationTokenSource? cancellation;
    public Task? Running { get; private set; }
    public bool Busy => Running is { IsCompleted: false };

    public void Stop() => cancellation?.Cancel();

    public void Start(InventoryAction action)
    {
        if (Busy || !allowed())
            return;
        cancellation?.Dispose();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        vm.AutoCast.StopAll();
        Running = RunAsync(action, cancellation.Token);
    }

    async Task RunAsync(InventoryAction action, CancellationToken token)
    {
        try
        {
            if (!GameInput.TryGetGameClientBounds(out var bounds))
                return;
            bool Guard() =>
                !token.IsCancellationRequested
                && allowed()
                && (
                    action == InventoryAction.DropAll
                        ? vm.Settings.DropAllEnabled
                        : vm.Settings.AutoSalvageEnabled
                )
                && GameInput.TryGetGameClientBounds(out var current)
                && current.Left == bounds.Left
                && current.Top == bounds.Top
                && current.Right == bounds.Right
                && current.Bottom == bounds.Bottom;
            int width = bounds.Right - bounds.Left,
                height = bounds.Bottom - bounds.Top;
            double scale = height / 1080d;
            // This layout is validated for the standard desktop HUD, not console/controller UI.
            if (width / (double)height is < 1.7 or > 3.6)
                throw new InvalidOperationException("Unsupported inventory layout/aspect ratio.");
            await using var capture = new GameCaptureWorker();
            IOcrService ocr = new WindowsOcrService();
            async Task<string> Screen()
            {
                if (!Guard())
                    throw new OperationCanceledException(token);
                var frame = await capture.CaptureAsync(token);
                if (frame is null || frame.Width != width || frame.Height != height)
                    throw new InvalidOperationException("Waiting for a matching game capture.");
                return (
                    await ocr.RecognizeAsync(InventoryOcrFrame.Create(frame), token)
                ).ToLowerInvariant();
            }
            RiftMapSnapshot? inventorySnapshot = null;
            async Task<IReadOnlyList<BackpackItem>> Backpack() =>
                await Task.Run(
                    () =>
                    {
                        inventorySnapshot ??= reader.Capture();
                        return reader.ReadBackpack(inventorySnapshot);
                    },
                    token
                );
            bool IsInventory(string text) => InventoryScreenRecognition.Backpack(text);
            bool IsSalvage(string text) => InventoryScreenRecognition.Salvage(text);
            bool VendorOpen(string text) =>
                text.Contains("salvage")
                || text.Contains("enchant")
                || text.Contains("stash")
                || text.Contains("blood shards")
                || text.Contains("transmogrify")
                || text.Contains("buyback")
                || text.Contains("repair")
                || text.Contains("forge weapons")
                || text.Contains("forge armor")
                || text.Contains("craft item");
            bool DialogOpen(string text) =>
                text.Contains("are you sure") || text.Contains("permanently");
            var text = await Screen();
            if (DialogOpen(text))
                throw new InvalidOperationException(
                    "Close the existing confirmation dialog first."
                );
            if (action == InventoryAction.DropAll && !IsInventory(text))
            {
                await input.KeyAsync(0x49, Guard, token);
                await Task.Delay(500, token);
                text = await Screen();
            }
            if (!IsInventory(text))
                throw new InvalidOperationException("Open your backpack first.");
            if (action != InventoryAction.DropAll && !IsSalvage(text))
                throw new InvalidOperationException("Open the Blacksmith Salvage tab first.");
            if (action == InventoryAction.DropAll && VendorOpen(text))
                throw new InvalidOperationException(
                    "Close the vendor/stash; DROP needs your backpack open in the world."
                );
            var rapid = new GameWindowInventoryInput(bounds);
            var initial = await Backpack();
            var plan = initial.Where(item => InventorySelection.Eligible(item, action)).ToArray();
            vm.InventoryStatus = $"{action}: {plan.Length} backpack items selected. Escape stops.";
            int completed = 0;
            foreach (var target in plan)
            {
                token.ThrowIfCancellationRequested();
                if (completed % 4 == 0)
                    text = await Screen();
                if (!IsInventory(text) || (action != InventoryAction.DropAll && !IsSalvage(text)))
                    throw new InvalidOperationException("The inventory/vendor screen changed.");
                if (DialogOpen(text) || (action == InventoryAction.DropAll && VendorOpen(text)))
                    throw new InvalidOperationException(
                        "A dialog or vendor interrupted the inventory action."
                    );
                if (action != InventoryAction.DropAll)
                    vm.BlacksmithSeen = Environment.TickCount64;
                var items = await Backpack();
                var current = items.FirstOrDefault(item => item.ActorId == target.ActorId);
                if (current is null)
                    continue;
                if (current != target || !InventorySelection.Eligible(current, action))
                    throw new InvalidOperationException(
                        "A selected item changed; hover again to create a new plan."
                    );
                int X(double value) => bounds.Left + (int)Math.Round(value * scale);
                int Y(double value) => bounds.Top + (int)Math.Round(value * scale);
                int itemX = bounds.Right - (int)Math.Round((492 - current.Column * 50) * scale);
                int itemY = Y(584 + current.Row * 50);
                if (action == InventoryAction.DropAll)
                {
                    rapid.Click(itemX - bounds.Left, itemY - bounds.Top, false, Guard, token);
                    rapid.Click(
                        width / 2 - (int)Math.Round(400 * scale),
                        (int)Math.Round(780 * scale),
                        false,
                        Guard,
                        token
                    );
                }
                else
                {
                    if (text.Contains("anvil") && text.Contains("begin salvaging"))
                    {
                        rapid.Click(X(165) - bounds.Left, Y(292) - bounds.Top, false, Guard, token);
                        text = text.Replace("begin salvaging", "salvaging active");
                    }
                    // Re-read after selecting the anvil; never inherit a moved/replaced item.
                    if (!(await Backpack()).Contains(target))
                        throw new InvalidOperationException("Inventory changed before salvage.");
                    rapid.Click(itemX - bounds.Left, itemY - bounds.Top, false, Guard, token);
                }
                bool removed = false;
                bool confirmationSent = false;
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    await Task.Delay(15, token);
                    if (!(await Backpack()).Any(item => item.ActorId == target.ActorId))
                    {
                        removed = true;
                        break;
                    }
                    if (
                        (attempt == 4 || attempt == 16)
                        && !confirmationSent
                        && action != InventoryAction.DropAll
                    )
                    {
                        text = await Screen();
                        // Confirm only a recognisable salvage prompt, never a generic dialog.
                        if (
                            text.Contains("salvage")
                            && text.Contains("sure")
                            && text.Contains("item")
                        )
                        {
                            if (!(await Backpack()).Contains(target))
                                throw new InvalidOperationException(
                                    "The salvage target changed before confirmation."
                                );
                            await input.KeyAsync(0x0D, Guard, token);
                            confirmationSent = true;
                        }
                    }
                }
                if (!removed)
                    throw new InvalidOperationException(
                        "Item did not leave the backpack; stopped without retrying or confirming other dialogs."
                    );
                if (confirmationSent)
                    text = await Screen();
                completed++;
                vm.InventoryStatus = $"{action}: {completed}/{plan.Length}. Escape stops.";
            }
            vm.InventoryStatus =
                $"{action}: finished ({completed} items). Unknown/non-equipment items are skipped by salvage.";
        }
        catch (OperationCanceledException)
        {
            vm.InventoryStatus = "Inventory action stopped.";
        }
        catch (Exception ex)
        {
            vm.InventoryStatus = "Inventory action stopped: " + ex.Message;
        }
    }
}
