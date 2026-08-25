# Changelog

All notable public changes to Kerbal Proportions are documented here.

## [Unreleased]

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

[Unreleased]: https://github.com/NickDeVeau/Kerbal-Proportions/compare/v2.5.0...HEAD
[2.5.0]: https://github.com/NickDeVeau/Kerbal-Proportions/releases/tag/v2.5.0
