# Diablo III map reader and experimental nearby-map panel

Investigated on 17 October 2026 against the user's running x64 game. This is a research milestone, not a finished full-floor reveal implementation.

## Verified result

A read-only C# 12/.NET 8 reader enumerates active scene records, resolves their navigation geometry, and reads the client scene-reveal cache. It is now shared by the desktop overlay and the JSON probe. The optional nearby-map widget renders available walkable cells as a separate isometric panel and cached scene extents as dashed outlines.

The widget defaults to disabled. Enable **NEARBY MAP (EXPERIMENTAL)** in F12 settings, then Apply and save. The development session's widget has been enabled and the updated default Release executable is running. F11 controls editing/click-through as before. Existing widget positions are preserved during migration.

Coverage is limited to geometry available in loaded scenes and cached scene extents. This is not complete unexplored-floor reveal, and the panel is not projected onto the game's existing minimap. The architecture supports future data-source work without changing rendering or widget settings.

The running executable is `C:\Program Files (x86)\Diablo III\x64\Diablo III64.exe`, version **2.8.0.99920**. SHA-256: `753d5f79d12b035b7268fdb255b5b2502ec858d149b6ebb62969a6e92dd23cc4`.

The PE numeric fixed-version field truncates the build to 34384; the string version and installation `.build.info` both identify build 99920. The probe gates compatibility by exact executable hash and checks the live iterator instruction, not by that truncated number.

## Discovery path

1. Inspect the installed PE without executing it. The `.text` section contains readable native instructions and `.rdata` contains scene/navmesh diagnostic strings.
2. Follow the `Client Scenes:` and `Server Scenes:` string references to the diagnostic routine at RVA `0x1CF490`.
3. Follow its call to the scene iterator at RVA `0x773AA0`, then the container iterator at `0x4BF710` and slot iterator at `0x4C09C0`.
4. The slot iterator establishes the active-slot bound, record stride and inactive sentinel. The wrapper adds eight bytes before returning a scene pointer.
5. Scene construction at `0x773C60` and bounds calculation at `0x7758F0` establish scene transforms and the world-space rectangle.
6. Scene asset lookup at `0x6FE1C0` / `0x6FBA60`, with SNO-to-handle lookup at `0x6FD3A0`, establishes the asset table path. Asset headers returned the exact requested SNOs in live tests.
7. Read the asset's navigation-cell array with validated count, byte length, coordinates and array identity.

The older [Enigma scene model](https://github.com/Lat0ur/Enigma.D3/blob/master/Enigma.D3.MemoryModel/Core/Scene.cs) and [Nav.D3 navigation-cell reference](https://github.com/ZenDraL/Nav.D3/blob/master/Nav.D3/SceneSnoNavData.cs) informed the investigation. No third-party reader binary was executed or source implementation incorporated. The walkable-bit interpretation remains based on the historical navigation-cell reference and geometric inspection.

## Current build profile

All global addresses below are **RVAs**, added to the actual loaded module base to accommodate ASLR. Offsets here are specific to the inspected executable.

| Item | Location |
| --- | --- |
| Object manager pointer | module + `0x16F5250` |
| Scene container pointer | object manager + `0xAA0` |
| Highest scene slot, unsigned 16-bit | container + `0x108`; `0xFFFF` means empty |
| Scene array pointer | container + `0x120` |
| Record stride | `0x7B8` |
| Unused record marker | signed 32-bit `-1` at record + `0x00` |
| Scene ID / world ID | record + `0x08` / `0x0C` |
| Scene SNO | record + `0xEC` |
| Quaternion x/y/z/w | record + `0xF0` through `0xFC` |
| Origin x/y/z | record + `0x100` through `0x108` |
| World XY min/max | record + `0x170` through `0x17C` |
| Scene asset group pointer | module + `0x1708970` |
| Asset container pointer | group + `0x20` |
| Asset slot array pointer | asset container + `0x120` |
| Asset slot stride / resource pointer | `24` bytes / slot + `0x10` |
| Direct SNO lookup limit / array pointer | module + `0x1704C48` / `0x1704C50` |
| Resource header SNO | resource + `0x00` |
| Navigation cell count / pointer / byte length | resource + `0x180` / `0x188` / `0x194` |
| Navigation cell stride | `32` bytes |
| Cell minimum / maximum XYZ | cell + `0x00` / `0x0C` |
| Cell flags | unsigned 16-bit at cell + `0x18` |

The resource table handle's low 18 bits select the slot. The slot identity and asset's own SNO are validated before reading geometry. SNOs outside the direct lookup range are reported as missing; the alternate hash lookup is not implemented.

Two other exported scene fields are explicitly marked candidate values. The pointer at record + `0x190` has not been established as a navigation pointer and is not dereferenced by the reader.

## Live observations

**Session attribution confirmed:** the user subsequently confirmed being dead inside the rift, stationary with slightly more map explored. That matches the captured death screen. The stationary follow-up (`stationary-explored.json` / `.png`) returned the same five scene identities, 341 cells across three unique assets, 64 walkable cell instances, and no missing assets as the after-movement snapshot. Compared with the earlier entrance snapshot, two scene identities were added and two removed. This validates repeatable extraction from the confirmed game state; it does not establish unexplored-floor coverage. A town portal being open alone would not verify that the destination floor's geometry is available in this container.

- Initial state: 9 scene records.
- After entering the rift: 5 active scenes, two unique assets containing 122 cells, 17 walkable cell instances.
- After movement: 5 active scenes, three unique assets containing 341 cells, 64 walkable cell instances. A real corridor asset replaced a filler asset while remaining in the same world.
- The actual game capture confirmed the Nephalem Rift objective and a Horazon's Journey I floor. The PNG is a world-space diagnostic view, not an aligned in-game minimap overlay.
- The scene container's changing contents are evidence of active/streamed geometry. These observations do **not** prove that the complete unexplored floor is present in this container. The earlier entrance-only capture must not be presented as the entire rift.

## Scene-reveal cache investigation

Static inspection with ILSpy 9.1 identified a second scene source in the supplied TurboHUD core: 88-byte scene-reveal records. The inspected path combines active scene records and those cached records by world/scene identity, then draws navigation cells from scene assets. The original binaries and bundled scripts were not executed. Decompiled output stays under ignored local artifacts; application code was independently written around validated record layouts.

The current client's localized area name was found at module RVA `0x1672A70`. The preceding pointer, RVA `0x1672A68`, leads to the level-area object. Its scene-reveal list has:

| Item | Offset |
| --- | --- |
| First / last node pointer | area + `0x20` / `0x28` |
| Node count | area + `0x30` |
| Allocator item size | area + `0x40`, validated as 88 |
| Allocator marker | area + `0x68`, validated as `0x600DF00D` |
| Scene SNO / scene ID / world ID / texture SNO | node + `0x00` / `0x04` / `0x08` / `0x0C` |
| XY bounds | node + `0x10` through `0x1C` |
| Previous / next link | node + `0x48` / `0x50` |

Live traversal returned 17 records across worlds, eight belonging to the current rift. Three were outside the five active scenes; all three were filler tiles with no added walkable geometry. This is a working second data source, not evidence that the entire unvisited floor is cached.

The reader validates allocation metadata, bounds, counts, link consistency, cycles and container identity. It does not read cached fog-mask buffers or write their visibility flags.

## Implementation

- `D3Vision.Core/RiftMap.cs`: `IMapDataSource`, snapshot models and render geometry models.
- `D3Vision.Infrastructure/DiabloMapReader.cs`: hash-gated native reader, with cached executable verification per process lifetime.
- `D3Vision.Infrastructure/SceneRevealCache.cs`: bounded cache traversal.
- `D3Vision.Infrastructure/ReadOnlyProcess.cs`: bounded process reads and injectable read interface for tests.
- `D3Vision.Services/RiftMapGeometry.cs`: scene transforms, walkability filtering, world isolation and isometric projection. Cached-only scenes get extents; their rotations are not fabricated.
- `D3Vision.Rendering/WidgetVisual.cs`: map drawing with scale-to-fit geometry, opacity and custom colour.
- `D3Vision.UI`: optional widget settings, preserved placement and one-second asynchronous updates. Disabling the widget stops map reads. Missing/unsupported game data clears stale geometry and displays a status.
- `tools/D3Vision.MapProbe`: command-line export using the same reader. Its diagnostics remain separate from the application.

Artifacts are under `artifacts/reverse-engineering/`: `binary-report.json`, `rift-ready.json`, `rift-second.json`, `after-movement.json`, and corresponding PNGs. They are local generated output and are excluded by the workspace's existing `artifacts/` ignore rule.

## Run

From `C:\Projects\D3_Buddy`, with the matching Diablo III client running:

```powershell
dotnet run --project tools/D3Vision.MapProbe -c Release -- artifacts/reverse-engineering/map.json
python scripts/Preview-D3Map.py artifacts/reverse-engineering/map.json artifacts/reverse-engineering/map.png
```

The preview script requires Pillow. The game reader itself has no third-party runtime packages.

For repeatable static PE inspection in a separate Python environment:

```powershell
python -m venv artifacts/reverse-engineering/venv
artifacts/reverse-engineering/venv/Scripts/python.exe -m pip install capstone==5.0.9 pefile==2024.8.26
artifacts/reverse-engineering/venv/Scripts/python.exe scripts/Inspect-D3Binary.py 'C:\Program Files (x86)\Diablo III\x64\Diablo III64.exe' --output artifacts/reverse-engineering/binary-report.json
```

An optional `--contexts` argument on MapProbe performs additional read-only thread/TLS diagnostics. This is experimental, tied to x64 TEB offsets and the inspected build, and is not needed for scene extraction.

## Checks and limits

The probe requests process query/read access only. It does not write game memory, inject code, suspend the game, change security settings, or execute the supplied TurboHUD archive. It has bounds on slots, coordinates and total bytes read, validates quaternion lengths, and rechecks scene/container/asset identities before exporting. These checks reduce inconsistent live snapshots; they do not make cross-process reads atomic. Retry if a load or transition invalidates a snapshot.

Validation completed: solution and probe Release builds with zero warnings/errors; 72 checks including malformed/cyclic/truncated reveal caches, transforms, world isolation, missing-asset handling and layout migration; repeated live captures; PNG inspection of the real WPF renderer; settings activation and persistence; F12 settings opening; map-enabled graceful shutdown with exit code zero; relaunch of the normal Release executable.

Short live performance samples with capture and map enabled were 4.38% CPU / 239.1 MB peak working set initially and 3.74% / 229.2 MB after warm-up (CPU normalized across logical processors). A separate ten-capture benchmark of map reading plus geometry construction at one-second intervals measured about 0.38% CPU. These samples do not establish steady-state gameplay performance or isolate every rendering cost. The original <2% CPU and <200 MB targets remain unmet for the whole application.

Still required for complete rift vision: locate any complete planned/unloaded scene layout, establish what data is available before exploration, test additional rift types and Greater Rifts, validate game minimap projection/player position, and meet application performance targets. The data source supports only the exact inspected executable hash; unknown builds fail closed. The probe remains outside the main solution as a research utility, while the shared reader and optional panel are part of the application.


## Native minimap alignment (2026-10-17)

The top-right minimap now has a player-relative mode, visually checked against a stationary player in Tormented Illusion I at 1920x1080. Native viewport dimensions use client-height / 1200: a 350-unit square at right inset 27 and top inset 54. World XY is rotated 45 degrees without the standalone panel's vertical compression. Geometry extents never control native scale. WPF converts physical client coordinates through PointFromScreen; mixed-DPI hardware remains untested. Scale and offset calibration persist in settings. The expanded Tab map is not aligned by this mode.

Verified current-build player chain: ObjectManager global RVA 0x16F5250; local input/player singleton root+0xB28; local index at +0; storage inline root+0x7B8; PlayerDataManager pointer storage+0x140; PlayerData entries manager+0x60 with stride 0xE7F0 and actor ID +4. ACD manager pointer storage+0x198, container pointer manager+0, block table container+0x120, block shift container+0x168. Actor low-16-bit index selects the block and 0x3C0-byte record. Native getter RVA 0x73A0C0 returns actor+0xC8 (WorldPlace); XYZ is actor+0xD8 and world ID actor+0x118. Actor identity, world identity, index/shift bounds and finite coordinates are validated before rendering. The earlier guessed inline LocalData RVA was rejected and is not used.

Player reads run at 10 Hz only while the map widget is enabled; scene geometry reads remain 1 Hz. Reads require the previously hash-validated process ID and process start time. No writes or injected code are used. Process/world changes clear aligned rendering until validated data returns. Console checks include projection, viewport scaling, and synthetic actor identity/world/index/container/coordinate failures (83 total checks).


## Elite and dropped-item markers (2026-10-17)

Actor markers use the validated ACD container at storage+0x198 (pointer to manager; manager+0 is container). Bounded block reads use 0x3C0 stride and the existing container shift/table. ACD +0xB0 is a GameBalance handle type, not an actor-type enum: -1 for monsters without a balance handle, 2 for items and 7 for heroes; rarity +0xB8 maps 1 champion, 2 rare, 3 rare minion, 4 unique, 7 boss. World +0x118 must match the scene snapshot. Floor inventory location is -1 at +0x124; equipped, stash, socket and inventory items are excluded. HP and deleted flags filter dead or removed elites. Selected actors are rechecked before publication; marker snapshots replace rather than accumulate old actors.

Native accessor RVA 0x76E730 resolves the actor attribute group ID at +0x130. Storage+0x180 -> manager+0x90 is the group container, stride 0x12E8. Attribute keys pack the ID in low 12 bits and default parameter 0xFFFFF above it. Verified IDs: quality 383 (9 legendary), Ancient_Rank 413 (0 normal, 1 Ancient, 2 Primal), HP current 115 (float), Deleted_On_Server 662. Group flags at +4 choose inline 256-bucket or external 512-bucket lookup; entries are next pointer, packed key at +8, 32-bit value at +12. Chain lengths/cycles and group identity are checked. Native read helpers inspected: 0x77A320, 0x77D270, 0x77D300, 0x77D4F0, 0x77D540. No native functions are executed by D3Vision.

Set membership is read from the loaded game balance item row, rather than guessed from actor names. Global manager RVA 0x16F64C0 -> map array, table type 2, table stride 128. Hash is FNV-1a over the four little-endian GBID bytes. Hash nodes contain GBID +8, balance SNO +12, row index +16. GameBalance asset group global RVA 0x1714180 uses the validated SNO lookup/container pattern. Asset type at +12 must be 2; byte length +0x2C, row pointer +0x30, row stride 0x580. Row GBID +0x100 must match and set GBID +0x174 must be neither 0 nor -1. These fields are corroborated by native balance accessor 0x745380 and set tooltip 0x77EEC0. Unavailable definitions and unknown ranks are suppressed, not guessed.

Live checks identified the user's adjacent normal set and Ancient set as separate actor records with ranks 0 and 1, then observed their removal on pickup. A subsequent normal legendary drop resolved as non-set rank 0. A subsequent live blue champion (rarity 1, balance type -1, positive HP) validated the corrected elite filter. Live Primal-drop validation remains pending. Synthetic checks cover all elite rarities, loot tiers, dead/inventory/other-world exclusions, pickup, toggles, invalid identities, malformed tables and cyclic chains. The generated WPF preview includes all symbols and is explicitly illustrative. The reader-only 10-second benchmark with scene 1 Hz, markers 5 Hz and player 10 Hz measured about 0.89% machine CPU and 35 MB working set; this excludes capture/OCR and WPF and is not a full-app performance guarantee. Full application startup sampled about 216 MB working set, above the original memory target.


### Elite filter correction

The first marker implementation incorrectly assumed +0xB0 was ActorType and required value 1 for monsters. A live champion with ACD ID 0x84A60151 (signed IDs are valid) showed balance type -1, rarity 1 and positive HP. The filter now accepts rarity-bearing records with no balance handle (-1), excluding item and hero handles. Native item balance accessors corroborate the field interpretation. Regression fixtures use the actual handle type and explicitly reject the former type-1 assumption. Release build passes 110 checks. The existing running application must be restarted to load the corrected Core/Infrastructure assemblies.


## Myriam local-class detection and reference source

The existing hash/start-time gated player chain also reads local ACD actor SNO +0x90. Explicit base hero SNO pairs map to the seven classes; unknown models produce an unavailable message rather than a default class. Verified live for the user's Barbarian (SNO 3301). The existing Windows OCR feed recognised ENCHANT plus Select a Property to Replace in the supplied live screen. The guide reuses this feed, so it does not allocate another capture session. Detection has a freshness timeout and clears on focus/settings/visibility changes. Outstanding requests are cancelled on dismissal or shutdown and request generations prevent stale class results from appearing.

Build data comes from Blizzard's public EU seasonal normal solo-GR HTML pages and item-profile tooltips. The current season is read from the official navigation (40 at validation). Live retrieval found rank-one Steingoblin's profile unavailable and used rank-two Wizbane, explicitly labelled rank 2. Tooltip hero IDs must match the selected leaderboard profile. All 13 gear slots appear; the profile did not publish a main-hand item, and that slot is labelled unavailable. Primary affixes exclude secondary affixes and Caldesann augments; socket count is included. These are observed reference rolls, not computed optimal enchant recommendations. Published current gear can differ from the loadout at the ranked clear.

The provider caps responses at 2 MB, uses HTTPS on the official EU host, bounds fallback to the first 20 ranks and limits tooltip concurrency to three. Fifteen-minute per-class caches persist in versioned JSON files; offline data up to 24 hours old is explicitly labelled. Native WPF renders plain text only. No external page instructions or scripts are executed. Validation includes fake-HTTP rank association/fallback, identity mismatch, offline expiry/cancellation, class mapping, enchant detection, item parsing, socket counting and cache behavior (172 total checks). The centre-screen WPF layout was rendered over the captured Enchant screen as a technical preview; the user's currently running older application still requires a restart to load this feature.
