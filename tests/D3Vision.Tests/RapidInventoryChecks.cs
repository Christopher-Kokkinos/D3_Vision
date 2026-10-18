using D3Vision.Infrastructure;

static class RapidInventoryChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool result)
        {
            if (!result)
                throw new Exception("Rapid inventory input contract failed.");
            checks++;
        }
        nint foreground = 42;
        var messages = new List<(uint Message, nuint Buttons, nint Position)>();
        var input = new GameWindowInventoryInput(
            new Native.Rect
            {
                Left = 100,
                Top = 50,
                Right = 2020,
                Bottom = 1130,
            },
            () => foreground,
            (_, message, buttons, position) =>
            {
                messages.Add((message, buttons, position));
                return true;
            }
        );
        input.Click(1480, 584, false, () => true, default);
        Check(
            messages.Count == 2
                && messages[0] == (0x201u, (nuint)1, (nint)((584 << 16) | 1480))
                && messages[1].Message == 0x202
                && messages[1].Buttons == 0
        );
        messages.Clear();
        input.Click(100, 200, true, () => true, default);
        Check(
            messages.Count == 2
                && messages[0].Message == 0x204
                && messages[0].Buttons == 2
                && messages[1].Message == 0x205
        );
        messages.Clear();
        try
        {
            input.Click(1, 1, false, () => false, default);
            throw new Exception("Guard ignored");
        }
        catch (OperationCanceledException)
        {
            Check(messages.Count == 0);
        }
        foreground = 99;
        try
        {
            input.Click(1, 1, false, () => true, default);
            throw new Exception("Focus ignored");
        }
        catch (OperationCanceledException)
        {
            Check(messages.Count == 0);
        }
        foreground = 42;
        try
        {
            input.Click(1920, 1, false, () => true, default);
            throw new Exception("Bounds ignored");
        }
        catch (ArgumentOutOfRangeException)
        {
            Check(messages.Count == 0);
        }
        try
        {
            input.Click(1, 1, false, () => true, new CancellationToken(true));
            throw new Exception("Cancellation ignored");
        }
        catch (OperationCanceledException)
        {
            Check(messages.Count == 0);
        }
        var balanced = new GameWindowInventoryInput(
            new Native.Rect { Right = 1920, Bottom = 1080 },
            () => foreground,
            (_, message, buttons, position) =>
            {
                messages.Add((message, buttons, position));
                foreground = 99;
                return true;
            }
        );
        balanced.Click(1, 1, false, () => true, default);
        Check(messages.Count == 2 && messages[1].Message == 0x202);
        return checks;
    }
}
