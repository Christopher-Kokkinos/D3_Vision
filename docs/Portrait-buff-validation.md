# Portrait buffs and skill targets

This update replaces the visual-only party strips with observed CoE element glows
or a ring symbol, Squirt necklace indicators, and an explicitly estimated Stricken
proc counter. The native reader retains the existing executable hash restriction
to Diablo III 2.8.0.99920 and uses read-only process access.

## Native attributes

The executable's descriptor ID is one greater than the ID packed into an attribute
key. For example, Item_Quality_Level has descriptor ID 384 and packed ID 383,
already validated by the loot reader. Buff_Icon_Count0 has descriptor ID 768,
therefore packed ID 767 (`0x2FF`). Keys are `(powerSno << 12) | (767 + icon)`.

- CoE power: 430674. Icon 0 establishes presence. Icons 1 through 7 correspond to
  Arcane, Cold, Fire, Holy, Lightning, Physical and Poison. Only one class-valid
  element can light a portrait; an ambiguous transition is unknown.
- Squirt power: 483552. Icon 5 (packed attribute `0x304`) is the 0..10 stack count.
  Native item hash for `P66_Unique_Amulet_010`, inventory owner and positive equipment
  slot identify worn gear. Backpack, ground and other-player items do not establish
  wearing. A confirmed worn necklace with default-zero stacks has a red cross.
  Remote equipment is often unavailable. Observed positive stacks or a positive
  icon-0 power-presence marker establish the power independently of worn gear.
  Zero stacks with confirmed power presence keep the icon and red cross visible.
  `*` means wearing is not confirmed, including an available cubed power. Missing
  gear, stacks and power presence remain unknown instead of assuming zero.
- Stricken power: 428348. Icon 0 establishes presence; rising edges of icon 2
  identify observed proc events. They do not expose exact enemy stack counts.

Live checks on 17 October 2026 observed a full Barbarian CoE rotation
(Cold, Fire, Lightning, Physical) and the independent Crusader rotation
(Fire, Holy, Lightning, Physical). Party Squirt stacks of 10 and a Demon Hunter's
Stricken proc icon were observed. Local player slot 3 was validated; automatic
portrait order puts that hero first, with explicit slot overrides for other frames.
A later combat sample observed Stricken proc edges and Squirt recovery from
6 through 10 stacks. Squirt red-cross loss and boss proc-count accuracy
remain unverified live; the boss was no longer detected as a live isolated target
by the end of the combat sample. These cases retain synthetic coverage.

Stricken `~` counts start from a baseline, require one identified living boss within
120 world units. Pets, scenery and ordinary enemies do not block the estimate.
Procs can hit other enemies, so the total cannot be attributed exactly to the boss, and reset on missing
players, a different boss/actor/world/process, focus loss, or sampling gaps above
350 ms. The first already-active proc is not counted. No attack count or damage
event is presented as an exact stack measurement. All portrait readings expire
after 750 ms; absent/out-of-world players have no glow.

## Hover geometry

The only three-second toggle targets are calibrated skill-artwork boxes. Movable
widget positions cannot arm skills. Legacy calibration must be saved again so it
records capture mode and monitor. Overlapping boxes, changed aspect ratio, changed
capture mode/monitor, stale geometry, and moved/resized window bounds reject hover.
Locked VisualRead countdowns, hover feedback and AUTO outlines anchor to each
calibrated artwork box instead of the movable six-column fallback panel.

Windows Graphics Capture reports physical screen bounds only when its pixel size
matches the window's DWM frame or selected monitor. This metadata is carried with
each captured frame, and mouse positions are transformed before hit testing.
The live borderless 1920x1080 capture passed this consistency check. Mixed-DPI
hardware and live hover toggling without firing skills still need validation.

## Checks and previews

```powershell
dotnet run --project tests/D3Vision.Tests -c Release
dotnet run --project tests/D3Vision.Tests -c Release -- --portrait-preview
dotnet run --project tools/D3Vision.MapProbe -c Release -- artifacts/buffs.json --buffs --buff-watch
```

`--portrait-preview` renders illustrative active, inactive, unknown and remote-power
states to `artifacts/portrait-buffs-preview.png`. It is not a game screenshot.
`--buff-details` is a bounded read-only diagnostic for native icon values/equipment;
probe captures stay in the ignored artifacts directory.

## Fast guardian capture and range correction

The bounded background recorder captured Rat King (actor SNO 426943, rarity 7)
alive across six samples spanning approximately 0.43 seconds. The Demon Hunter
reported Stricken equipped but no proc during those samples. The guardian appeared
in the diagnostic actor set within 120 units of a Stricken wearer but was excluded
by the original 40-unit counter radius. Tracking now uses the same 120-unit radius.
Pets, breakable scenery and other positive-HP objects no longer block encounter
selection. Explicit guardian actor identities supplement the rarity flag and are
also used for minimap boss classification; UI actors and guardian minions are excluded.

This confirms live guardian capture, not a live counter increment. Proc attribution
and final enemy stack totals remain estimates, especially when other enemies are
present. The five-minute, 16-MB-capped recorder only reads memory, writes local ignored
diagnostics, and does not send game inputs. Use `--record-buffs` to run it explicitly.

## Controlled solo capture

A subsequent solo Barbarian run on 17 October 2026 captured guardian actor SNO
353535 with Stricken equipped. Across 124 encounter samples spanning approximately
12.3 seconds, the observed proc estimate increased from 0 to 16. It cleared on the
first sample after guardian disappearance, including while a proc flag lingered.
The same run observed a confirmed worn Squirt necklace at every stack value 0..10.

The normalized 144-sample encounter (without process IDs, player names, coordinates,
or other raw game data) is preserved in the test fixture
`tests/D3Vision.Tests/Fixtures/solo-stricken-procs.json`. Its replay checks the recorded
estimate sequence, peak and disappearance reset. These results validate observed-proc
tracking and native Squirt state readings; they do not establish sixteen exact enemy
stacks, exclude missed fast events, or verify stack attribution in multi-enemy fights.
The recorder was stopped after the capture. No game inputs were generated.
