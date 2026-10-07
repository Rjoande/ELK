# Changelog

## v1.1.0

### Added
- **Optional mod integrations.** ELK now offers hotkeys for AtmosphereAutopilot, AtmosphereApproach, MechJeb2 and NavUtilities (NavInstruments Continued), also adding new shortcuts not included in the original mods.
- **Key sets.** Name some sets, assign each hotkey to one or leave it "always", and rotate with the new `SET_NEXT` / `SET_PREV` keys, with an on-screen message. As result, the same physical key can drive the stock SAS in one set and a mod in the next. ELK only warns about conflicts between keys that can be active together.
- **Joystick buttons follow the device**: Windows hands out joystick numbers in a different order from one launch to the next. Now a captured button is tied to the device's name (editable). Old bindings keep working by number until recaptured.

### Fixed
- `SAS_RADIAL_IN` and `SAS_RADIAL_OUT` were swapped against the navball markers; ids, labels and presets are unchanged.
- Fixed hotkeys not firing in map view.
- A hotkey cannot fire twice within 0.15 s (some controllers send one press as two).
- A joystick button is captured once, as that joystick's button, no longer paired with the generic alias.
- Two binds on the same key but with different modifiers are no longer reported as a conflict.
- **Command/Apple keys (or LWin / RWin) are no longer recorded twice** as modifiers.

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
