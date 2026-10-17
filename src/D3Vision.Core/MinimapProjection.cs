namespace D3Vision.Core;

public readonly record struct MinimapViewport(double Left, double Top, double Size);

/// <summary>Native top-right minimap layout in game client pixels.</summary>
public static class MinimapProjection
{
    public static MinimapViewport Viewport(double clientWidth, double clientHeight)
    {
        double scale = clientHeight / 1200;
        return new(clientWidth - 1 - 377 * scale, 54 * scale, 350 * scale);
    }

    // The standalone panel uses half-height isometric coordinates. The native
    // minimap rotates 45 degrees without this vertical compression.
    public static MapPoint Position(
        MapPoint panelPoint,
        MapPoint player,
        double size,
        double calibration = 1
    )
    {
        const double diagonal = 0.7071067811865476;
        double scale =
            size / 350 * (double.IsFinite(calibration) ? Math.Clamp(calibration, .1, 10) : 1);
        return new(
            size / 2 + (panelPoint.X - (player.Y - player.X) * diagonal) * scale,
            size / 2 + (panelPoint.Y * 2 - (player.X + player.Y) * diagonal) * scale
        );
    }
}
