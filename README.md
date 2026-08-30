# Kerbal Proportions

[![Latest release](https://img.shields.io/github/v/release/NickDeVeau/Kerbal-Proportions?display_name=tag)](https://github.com/NickDeVeau/Kerbal-Proportions/releases/latest)
[![SpaceDock](https://img.shields.io/badge/SpaceDock-download-2f6f9f)](https://spacedock.info/mod/4513)
[![KSP 1.12.5](https://img.shields.io/badge/KSP-1.12.5-2f6f9f)](https://www.kerbalspaceprogram.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Kerbal Proportions is a live visual rig editor for Kerbal Space Program 1. It
lets you reshape EVA and IVA Kerbals in flight by moving, rotating, and scaling
the bones and mesh roots already present in the active model. No replacement
Kerbal model or texture pack is required.

The current release is **v2.6.0**, tested with **KSP 1.12.5**.

## Features

- Edit discovered Kerbal bones and mesh roots with an always-visible viewport
  gumball or exact numeric values.
- Arrange independent draggable Hierarchy and Controls windows around the
  flight view; their positions persist between scenes and launches.
- Select and manipulate targets directly on the Kerbal in the normal world view.
- Search and browse targets in a collapsible hierarchy that follows the actual
  runtime rig.
- Hover highlighting, Ctrl multi-selection, Shift range selection, whole-branch
  selection, and mirrored left/right editing.
- Type-aware highlighting for bones, rendered meshes, and collider-bearing
  targets, including a matching hierarchy legend.
- Persistent type filters that apply to both viewport hover selection and the
  hierarchy while promoting visible descendants of filtered-out parents.
- Move, rotate, or scale several related bones together without compounding
  nested scale changes.
- Create persistent virtual accessory groups from unrelated mesh roots; group
  gumballs rotate and scale the complete assembly around a shared pivot without
  reparenting mod objects. Compatible wearable props can be grouped
  automatically, and group definitions travel with profiles.
- Switch between target-local axes and a surface frame whose up axis points
  away from the current body and whose forward axis follows the Kerbal.
- Reduce stock animation rotation independently on each local axis, or use
  animation-safe pose rotation to layer a correction over the rest pose.
- Edit EVA and IVA rigs, including IVA overlays used by compatible internal-view
  mods. IVA editing is intentionally opt-in.
- Adjust portrait framing, zoom, yaw, and pitch without moving a Kerbal out of
  its seat.
- Save, load, import, and delete named pose, motion, and portrait profiles as
  standalone shareable files while keeping the current edit across scene
  changes and quickloads.
- Rediscover removable suit accessories and wearable props at runtime so saved
  edits can match newly recreated instances after reattachment.

Kerbal Proportions changes the rendered rig. It does not replace Kerbal meshes,
change vessel physics, or alter Kerbal statistics.

## Installation

1. Close KSP.
2. Download `KerbalProportions-v2.6.0.zip` from the
   [latest GitHub release](https://github.com/NickDeVeau/Kerbal-Proportions/releases/latest)
   or [SpaceDock](https://spacedock.info/mod/4513).
3. Extract the ZIP into the KSP installation folder and merge its `GameData`
   folder with KSP's existing `GameData` folder.
4. Confirm that the DLL is located at:

   ```text
   Kerbal Space Program/GameData/KerbalProportions/Plugins/KerbalProportions.dll
   ```

If you previously tested the temporary `KerbalProportionsV2` package, remove
that old folder after copying its `PluginData` somewhere safe. Do not leave both
DLLs installed.

The release archive does not contain a personal `settings.cfg` or
`profiles.cfg`, so installing an update will not overwrite your edits. The mod
creates and updates those files under
`GameData/KerbalProportions/PluginData` while KSP is running.

## Basic use

1. Enter flight with an EVA Kerbal or a vessel containing crew.
2. Select the Kerbal Proportions button in KSP's stock toolbar.
3. Select a target in the hierarchy or click a highlighted body part in the
   viewport.
4. Drag the gumball, or enter exact values in the **Pose** tab.
5. Select **Save current** to persist the active settings, or use the
   **Profiles** tab to save a named preset.

| Action | Control |
| --- | --- |
| Move | `W` |
| Rotate | `E` |
| Scale | `R` |
| Add or remove a target | Ctrl-click |
| Select a visible range | Shift-click |
| Select a target and its descendants | **Select branch** |
| Add left/right counterparts | **Mirror** |

The **Motion** tab controls how strongly stock animation rotation affects the
selected targets on each local axis. A value of 100% preserves stock motion;
0% holds that axis at its rest orientation. This is useful when a changed body
shape makes a walk cycle look too wide or exaggerated.

The **Portrait** tab modifies only KSP's portrait cameras. The **Profiles** tab
stores pose, motion, and portrait values together. General editor preferences
remain in the current settings file rather than in each named profile.

Each newly saved profile is a standalone `.cfg` file in
`GameData/KerbalProportions/PluginData/Profiles`. To import one, copy it into
that folder and select **Refresh imports** in the Profiles tab. Older combined
`PluginData/profiles.cfg` libraries remain readable.

Surface axes apply to move and rotate. Scale handles remain target-local so the
red, green, and blue handles always modify the displayed X, Y, and Z scale
components respectively; arbitrary surface-axis scale would require mesh shear.

Moving or rotating a bone that carries a ragdoll rigidbody still moves that
physics transform. On entry to EVA ragdoll, the mod hands the last edited pose
to physics once, rebases the active CharacterJoint attachment anchors, and then
stops writing position and rotation until recovery. Collider-bearing targets
remain visible for identification, but the mod does not modify or replace their
collider components. Extreme overlapping shapes or rotations outside stock
joint limits can still destabilize the ragdoll.

## Compatibility and limitations

- Tested on KSP 1.12.5. Other KSP versions are not currently claimed as
  compatible.
- No third-party dependencies are required.
- Suit and model mods can expose different transform hierarchies. Semantic name
  matching helps profiles cross between compatible rigs, but unmatched targets
  are reported in `KSP.log` and left unchanged.
- Large rest-pose changes can still make authored animations look unusual. Use
  the Motion controls to constrain the affected axes.
- Back up `GameData/KerbalProportions/PluginData` before experimenting with a
  valuable profile library.

## Building from source

Building requires a local KSP 1.12.5 installation because the project compiles
against KSP and Unity assemblies that are not redistributed here.

From PowerShell:

```powershell
.\build.ps1 -KspRoot 'C:\Path\To\Kerbal Space Program'
```

Useful options:

```powershell
# Build and create artifacts/KerbalProportions-v2.6.0.zip
.\build.ps1 -KspRoot 'C:\Path\To\Kerbal Space Program' -Package

# Build and install into that KSP copy; KSP must be closed
.\build.ps1 -KspRoot 'C:\Path\To\Kerbal Space Program' -Install
```

On the author's default Steam installation, `-KspRoot` can be omitted. The
compiler path can be overridden with `-Compiler` if needed. Use `-DebugSymbols`
for a local PDB-enabled development build; public packages are always built
without path-bearing debug symbols.

## Project layout

```text
src/                                             C# source
GameData/KerbalProportions/                      Version metadata/default data
build.ps1                                        Build, install, and package script
KerbalProportions.netkan                         Proposed CKAN indexing metadata
```

Bug reports and feature requests are welcome through
[GitHub Issues](https://github.com/NickDeVeau/Kerbal-Proportions/issues). Please
include the KSP version, mod version, reproduction steps, relevant mod list, and
the matching section of `KSP.log`.

See [CHANGELOG.md](CHANGELOG.md) for release history and
[CONTRIBUTING.md](CONTRIBUTING.md) for development guidance.

## License

Kerbal Proportions is released under the [MIT License](LICENSE).

Kerbal Space Program and its related names and assets belong to their respective
owners. This community project is not affiliated with or endorsed by them.
