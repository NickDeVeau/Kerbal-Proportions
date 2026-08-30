# Changelog

All notable public changes to Kerbal Proportions are documented here.

## [Unreleased]

## [2.6.0] - 2026-08-30

### Added

- Non-destructive virtual accessory groups for otherwise unrelated/root-level
  mesh pieces, including automatic discovery through wearable-prop runtime
  metadata and manual creation from any multi-selection.
- Shared-pivot group move, rotation, and scaling without changing Unity parent
  transforms, plus group persistence in settings and standalone profiles.
- Independent draggable Hierarchy and Controls windows with persistent screen
  positions, leaving the flight view available for direct world editing.
- Standalone, shareable profile files under `PluginData/Profiles`.
- In-game profile import refresh while retaining backward compatibility with
  the legacy combined `profiles.cfg` library.
- A surface-relative gumball frame aligned to planetary up and the Kerbal's
  projected facing direction.
- Type-aware hover and selection colors for bones, meshes, and collider-bearing
  targets without making collider-only objects editable.
- Persistent bone, mesh, and collider filters shared by the hierarchy and
  viewport hover selection.
- Ragdoll-aware pose handoff that rebases CharacterJoint attachment anchors
  once when physics takes ownership, without replacing stock colliders.
- Automatic rig rediscovery when runtime suit accessories, wearable props,
  lights, renderers, or colliders are added or removed.

### Fixed

- Excluded global camera effects, trajectory lines, screen-space shadow helpers,
  and particle renderers from the editable mesh hierarchy.
- Removed the experimental embedded preview and its camera/input/rendering path
  so editing consistently uses the normal KSP world view.
- Added clearer vertical separation throughout every inspector tab.
- Prevented edited head scales from being applied twice for a frame when KSP
  synchronizes a Kerbal from ragdoll back to its animated rig.
- Accepted flat standalone profiles written by early 2.6 test builds and made
  future exports use a consistent named wrapper.
- Kept scale handles aligned with the target-local X/Y/Z components they
  actually modify, independent of the move/rotate surface-axis setting.
- Suspended continuous position and rotation writes while an EVA Kerbal is in
  ragdoll, preventing the editor from pulling against the physics solver.
- Held only configured bone scales through KSP's ragdoll recovery blend to
  prevent heads and other scaled parts shrinking before the stand-up completes.
- Distinguished KSP's animation-owned `st_recover` state from free ragdoll so
  profile position and height offsets resume at the start of standing up.
- Made rotation drags follow the selected ring's camera-facing signed angle,
  eliminating direction reversals when an axis points toward the camera.
- Prevented nearly camera-facing move/scale axes with collapsed screen-space
  handles from capturing an ambiguous drag.

## [2.5.1] - 2026-08-25

### Added

- Added separate read-only Stock and Humanoid presets; custom profiles remain
  user-owned and are never replaced by an update.
- Added soft compatibility with Benjee10 Historical Kerbal Suits wearable
  attachments so external helmets inherit their declared head-bone scale.

### Fixed

- Limited the stock toolbar button to flight and corrected launcher lifecycle
  cleanup so it no longer accumulates duplicate buttons in the VAB.

## [2.5.0] - 2026-08-24

First public GitHub release and the supported baseline for the current rig
editor.

### Added

- Live move, rotation, and per-axis scale editing for discovered Kerbal bones
  and mesh roots.
- An always-in-front viewport gumball with local and world-space operation.
- Predictive hover highlighting and viewport selection.
- Ctrl/Shift multi-selection, hierarchy selection, and mirrored left/right
  editing.
- Collapsible root-first transform hierarchy with search and persistent branch
  state.
- Animation-safe pose rotation and per-axis animation rotation strength.
- EVA and opt-in IVA rig support, including compatible internal overlays.
- Live portrait position, zoom, yaw, and pitch controls.
- Named profiles containing pose, motion, and portrait values.
- Migration support for settings and profiles created by the temporary V2 test
  package.

### Packaging

- Added clean GameData-style release archives, KSP-AVC version metadata, and
  proposed NetKAN metadata.
- Runtime-created settings and profiles are excluded from release archives.

[Unreleased]: https://github.com/NickDeVeau/Kerbal-Proportions/compare/v2.6.0...HEAD
[2.6.0]: https://github.com/NickDeVeau/Kerbal-Proportions/compare/v2.5.1...v2.6.0
[2.5.1]: https://github.com/NickDeVeau/Kerbal-Proportions/compare/v2.5.0...v2.5.1
[2.5.0]: https://github.com/NickDeVeau/Kerbal-Proportions/releases/tag/v2.5.0
