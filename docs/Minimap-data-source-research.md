# Minimap data source research

Researched 17 October 2026. Source inspection only; no third-party reader was executed and no current-game compatibility was tested.

## Result

Public C# scene readers and navigation geometry code exist. They could reduce the work compared with starting from scratch or reconstructing TurboHUD's proprietary core. This search did not establish a maintained, ready-to-use reader compatible with the user's installed Diablo III build, or a documented TurboHUD geometry export suitable for D3Vision.

## Strongest leads

| Source | Verified contents | Limitation |
| --- | --- | --- |
| [Enigma.D3, Lat0ur fork](https://github.com/Lat0ur/Enigma.D3) | Memory-model, asset and MapHack projects. Its [x64 symbol table](https://github.com/Lat0ur/Enigma.D3/blob/master/Enigma.D3.MemoryModel/SymbolTable.X64.cs) explicitly targets `2.6.1.48432` and contains scene/object-manager addresses and field offsets. | Historical, version-specific addresses. No verified support for the installed game. |
| [Enigma.D3, randle51 mirror](https://github.com/randle51/Enigma.D3) | README describes out-of-process Diablo III structure reading. Its [x64 symbol table](https://github.com/randle51/Enigma.D3/blob/master/Enigma.D3.MemoryModel/SymbolTable.X64.cs) targets `2.6.1.47710`. | Older reference implementation, not a current compatibility claim. |
| [Nav.D3](https://github.com/ZenDraL/Nav.D3) | Navigation mesh and live D3 viewer using Enigma. [SceneSnoNavData.cs](https://github.com/ZenDraL/Nav.D3/blob/master/Nav.D3/SceneSnoNavData.cs) reads scene asset navigation cells and movement flags. [SceneData.cs](https://github.com/ZenDraL/Nav.D3/blob/master/Nav.D3/SceneData.cs) reads scene identity and mesh bounds. | Still depends on a functioning Enigma reader. Repository declares GPL-3.0; assess licensing before incorporating code. |
| [Public TurboHUD files](https://github.com/cpiehl/TurboHUD) | Interfaces and plugins, including scene-reveal controls. | No standalone core scene reader or documented geometry export established. Similar boundary to the supplied archive. |

## Implications for D3Vision

These sources show how older clients exposed scene instances and how navigation cells could become map geometry. They do not establish that every part of a generated rift is available before exploration in today's client. That must be checked separately; loaded geometry and the complete floor layout are different requirements.

The most practical next investigation is to compare the installed executable's exact version with available reader symbol tables, then audit scene enumeration, asset access and coordinate transforms. A mismatch requires updated addresses and potentially updated structure layouts; historical source cannot guarantee that reverse engineering is eliminated.

Once a compatible data source is verified, D3Vision can keep it behind a map-data service and render its geometry in the existing overlay. Rendering alone cannot supply unseen geometry from screen capture.

No app code was changed for this research. See [the supplied archive review](TurboHUD-archive-review.md) for the separate static inspection of the user's TurboHUD files.
