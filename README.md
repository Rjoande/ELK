# ELK - Extended Logic Keys

![](assets/Elk2.jpg)

A small KSP plugin that assigns configurable hotkeys to functions that don't have any: toggling stock *Brakes*, jumping to a specific SAS mode, and - when the mod is installed - the autopilot modes, holds and windows of **AtmosphereAutopilot**, **AtmosphereApproach**, **MechJeb2** and **NavUtilities**. Keys can be grouped into **key sets** that rotate with a single press, so the same keypad drives the stock SAS on a rocket and Cruise Flight on a plane. An optional in-game toolbar (Space Center only) lets you assign keys by pressing them, instead of hand-editing a config file.

## The problem

Stock KSP has no way to:
- **Toggle** Brakes from the keyboard - `B` only holds them while pressed, there's no "parking brake" key (a [long-standing request](https://forum.kerbalspaceprogram.com/topic/180598-a-shortcut-to-lock-brakes/) that never got picked up).
- Jump straight to a specific **SAS mode** (Retrograde, Radial, a maneuver node...) with one press. `T` only toggles SAS on/off in whatever mode it was last in; picking a mode means clicking the navball.

Autopilot mods have the same gap one level up: MechJeb's SmartASS targets and Translatron modes exist only as buttons (or as per-craft action groups), AtmosphereAutopilot's own hotkeys take no modifiers and fire into text fields, NavUtilities' HSI has a toolbar button and nothing else.

## How it works

Each hotkey calls the same API the corresponding UI element calls - stock or the mod's own - so the flight UI, the PAW, the mod's windows and any other mod reading that state all stay in sync. Third-party mods are reached **at runtime by reflection**: ELK references none of them at compile time, bundles none and copies no code. A mod that isn't installed simply has no tab, and its keys do nothing.

## Installation

**CKAN:** search for *ELK* (identifier `ELK`); the toolbar dependencies are installed with it.

**Manual:** drop the `ELK` folder into `GameData`, so you end up with `GameData/ELK/Plugins/ELK.dll`. The optional toolbar additionally needs [ToolbarControl](https://github.com/linuxgurugamer/ToolbarControl) and [ClickThroughBlocker](https://github.com/linuxgurugamer/ClickThroughBlocker), both bundled in the `with-dependencies` zip of each release (the plain `ELK_v<version>.zip` carries only ELK, as CKAN needs). Set `toolbar = false` in `ELK.cfg` if you'd rather not have them at all and just hand-edit the config.

None of the supported mods is required. Upgrading from 1.0.x: your stock keys are migrated into the new `ELK_Squad.cfg` automatically on the first launch.

## Hotkeys

Every key ships **empty**. This is deliberate: ELK never picks a default for you, so a fresh install can never collide with a binding you already use elsewhere. The tables below list the slot id (the node name in the cfg) and the label the toolbar shows.

**ELK tab** - `ELK.cfg`

| Slot | Does |
|---|---|
| `SET_NEXT` / `SET_PREV` | Rotate the active key set (see *Key sets*). Always active, also in map view. |

**Squad tab** - `ELK_Squad.cfg`

| Slot | Does |
|---|---|
| `BRAKES_TOGGLE` | Brakes on/off (a real parking brake). |
| `SAS_STABILITY`, `SAS_PROGRADE`, `SAS_RETROGRADE`, `SAS_NORMAL`, `SAS_ANTINORMAL`, `SAS_RADIAL_IN`, `SAS_RADIAL_OUT`, `SAS_TARGET`, `SAS_ANTITARGET`, `SAS_MANEUVER` | Select that SAS mode; switches SAS on first if needed (`sas_autoengage`). Does nothing when the mode isn't available (no node, no target) - checked before SAS is engaged. Radial in/out follow the navball markers. |

**AtmosphereAutopilot tab** - `ELK_AtmosphereAutopilot.cfg` (needs [AtmosphereAutopilot](https://github.com/Boris-Barboris/AtmosphereAutopilot))

| Slot | Does |
|---|---|
| `AA_MASTER_TOGGLE` | Master switch on/off. |
| `AA_GUI_TOGGLE` | Show/hide the AA window. |
| `AA_SELECT_CRUISE`, `AA_SELECT_FBW` | Switch AA on with Cruise Flight / Standard Fly-By-Wire. |
| `AA_FD_TOGGLE` | Cruise Flight <-> Standard FBW; with AA off, Standard FBW. |
| `AA_CF_LEVEL`, `AA_CF_HEADING`, `AA_CF_WAYPOINT` | Cruise Flight lateral mode: Level, Heading hold, Waypoint. |
| `AA_CF_VS_TOGGLE`, `AA_CF_ALT_TOGGLE` | Engage vertical speed / altitude hold, or disengage it if already in that mode. |
| `AA_CF_VERTICAL_TOGGLE` | Vertical motion control on/off. |
| `AA_CF_SETPOINT_TYPE_TOGGLE` | Altitude <-> vertical speed (or flight path angle) setpoint. |
| `AA_CF_KEYS_MODE_TOGGLE` | AA's "CF keys input mode" preference. |
| `AA_CF_VS_UP/DOWN`, `AA_CF_ALT_UP/DOWN`, `AA_CF_HDG_UP/DOWN` | Move the V/S, altitude or heading bug by `aa_vs_step` / `aa_alt_step` / `aa_hdg_step`. Never switch anything on: a bug can be preset with AA off or FBW flying. |
| `AA_CF_ALT_SYNC`, `AA_CF_HDG_SYNC` | Altitude bug = current altitude and hold engaged; heading bug = current heading and heading hold selected. |
| `AA_FBW_MODERATION`, `AA_FBW_ROCKET`, `AA_FBW_COORD_TURN` | Standard FBW toggles. |
| `AA_SPEED_CONTROL`, `AA_THRUST_BALANCING` | Speed control / thrust balancing flags (effective while a controller using them is active, AA's own rule). |
| `AA_APR_TOGGLE` | **AtmosphereApproach** only: APR arm/disarm, selecting the Approach controller first, same as AAPR's own hotkey. The row appears only with AAPR installed. |

With `aa_autoengage` (default on) a Cruise mode, hold, sync or FBW toggle key pressed while that controller is off switches AA on with it first, so one press does what its name says; off, those keys only act while their controller is already flying, like AA's own hotkeys. `aa_autoshow` (default off) opens the AA window whenever a key switches something on.

**MechJeb2 tab** - `ELK_MechJeb2.cfg` (needs [MechJeb2](https://github.com/MuMech/MechJeb2)); the actions MechJeb also offers as part action groups, here as global hotkeys that find the vessel's master MechJeb on every press.

| Slot | Does |
|---|---|
| `MJ_SASS_OFF`, `MJ_SASS_KILLROT`, `MJ_SASS_PROGRADE`, `MJ_SASS_RETROGRADE`, `MJ_SASS_NORMAL_PLUS/MINUS`, `MJ_SASS_RADIAL_PLUS/MINUS` | SmartASS off / Kill rotation / orbital targets. |
| `MJ_TRANS_OFF`, `MJ_TRANS_KEEP_VERT`, `MJ_TRANS_ZERO`, `MJ_TRANS_KILLH_TOGGLE`, `MJ_PANIC` | Translatron off / Keep vertical / speed = 0 / Kill H/S / PANIC. |
| `MJ_LAND_SOMEWHERE`, `MJ_LAND_KSC` | Landing guidance: land somewhere / at KSC. |
| `MJ_ASCENT_TOGGLE` | Ascent guidance autopilot on/off. |

**MechJeb2+ tab** - same file, what the action groups don't cover.

| Slot | Does |
|---|---|
| `MJ_SASS_NODE`, `MJ_SASS_TARGET_PLUS`, `MJ_SASS_TARGET_MINUS` | SmartASS Node / Target+ / Target- (no-op without a node or target). |
| `MJ_TRANS_KEEP_SURF`, `MJ_TRANS_KEEP_ORBIT` | Translatron Keep surface / Keep orbital. |
| `MJ_TRANS_SPEED_UP`, `MJ_TRANS_SPEED_DOWN` | Translatron speed +/- `mj_trans_step` m/s. |
| `MJ_LAND_TARGET`, `MJ_LAND_ABORT` | Land at target (needs a position target) / abort landing. |
| `MJ_NODE_EXECUTE_TOGGLE` | Execute the next maneuver node, or abort if running. |
| `MJ_MENU_TOGGLE` | Show/hide the MechJeb menu. |

MechJeb's own gates apply (no MechJeb aboard, module locked in career, nothing to act on = silent). `mj_autoengage` (default on) lets a speed key switch the Translatron on in Keep vertical; `mj_autoshow` (default off) opens the module's window when a key acts; `mj_navball` (default off) makes a SmartASS key bring back a hidden navball, like `sas_navball`.

**NavUtilities tab** - `ELK_NavUtilities.cfg` (needs [NavInstruments Continued](https://github.com/linuxgurugamer/NavInstruments))

| Slot | Does |
|---|---|
| `NAV_HSI_TOGGLE` | HSI window on/off, same as a click on its toolbar button (icon kept in sync). |

## Key sets

Name some sets in `ELK.cfg` - `sets = Stock, MechJeb, AA` - then give each hotkey a set (`set = MechJeb` in its node, or the button next to its row in the toolbar) or leave it empty for "always". `SET_NEXT` / `SET_PREV` rotate through the list with an on-screen message, and only the hotkeys of the active set (plus the "always" ones) fire. The same physical key can then select Prograde on the stock SAS in one set and on SmartASS in the next, and ELK only warns about clashes between keys that can be active together.

- `sets_per_vessel = true` (default): every vessel remembers its own active set inside the save game, so a plane can live in the AA set while a rocket stays in Stock; `active_set` is the set new vessels start in. `false`: one global set, stored in `ELK.cfg`.
- `sets_disengage = *` (default): leaving a set in flight switches off whatever its keys control - SAS, brakes, the AA master, SmartASS, the Translatron, a running landing / ascent / node execution - so the next set never fights it. A list of set names limits this to those sets, empty disables it; the ELK tab has one checkbox per set. Nothing else triggers it (not a scene change, not switching vessel), and "always" hotkeys are never switched off.
- With `sets` empty (the default) the feature is off and every hotkey is simply always active.

## Map view

Each hotkey has a `map` flag (`Map` button on its row): on, the key also fires in map view, navball shown or hidden; off, it is flight-scene only. The 10 stock SAS modes ship on, everything else off; `SET_NEXT` / `SET_PREV` always work in map view. A text field with keyboard focus still blocks every hotkey.

An SAS mode key that selects a mode also brings the navball back when it is hidden - in flight and in map view, where it frees the attitude controls the way the navball's own button does (`sas_navball`, default on, checkbox in the Squad tab). Stock never does this by itself.

## Configuration

All files live in `PluginData/` and are re-read every time you enter Flight or the Space Center (no restart needed). `ELK.cfg` and `ELK_Squad.cfg` ship with the mod; each `ELK_<Mod>.cfg` is created, with every key empty, the first time ELK runs with that mod installed, and never deleted.

`ELK.cfg` - global options:

```
ELK
{
	enabled = true           // false: every hotkey off
	toolbar = true           // false: no Space Center button, dependencies not needed
	sets =                   // key sets, comma-separated, in rotation order; empty = feature off
	active_set =             // default set for new vessels (or the only set when sets_per_vessel = false)
	sets_per_vessel = true
	sets_disengage = *       // *, a list of set names, or empty
	SET_NEXT { key = }
	SET_PREV { key = }
	DEVICES { }              // joysticks known by name, written by ELK (see Key format)
}
```

A per-tab file - `ELK_Squad.cfg` shown, the mod files have the same shape with their own options:

```
ELK
{
	hotkeys = true           // false: every hotkey of this tab off, keys kept
	sas_autoengage = true
	sas_navball = true
	BRAKES_TOGGLE
	{
		key =                // the hotkey
		set =                // key set; empty = always
		map = false          // also fires in map view
	}
	SAS_PROGRADE
	{
		key =
		set =
		map = true
	}
	...
}
```

(Each node name and brace on its own line: stock's config parser doesn't handle inline `NODE { key = X }`.)

Tab options: `sas_autoengage`, `sas_navball` (Squad); `aa_autoengage`, `aa_autoshow`, `aa_vs_step` = 1, `aa_alt_step` = 50, `aa_hdg_step` = 1 (AtmosphereAutopilot); `mj_autoengage`, `mj_autoshow`, `mj_trans_step` = 1, `mj_navball` (MechJeb2). All have a checkbox or field in their tab.

`sas_autoengage = true` (the default) means an SAS mode key pressed while SAS is *off* switches SAS on and selects that mode in one press, exactly as if you had pressed the stock SAS key (<kbd>T</kbd>) first. Set it to `false` and the SAS keys only ever change mode while SAS is already on. Either way a mode key still does nothing when the mode itself isn't available (*Maneuver* with no node planned, *Target* with nothing targeted): ELK checks that before engaging anything, so a dead key press can't leave you with SAS switched on behind your back.

**Key format**: optional modifiers separated by `+`, then the main key, all literal `UnityEngine.KeyCode` names - e.g. `Y`, `LeftAlt+Y`, `LeftControl+LeftShift+G`. A key captured through the toolbar is written back in this exact format, so both ways of setting a key stay compatible. **Joystick buttons** follow the device, not its number: Windows hands out joystick numbers in a different order from one launch to the next, so a captured button (`Joystick1Button10` in the file) is tied to the device's name in the `DEVICES` node and fires on that device wherever it sits; a device that isn't connected simply does nothing. Each device gets a short label, editable in the ELK tab (which also shows where each known device is right now and lets you forget one), and the window shows such a key as `VKBsim.B10` - a form the cfg accepts too.

### Toolbar

Space Center scene only: ELK's hotkeys are global player bindings with nothing vessel, part, or flight-specific about them, so the button doesn't clutter Flight/VAB/SPH/Tracking Station. One tab per installed mod (hidden otherwise, with a `Not installed:` line), each with a `hotkeys` checkbox and its options. Click **Capture** next to a function, then press the key (or combo) you want: it is bound the instant you press it. **Clear** removes a binding, so does pressing <kbd>Delete</kbd> while a capture is in progress. <kbd>Esc</kbd> cancels a capture without changing anything. A non-blocking warning appears under a row if the key you just picked is also used by another ELK slot that can be active at the same time, by a stock keybinding, or by a native hotkey of AtmosphereAutopilot (see below for why that warning matters). It's advisory only.

The AtmosphereAutopilot tab also has **Import from AA** (moves AA's own hotkeys into the matching ELK slots and clears them in AA, so nothing fires twice), **Export to AA** (the reverse, for keys without modifiers) and **Restore AA keys** (puts back the snapshot ELK takes of AA's keys before its first change, `GameData/AtmosphereAutopilot/ELK_AtmosphereAutopilot_default-backup.txt`). These are the only times ELK writes another mod's file, and only on your click. The MechJeb2 tab has **Copy from SAS** / **Copy to SAS**, which mirror the 10 stock SAS keys onto the matching SmartASS slots or back without clearing the source: with key sets, both can live on the same keys.

### Presets

A "preset" is one or more per-tab cfg files with the keys already filled in; `ELK.cfg` stays yours. You can find a selection in [`Presets/`](Presets/), with its own README on making and installing them. To use one, close KSP, replace the matching file(s) in `GameData/ELK/PluginData/`, and relaunch.

## Why not bind Brakes to `B`

**Don't use whatever key is currently bound to the stock *Brakes* action** (`Settings > Input > BRAKES`, `B` by default) as `BRAKES_TOGGLE`'s key, not even with a modifier. Any combo containing `B` can only ever *disengage* the brakes, never re-engage them. A key with no stock binding at all sidesteps the conflict entirely. The toolbar's conflict warning checks every stock `GameSettings` keybinding (via reflection) and would flag this collision live.

## Optional integrations

Nothing in this section is required or bundled. ELK looks for each mod's DLL at runtime and reaches it by reflection, so a missing mod just hides its tab; a future version of a mod that renames something turns the affected keys into no-ops with a warning in the log, never into an error. Verified against AtmosphereAutopilot 1.6.1 (official and Rjoande's fork), AtmosphereApproach 0.1, MechJeb2 2.15.3 and NavInstruments Continued 0.8.1.1 on KSP 1.12.5.

Thanks to Boris-Barboris (AtmosphereAutopilot), the MechJeb team, kujuman and linuxgurugamer (NavUtilities / NavInstruments Continued), and linuxgurugamer again for ToolbarControl and ClickThroughBlocker.

## Compatibility

Built and tested against KSP 1.12.5. No mod dependency for the hotkeys; the toolbar needs ToolbarControl/ClickThroughBlocker. Some controllers deliver one press as two: ELK ignores a second press of the same hotkey within 0.15 s and logs every press as `[ELK] <slot> fired`, so `Player.log` tells you exactly what fired and what didn't.

## Contributing

Want to propose a new hotkey slot or a new supported mod? See [CONTRIBUTING.md](CONTRIBUTING.md).

## Predecessor

The brake toggle here started as its own small mod, [KSP-Stock-Brake-Toggle](https://github.com/Rjoande/KSP-Stock-Brake-Toggle) (now archived in favor of ELK). SAS hotkeys are a fresh implementation, not a fork of the decade-old, GPL-3.0, [SASHotkeys](https://github.com/petersohn/SASHotkeys) (different license, different external dependency, and the stock API it targeted has since gained a `CanSetMode` guard this version relies on).

## License

MIT - see [LICENSE](LICENSE).

## Credits

Author: Rjoande. Built with the help of Claude Code.
