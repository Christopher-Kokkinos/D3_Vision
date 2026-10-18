# Release notes

## v1.1.2 - autocast fixes and rapid inventory actions (2026-10-18)

- Fixed skill-dot hover activation, countdown feedback, dark-icon reading and keyboard input; added potion D autocast.
- Reduced Myriam guide obstruction and added F9 guide visibility toggle.
- Added opt-in three-second-hover DROP and Blacksmith salvage, including Ancient/Primal-preserving selective salvage.
- Added rapid inventory clicks and burst Kadala multi-buy, with foreground, identity and progress checks.
- Improved capture/OCR scheduling and inventory capture ownership.
- 461 automated checks passed. Rapid live destructive actions remain unverified; operations are sequential rather than atomic.

[Full v1.1.2 notes](docs/releases/v1.1.2.md) | [GitHub release](https://github.com/Christopher-Kokkinos/D3_Vision/releases/tag/v1.1.2)

## v1.1.1 - solo Stricken validation and clearer readouts

- Confirmed the observed proc estimate rising 0 to 16 in a controlled solo guardian
  fight and clearing after the guardian disappeared; confirmed worn Squirt stacks 0..10.
- Added a normalized capture replay and explicit ready/estimated/unknown Stricken states.
- Added ZIP/checksum generation to the installer build and faster isolated verification.
- Stricken remains an estimate, not an authoritative enemy stack count.

[Full v1.1.1 notes](docs/releases/v1.1.1.md) · [GitHub release](https://github.com/Christopher-Kokkinos/D3_Vision/releases/tag/v1.1.1)

## v1.1.0 - portrait buffs and skill-target alignment

- Added live CoE portrait glows/ring mode and Squirt necklace/stack indicators.
- Fixed three-second hover hit boxes and anchored feedback to calibrated skill artwork.
- Corrected buff indexing, player-slot mapping, guardian identity checks and ranged tracking.
- Included experimental Stricken tracking and bounded read-only recording; solo validation
  was pending at publication and is covered by v1.1.1.

[Full v1.1.0 notes](docs/releases/v1.1.0.md) · [GitHub release](https://github.com/Christopher-Kokkinos/D3_Vision/releases/tag/v1.1.0)

Earlier: [v1.0.0 - initial packaged release](docs/releases/v1.0.0.md).
