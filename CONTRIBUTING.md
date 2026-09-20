# Contributing to ELK

## Proposing a new hotkey slot

Every ELK hotkey is one entry in [`Source/ElkSlots.cs`](Source/ElkSlots.cs)'s `ElkSlots.All` list. That's the only file that needs to change for a new slot. `ElkAddon` (which fires the hotkeys in Flight) and `ElkWindow` (the toolbar) both just read that same list, so nothing else needs to know a new slot exists.

An entry is:

```csharp
new ElkSlot("YOUR_SLOT_ID", "Human-readable label", YourFireAction),
```

- `id`: `UPPER_SNAKE_CASE`, stable forever once released (it's also the `ConfigNode` name under `ELK { }` in the cfg - renaming it orphans existing users' saved bindings).
- `displayName`: shown in the toolbar window. Keep it short - the row has limited width.
- `fire`: an `Action<Vessel>` (or `SasSlot(mode)`-style helper, see the existing SAS entries) - whatever stock API call your slot should trigger. **Call the same API the corresponding stock UI element calls** - don't reimplement behavior stock already has (see `BRAKES_TOGGLE` and the SAS slots for the pattern: `ActionGroups.ToggleGroup`/`SetGroup`, `VesselAutopilot.Enable`/`CanSetMode`). If you're not sure what the stock UI calls, decompile it (`ilspycmd` against `Assembly-CSharp.dll`) rather than guessing - see the README's "Why not bind Brakes to B" section for why that verification step matters (an unverified assumption there shipped a real bug once).

## Checklist for a slot PR

- [ ] One new entry in `ElkSlots.cs`, following the pattern above.
- [ ] **No pre-picked key** in `PluginData/ELK.cfg` - new slots ship with an empty `key`, same as every existing one. ELK's whole no-conflicts design rests on never assuming a key is safe for every install.
- [ ] One line added to the slot table in `README.md`.
- [ ] If your slot's `fire` action can silently no-op in some vessel state (like `SAS_TARGET`/`SAS_MANEUVER` do when nothing is selected), document that in a code comment next to the entry, the way the existing SAS slots do.

## Scope

ELK is for stock functions that are missing a *toggle/set* keybind but already exist as clickable UI (Brakes, SAS modes). It is not a general custom-action-group system. For binding arbitrary part actions to keys, see [KRILL](https://github.com/Rjoande/KRILL) instead. If your idea doesn't fit that scope, open an issue to discuss before sending a PR.

## Build

`Source/build.bat` rebuilds the DLL with the C# compiler bundled in the .NET Framework, against KSP's own managed assemblies plus the installed `GameData/001_ToolbarControl/Plugins/ToolbarControl.dll`. No SDK, NuGet package, or Harmony required.
