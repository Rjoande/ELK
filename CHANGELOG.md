# Changelog

## v1.0.1

### Added
- `SAS autoengage` (default `true`, with a checkbox in the toolbar window): an SAS mode hotkey pressed while SAS is off now switches SAS on and selects that mode in one press.

### Fixed
- An SAS mode hotkey pressed while SAS was off could engage SAS in *Stability Assist* instead of the requested mode.

## v1.0.0

### Added
- Keyboard toggle for stock *Brakes*, plus hotkeys for all 10 SAS autopilot modes (Stability Assist, Prograde/Retrograde, Normal/Anti-normal, Radial in/out, Target/Anti-target, Maneuver node). Each hotkey calls the same stock API the UI does, so the flight UI, the PAW and any other mod reading that state stay in sync.
- All hotkeys ship unbound, so a fresh install cannot collide with a binding already in use.
- Optional in-game toolbar (Space Center only): bind a key by pressing it, clear it, and get a non-blocking warning when the key is already used by another ELK slot or by a stock keybinding. Needs ToolbarControl and ClickThroughBlocker; `toolbar = false` drops both. Capture and conflict-detection logic ported from KRILL (same author, MIT), downgraded to C# 5 for the legacy `csc` build.
- `PluginData/ELK.cfg`, re-read on entering Flight or the Space Center, so changing a key needs no restart. A preset is just this file with the keys already filled in - see `Presets/`.
- Successor to [KSP-Stock-Brake-Toggle](https://github.com/Rjoande/KSP-Stock-Brake-Toggle) (now archived), which contributed the brake-toggle slot. SAS hotkeys are a fresh implementation, not a fork of the older GPL-3.0 [SASHotkeys](https://github.com/petersohn/SASHotkeys).
