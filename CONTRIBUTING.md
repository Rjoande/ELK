# Contributing to ELK

![](assets/ELK_long.png)

## Proposing a new hotkey slot

Every ELK hotkey is one entry in [`Source/ElkSlots.cs`](Source/ElkSlots.cs)'s `ElkSlots.All` list, stock and third-party alike. That's the only registry: `ElkAddon` (which fires the hotkeys in Flight), `ElkWindow` (the toolbar) and `ElkConfig` (the cfg files) all just read that same list, so nothing else needs to know a new slot exists.

An entry is:

```csharp
new ElkSlot("YOUR_SLOT_ID", "Human-readable label", YourFireAction),                        // stock, Squad tab
new ElkSlot("MOD_SLOT_ID", "Mod: label", ElkModBridge.Action, ElkGroups.Mod, ElkModBridge.IsInstalled, ElkModBridge.Off),
```

- `id`: `UPPER_SNAKE_CASE`, stable forever once released (it's also the `ConfigNode` name in the tab's cfg file - renaming it orphans existing users' saved bindings).
- `displayName`: shown in the toolbar window. Keep it short - the row has limited width - and ASCII only (the IMGUI font has no fancy glyphs).
- `fire`: an `Action<Vessel>` - whatever API call your slot should trigger. **Call the same API the corresponding UI element calls** - don't reimplement behavior the game or the mod already has (see `BRAKES_TOGGLE` and the SAS slots for the stock pattern: `ActionGroups.ToggleGroup`/`SetGroup`, `VesselAutopilot.Enable`/`CanSetMode`). If you're not sure what the UI calls, decompile it (`ilspycmd` against `Assembly-CSharp.dll` or the mod's DLL) rather than guessing: an unverified assumption shipped a real bug once (see the README's "Why not bind Brakes to B").
- `group`: the tab (and cfg file) the slot belongs to, one of `ElkGroups`. Omit it for stock slots.
- `available`: for a third-party slot, the bridge's `IsInstalled` probe. The row is hidden and the key is a silent no-op while the mod is absent.
- `off` (optional): what "switched off" means for the slot, called when the player leaves the slot's key set in flight (`sets_disengage`). Omit it for slots that have no "off" state (a window toggle, a one-shot action).

### Third-party mods

A slot for another mod goes through a static bridge in `Source/Bridges/` that reaches the mod **by reflection only**: ELK references no third-party DLL at compile time, bundles none, and copies no code (AtmosphereAutopilot, AtmosphereApproach and MechJeb2 are GPL-3; ELK stays MIT). The bridge resolves the mod's members lazily on the first press, logs how many it found, and turns each missing member into a no-op rather than an exception. A new mod also means a new `ElkGroup` entry (tab label, cfg file name, probe, optional conflict scanner and option defaults) - the cfg file is then created automatically when ELK first runs with that mod installed.

## Checklist for a slot PR

- [ ] One new entry in `ElkSlots.cs`, following the pattern above.
- [ ] **No pre-picked key**: new slots ship with an empty `key`, same as every existing one. The shipped `PluginData/ELK_Squad.cfg` (stock slots only) gets a new empty node; mod cfgs are generated and need nothing. ELK's whole no-conflicts design rests on never assuming a key is safe for every install.
- [ ] One line added to the slot table in `README.md` and a line in `CHANGELOG.md` under the next version.
- [ ] If your slot's `fire` action can silently no-op in some vessel state (like `SAS_TARGET`/`SAS_MANEUVER` do when nothing is selected), document that in a code comment next to the entry, the way the existing slots do.
- [ ] C# 5 syntax only (no `?.`, no `nameof`, no `$""`, no expression-bodied members): `build.bat` uses the legacy `csc` of the .NET Framework. A new `.cs` file must be added to the explicit list in `build.bat` (the csproj picks it up by itself).

## Scope

ELK is for **global hotkeys to vessel-level functions** that have no key of their own, whether stock or belonging to a supported mod: functions that only exist as something to click (Brakes toggle, SAS modes, an autopilot mode, a window), or that a mod offers only as a per-craft action group. It is not a general custom-action-group system: binding arbitrary **part** actions to keys is what [KRILL](https://github.com/Rjoande/KRILL) is for. A new supported mod is welcome if it fits that definition and can be reached by reflection. If your idea doesn't fit, open an issue to discuss before sending a PR.

## Build

`Source/build.bat` rebuilds the DLL with the C# compiler bundled in the .NET Framework, against the managed assemblies of the KSP install named in the script (plus its `GameData/001_ToolbarControl/Plugins/ToolbarControl.dll`), and writes it to this repo's `GameData/ELK/Plugins/` only: copying it into your game install is up to you. `dotnet build -c Release` in `Source/` (`ELK.csproj`) produces the same DLL. No NuGet package or Harmony required.
