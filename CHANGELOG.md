# Changelog

## [Unreleased]

### Changed
- Renamed the project from "ELK - Extended Lock Keys" to "ELK - Extended Logic Keys".
- Toolbar window widened and shortened the in-progress capture placeholder text, after the **Clear** button was being clipped and required a horizontal scrollbar to reach.
- Toolbar window no longer carries a fixed content height: the row list now sizes to its actual content instead of leaving dead space between the rows and the **Close** button.
- Toolbar window's scroll area now has an explicit width instead of stretching to the window's full available width, which was leaving dead space to the right of the Capture/Clear buttons.
- Presets are no longer a repo-bundled drop-in file: a preset is just an `ELK.cfg` with keys already filled in, shared however (gist, forum post, zip) and dropped into `PluginData/ELK.cfg` by hand. README and CONTRIBUTING updated to match.

### Fixed
- Hand-authored `PluginData/ELK.cfg` and the (now removed) bundled preset used inline `NODE_NAME { key = }` node syntax, which stock KSP's `ConfigNode.Load` does not parse correctly - each node name, opening brace, and closing brace now need their own line.
- `ElkConfig`'s config file path was computed one directory level too shallow (`Plugins/PluginData/ELK.cfg` instead of `PluginData/ELK.cfg`, since `ELK.dll` lives in a `Plugins/` subfolder of the mod root while `PluginData` is a sibling of `Plugins`, not nested under it). The cfg was silently never found, so every hotkey did nothing.
- Pressing <kbd>Esc</kbd> to cancel a toolbar capture also opened the Space Center's own pause menu. `UISpaceCenter.Update()` checks the input lock on the same frame it sees Escape's key-up, racing the capture logic's same-frame lock release; now waits one extra frame past the key-up before releasing the lock.

### Removed
- The bundled `Presets/Suggested.cfg` "drop-in replacement" file and its associated mechanism.

## v1.0.0

### Added
- Initial release: keyboard toggle for stock *Brakes*, plus hotkeys for all 10 SAS autopilot modes (Stability Assist, Prograde/Retrograde, Normal/Anti-normal, Radial in/out, Target/Anti-target, Maneuver node).
- Optional in-game toolbar (Space Center scene only) for assigning keys by pressing them, built on ToolbarControl/ClickThroughBlocker; capture and conflict-detection logic ported from KRILL (same author, MIT), downgraded to C# 5 for the legacy `csc` build.
- All hotkeys ship unbound by default to avoid colliding with other mods' bindings.
- Successor to [KSP-Stock-Brake-Toggle](https://github.com/Rjoande/KSP-Stock-Brake-Toggle) (now archived), which contributed the brake-toggle slot. SAS hotkeys are a fresh implementation, not a fork of the older GPL-3.0 [SASHotkeys](https://github.com/petersohn/SASHotkeys).

[Unreleased]: https://github.com/Rjoande/ELK/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Rjoande/ELK/releases/tag/v1.0.0
