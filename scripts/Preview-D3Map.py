"""Render a MapProbe snapshot as a diagnostic PNG (requires Pillow).

This is a world-space geometry preview, not a game minimap projection.
Only navigation cells with the historical AllowWalk bit are filled.
"""
import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw


def transform(point, origin, quaternion):
    x, y, z, w = quaternion
    if not all(math.isfinite(v) for v in quaternion) or abs(x*x + y*y + z*z + w*w - 1) > 0.01:
        raise ValueError("Scene rotation is not a unit quaternion")
    px, py, pz = point
    tx, ty, tz = 2*(y*pz-z*py), 2*(z*px-x*pz), 2*(x*py-y*px)
    return (px+w*tx+y*tz-z*ty+origin["X"], py+w*ty+z*tx-x*tz+origin["Y"])


def render(snapshot, output):
    assets = {asset["SceneSno"]: asset["Cells"] for asset in snapshot["assets"]}
    scenes = snapshot["scenes"]
    if not scenes:
        raise ValueError("The snapshot has no loaded scenes")
    worlds = {scene["WorldId"] for scene in scenes}
    if len(worlds) != 1:
        raise ValueError("Multiple worlds are loaded; filter the snapshot before previewing")
    min_x = min(s["WorldBoundsXY"][0] for s in scenes)
    min_y = min(s["WorldBoundsXY"][1] for s in scenes)
    max_x = max(s["WorldBoundsXY"][2] for s in scenes)
    max_y = max(s["WorldBoundsXY"][3] for s in scenes)
    width, height = 1200, 1000
    scale = min((width-100)/max(max_x-min_x, 1), (height-160)/max(max_y-min_y, 1))

    def screen(p):
        return 50+(p[0]-min_x)*scale, 100+(p[1]-min_y)*scale

    image = Image.new("RGB", (width, height), "#11151c")
    draw = ImageDraw.Draw(image)
    draw.text((30, 20), f"D3Vision: loaded navigation geometry | build {snapshot['fileVersion']}", fill="white")
    draw.text((30, 42), "Diagnostic world view. Full-floor coverage and in-game alignment are not verified.", fill="#d5af67")
    count = 0
    for scene in scenes:
        for cell in assets.get(scene["SceneSno"], []):
            if not cell["Flags"] & 1:
                continue
            a, b = cell["Min"], cell["Max"]
            corners = [(a["X"], a["Y"], a["Z"]), (b["X"], a["Y"], a["Z"]),
                       (b["X"], b["Y"], a["Z"]), (a["X"], b["Y"], a["Z"])]
            points = [screen(transform(p, scene["Origin"], scene["RotationQuaternion"])) for p in corners]
            draw.polygon(points, fill="#647fa3", outline="#8ba5c7")
            count += 1
    for scene in scenes:
        bounds = scene["WorldBoundsXY"]
        draw.rectangle([screen(bounds[:2]), screen(bounds[2:])], outline="#534d3c")
        draw.text(screen(bounds[:2]), str(scene["SceneSno"]), fill="#dbc18a")
    draw.text((30, height-35), f"{len(scenes)} loaded scenes; {count} walkable cell instances; {snapshot['capturedUtc']}", fill="white")
    image.save(output)
    print(f"Rendered {count} walkable cell instances to {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    render(json.loads(args.snapshot.read_text(encoding="utf-8")), args.output)
