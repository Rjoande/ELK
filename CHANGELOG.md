# Changelog

## [Unreleased]

### Added
- Optional NavUtilities (NavInstruments Continued) integration: a `NavUtilities: HSI window` hotkey that opens and closes the HSI, same as a left click on its toolbar button (icon kept in sync). Reached at runtime by reflection, so NavUtilities is neither required nor bundled; with the mod absent the hotkey is a silent no-op.
- The toolbar window is organized in tabs, one per supported mod, shown only when that mod is installed. Each tab has its own cfg file in `PluginData` (`ELK.cfg` for stock, `ELK_NavUtilities.cfg`...), created empty when the mod is first detected and never deleted, so presets for different mods stay independent.
- Optional AtmosphereAutopilot integration (reflection, nothing bundled): 26 hotkeys covering the master switch, Cruise Flight / Standard FBW selection and the FD toggle, every Cruise Flight mode (Level, Heading, Waypoint, V/S and ALT engage/disengage, vertical motion, ALT <-> V/S, keys input mode), V/S / ALT / HDG bug encoders with configurable steps plus "ALT bug = current altitude, hold engaged" and "HDG bug = current heading, heading hold selected", the three FBW toggles, speed control and thrust balancing. `aa_autoengage` (default on) lets a Cruise/FBW key switch the autopilot on first, like `sas_autoengage`; `aa_autoshow` opens the AA window when a key switches it on. Works on official AA 1.6.1; unlike AA's own hotkeys these accept modifier combos and respect text-field focus.
- AtmosphereAutopilot tab buttons: **Import from AA** moves AA's own hotkeys into the matching ELK slots and clears them in AA (no double input), **Export to AA** does the reverse, **Restore AA keys** puts back the snapshot ELK takes before its first change (`GameData/AtmosphereAutopilot/ELK_AtmosphereAutopilot_default-backup.txt`). Capturing a key that AA still uses natively is flagged like a stock conflict.
- Optional MechJeb2 integration (reflection, nothing bundled), on two tabs sharing `ELK_MechJeb2.cfg`. **MechJeb2**: the 16 actions MechJeb already offers as part actions (SmartASS Off/Kill rotation/Prograde/Retrograde/Normal+-/Radial+-, Translatron Off/Keep vertical/Speed 0/Kill H/S/PANIC, Land somewhere/at KSC, Ascent autopilot toggle), now global hotkeys that find the vessel's master MechJeb on every press, no action group per craft. **MechJeb2+**: SmartASS Node and Target+/-, Translatron Keep surface/orbital and Speed +/-step (`mj_trans_step`), Land at target and Abort, Execute/abort node, Show/hide menu, with MechJeb's own gates (no node, no target, locked in career...). `mj_autoengage` lets a speed key switch the Translatron on in Keep vertical, `mj_autoshow` opens the module's window. **Copy from/to SAS** copies the 10 stock SAS keys onto the matching SmartASS slots (or back) without clearing the source: with key sets, both live on the same keys.
- **Key sets**: name some sets in `ELK.cfg` (`sets = Stock, MechJeb, AA`), assign each hotkey to one (or leave it "always"), and rotate with the new `SET_NEXT` / `SET_PREV` hotkeys, with an on-screen message. The same physical key can then drive the stock SAS in one set and another mod in the next, and ELK only warns about key clashes between hotkeys that can be active together. Each vessel remembers its own set inside the save game (`sets_per_vessel`, on by default, with a global default for new vessels); off, one global set. Leaving a set in flight switches off whatever its keys control (SAS, brakes, AA master, SmartASS, Translatron, a running landing / ascent / node execution), so the keys of the next set never fight it; `sets_disengage` chooses which sets do this (`*` = all, the default, with one checkbox per set in the window); slots marked "always" are never touched. With no set declared nothing changes.
- The stock hotkeys (Brakes, SAS modes) and `sas_autoengage` moved from `ELK.cfg` to their own `ELK_Squad.cfg`, so a preset never carries the global options; `ELK.cfg` keeps `enabled`, `toolbar` and the key-set settings. An install upgraded from 1.0.x gets `ELK_Squad.cfg` created automatically with its existing keys copied over. The NumPad preset is now `Presets/NumPad/ELK_Squad.cfg`.
- `hotkeys = true/false` per cfg file, with a checkbox on each tab: switches off every hotkey of that tab (and, for a mod tab, every interaction with that mod) while keeping the bindings.
- `Source/ELK.csproj`: the mod can now also be built with `dotnet build` (SDK-style, net472, C# 5 kept so `build.bat` keeps working too).
- A slot cannot fire twice within 0.15 s: some controllers deliver one press as two key-down events a frame apart, which used to flip a toggle twice. Every hotkey press is logged as `[ELK] <slot> fired`.

### Fixed
- `SAS_RADIAL_IN` and `SAS_RADIAL_OUT` were swapped: stock's `AutopilotMode.RadialIn` actually points away from the body (the navball's radial-out marker) and vice versa. The slots now follow the navball; ids, labels and presets are unchanged.

### Changed
- Two ELK bindings now conflict only when one press can fire both: same main key and one modifier set contained in the other. `RightControl+Keypad1` and `RightShift+Keypad1` no longer warn; `Keypad1` against `RightShift+Keypad1` still does (the bare key fires whatever is held), and stock keybindings are still matched on the main key alone.
- Capturing a joystick button records it once, as that joystick's button (`Joystick2Button10` instead of `Joystick2Button10+JoystickButton10`); held alias keys are no longer recorded twice. Bindings saved in the old form keep working.

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
