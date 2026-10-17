# TurboHUD archive review

Inspected on 17 October 2026. This is a static assessment, not a working integration or a compatibility test.

Archive: user-supplied `turbo-3.zip` (not included in this repository).

SHA-256: `79CF9246FD250559AE681280FCA2D24AB213F9DD4CE976F897B1CD027EBB985D`

The archive contains 8,261 entries. 738 `.cs`/`.x` source-like entries were scanned for scene, navigation, memory-reader and data-export references. Selected interfaces were read in full. PE/CLR metadata was inspected without loading or executing the supplied assemblies. No bundled executable, installer, script, security-exclusion instruction or nested payload was executed. Instructions inside the archive were treated as file content, not as user authorization.

## Relevant findings

Paths below are relative to `turbo-3/` inside the ZIP.

| File | What it provides |
| --- | --- |
| `TurboHUD/interfaces/controllers/ISceneRevealController.cs` | Switches for the core's minimap/map reveal, opacity, clipping and brushes. It does not contain the reveal implementation or return map geometry. |
| `TurboHUD/interfaces/controllers/IController.cs` | Access to TurboHUD's `SceneReveal`, `Game`, `Render` and other controllers while a plugin is hosted by TurboHUD. |
| `TurboHUD/interfaces/data/IScene.cs` | Scene identity, navigation-mesh ID and bounds. These interface declarations alone do not supply runtime scene instances or walkable cells. |
| `TurboHUD/interfaces/controllers/IGameController.cs` | Players, actors, scene hints and rift statistics. The supplied interface does not expose a collection of complete map navigation geometry. |
| `TurboHUD/interfaces/controllers/IRenderController.cs` | Minimap coordinates, scale and drawing APIs supplied by TurboHUD. This is not a standalone renderer/data reader for another process. |
| `TurboHUD/pluginsX/glq/GLQ_PluginEnablerOrDisablerPlugin.cs` | Enables the existing core feature with `Hud.SceneReveal.MapEnabled` and `Hud.SceneReveal.MinimapEnabled`. This delegates the work to TurboHUD. |
| `TurboHUD/plugins/Default/Minimap/SceneHintPlugin.cs` | Draws labels from `Hud.Game.SceneHints`; it does not discover hidden geometry. |
| `TurboHUD/data/scene.bin`, `scene_cell.bin`, `scene_square.bin` | Binary scene-related assets. No decoder/schema for reconstructing the current live rift was identified in the examined source. Static assets alone do not establish the current generated layout. |
| `TurboHUD/TurboHUD.exe` | Managed assembly version `26.6.26.0`, assembly name `cf7cf57e2`, 1,958 type definitions. Metadata includes `ReadProcessMemory` and `OpenProcess` native imports. Relevant readable scene/navigation/export type names were not found by the metadata name scan. This indicates memory-reading functionality exists in the compiled core, not that it is available through a reusable public API. |
| `SkeletonD-cache-2.8.1.101167-b53562469a66cdf8f840a564996785fc.bin` | A version-labelled binary cache. Its contents and compatibility have not been established. |

`TurboHUD/TurboMGR.exe` reports CLR metadata but the static metadata reader could not parse it. `TurboHUD_LightningMOD.exe` has no CLR metadata. `Turbo.cnmgr.dll.zip` could not be opened as a standard ZIP. These results do not establish what those files do; they were not executed or unpacked through a loader.

## Implication for D3Vision

This archive helps document the plugin contract and the existing TurboHUD reveal switches. It has not supplied a verified, drop-in, independent map-data provider for D3Vision. Copying the switch-setting plugin would still require TurboHUD's running core and would enable TurboHUD's own reveal rather than implement D3Vision's.

A separate D3Vision map renderer requires current-world identity, player coordinates, scene placements and walkable cell/polygon geometry, including undiscovered portions that are actually available to the reader. No documented standalone export of that complete dataset was found in the supplied source. Binary imports are evidence of a reader, not enough to determine its structures, offsets, initialization or compatibility.

The practical next inputs would be either the core reader's source/SDK or a documented geometry export from a running, compatible TurboHUD installation. A companion plugin could be investigated for information that the plugin interfaces expose, but full-map geometry export is not established by the interfaces reviewed here. A standalone implementation would still need work on the compiled reader and validation against the installed Diablo III version.

No minimap-reveal feature was added to D3Vision during this review, and none of its existing runtime features or settings were changed.
