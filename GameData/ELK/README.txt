==============================================================================
 ELK - Extended Logic Keys
 Configurable hotkeys for functions that don't have any.
==============================================================================

ELK assigns hotkeys to functions KSP leaves unbound: toggling Brakes, and
setting a specific SAS autopilot mode (Stability Assist, Prograde/Retrograde,
Normal/Anti-normal, Radial in/out, Target/Anti-target, Maneuver node). With
the mod installed it also offers hotkeys for AtmosphereAutopilot,
AtmosphereApproach, MechJeb2 and NavUtilities (NavInstruments Continued).
Keys can be grouped into "key sets" that rotate with one press, so the same
keypad drives the stock SAS on a rocket and Cruise Flight on a plane. An
optional in-game toolbar (Space Center only) lets you assign keys by
pressing them, instead of hand-editing a config file.

Every hotkey calls the same API the corresponding UI element calls - stock
or the mod's own - so the flight UI, the PAW, the mod's windows and any
other mod reading that state all stay in sync. Other mods are reached at
runtime by reflection: none of them is required or bundled, and a mod that
is not installed simply has no tab.


------------------------------------------------------------------------------
 INSTALLATION
------------------------------------------------------------------------------

Copy the ELK folder from this zip into your KSP GameData folder, so you end
up with:

    GameData/ELK/Plugins/ELK.dll
    GameData/ELK/PluginData/ELK.cfg
    GameData/ELK/PluginData/ELK_Squad.cfg
    GameData/ELK/Textures/

The optional toolbar additionally needs ToolbarControl and
ClickThroughBlocker. They are not in the plain ELK zip: install them
yourself, or download the "with-dependencies" zip of the release, which
carries both folders - copy those into GameData too, unless you already have
them (in that case keep the newer version). CKAN installs them for you.

If you'd rather not have the toolbar at all, you can skip both dependencies:
set "toolbar = false" in ELK.cfg and bind your keys by hand.

Upgrading from 1.0.x: the stock keys of your old ELK.cfg are copied into
the new ELK_Squad.cfg on the first launch; nothing is lost.

Built and tested against KSP 1.12.5. No mod dependency for the hotkeys.


------------------------------------------------------------------------------
 QUICK START
------------------------------------------------------------------------------

Every hotkey ships EMPTY. This is deliberate: ELK never picks a default for
you, so a fresh install can never collide with a binding you already use
elsewhere. Nothing happens until you assign a key.

  1. Launch KSP and go to the Space Center.
  2. Click the ELK button in the toolbar.
  3. Pick the tab (Squad for the stock keys; one tab per installed mod).
  4. Click "Capture" next to a function, then press the key (or combo) you
     want - it is bound the instant you press it.
  5. Repeat for any other function you want a key for. That's it.

In the toolbar window:

  Capture ..... press the key to bind; it is saved immediately
  Clear ....... removes a binding (so does pressing Delete mid-capture)
  Esc ......... cancels a capture without changing anything
  Map ......... the hotkey also works in map view (on by default for the
                SAS modes)
  [set] ....... with key sets on: the set the row belongs to (cycles)

  "hotkeys" checkbox (every tab but ELK): switches that whole tab off while
  keeping its keys.

A non-blocking warning appears under a row if the key you just picked is also
used by another ELK slot that can be active at the same time, by a stock
keybinding, or by one of AtmosphereAutopilot's own hotkeys. It is advisory
only. See "A note about Brakes" below for the one case where it really
matters.

The button is in the Space Center scene only: ELK's hotkeys are global player
bindings with nothing vessel, part, or flight-specific about them, so the
button doesn't clutter Flight/VAB/SPH/Tracking Station.


------------------------------------------------------------------------------
 HOTKEYS
------------------------------------------------------------------------------

Squad (stock)
    Brakes toggle; the 10 SAS modes. An SAS key switches SAS on first if
    needed (sas_autoengage) and brings back a hidden navball (sas_navball).

AtmosphereAutopilot
    Master switch, show/hide window, select Cruise Flight / Standard FBW,
    FD key; Cruise Flight Level / Heading / Waypoint, V/S and ALT hold,
    vertical motion, ALT <-> V/S, keys input mode, V/S / ALT / HDG bug
    up/down (steps configurable), "ALT bug = current altitude, hold
    engaged", "HDG bug = current heading, heading hold"; FBW Moderation /
    Rocket mode / Coordinated turn; speed control; thrust balancing.
    With AtmosphereApproach installed: APR arm/disarm.
    Import from AA / Export to AA / Restore AA keys move AA's own hotkeys
    to ELK and back, so nothing fires twice.

MechJeb2 (two tabs)
    SmartASS Off / Kill rotation / Prograde / Retrograde / Normal+- /
    Radial+- / Node / Target+-; Translatron Off / Keep vertical / Keep
    surface / Keep orbital / speed 0 / speed +- step / Kill H/S / PANIC;
    Land somewhere / at KSC / at target / abort; Ascent autopilot;
    Execute or abort node; show/hide menu. Copy from SAS / Copy to SAS
    mirror the 10 SAS keys onto SmartASS or back.

NavUtilities
    HSI window on/off.

The slot ids (node names in the cfg files) are listed in README.md on the
project page.

KEY SETS: name some sets in ELK.cfg ("sets = Stock, MechJeb, AA"), give
each hotkey a set (or leave it "always"), and rotate with the SET_NEXT /
SET_PREV hotkeys of the ELK tab; an on-screen message shows the new set.
The same physical key can then drive the stock SAS in one set and a mod in
the next. Each vessel remembers its own set in the save game
(sets_per_vessel), and leaving a set switches off what its keys control
(sets_disengage), so the next set never fights it. With "sets" empty,
every hotkey is simply always active.


------------------------------------------------------------------------------
 CONFIGURATION FILES
------------------------------------------------------------------------------

All in GameData/ELK/PluginData/, re-read every time you enter Flight or the
Space Center, so no game restart is needed after an edit.

    ELK.cfg                       global options, SET_NEXT / SET_PREV,
                                  known joysticks (DEVICES, written by ELK)
    ELK_Squad.cfg                 stock hotkeys, sas_autoengage, sas_navball
    ELK_AtmosphereAutopilot.cfg   created when AA is detected
    ELK_MechJeb2.cfg              created when MechJeb2 is detected
    ELK_NavUtilities.cfg          created when NavUtilities is detected

The mod files are created with every key empty and never deleted. Each
hotkey is a node like:

    SAS_PROGRADE
    {
        key = LeftAlt+Keypad8     the hotkey
        set =                     key set; empty = always
        map = true                also fires in map view
    }

ELK.cfg options:
  enabled = false        disables every hotkey.
  toolbar = false        hides the Space Center button/window entirely, so
                         ToolbarControl/ClickThroughBlocker are not needed.
  sets, active_set, sets_per_vessel, sets_disengage   see KEY SETS above
                         and the comments in the file.

Per-tab options (each has a checkbox or a field in its tab):
  hotkeys = false        switches off every hotkey of that file, keys kept.
  sas_autoengage = true  an SAS mode key pressed while SAS is OFF switches
                         SAS on and selects that mode in one press; false:
                         SAS keys only change mode while SAS is already on.
                         Either way a mode key does nothing when the mode
                         itself is not available (Maneuver with no node,
                         Target with nothing targeted).
  sas_navball = true     an SAS mode key brings back a hidden navball.
  aa_autoengage = true   a Cruise/FBW key switches AA on with that
                         controller first; false: only acts while it flies.
  aa_autoshow = false    open the AA window when a key switches it on.
  aa_vs_step = 1, aa_alt_step = 50, aa_hdg_step = 1   bug encoder steps.
  mj_autoengage = true   a Translatron speed key switches it on (Keep
                         vertical) first.
  mj_autoshow = false    open the MechJeb module's window when a key acts.
  mj_trans_step = 1      Translatron speed step, m/s.
  mj_navball = false     SmartASS keys bring back a hidden navball.

KEY FORMAT: optional modifiers separated by "+", then the main key, all
literal UnityEngine.KeyCode names - for example:

    key = Y
    key = LeftAlt+Y
    key = LeftControl+LeftShift+G

A key captured through the toolbar is written back in this exact format, so
both ways of setting a key stay compatible with each other. Joystick
buttons follow the device, not its number (Windows renumbers joysticks
between launches): a captured button is tied to the device's name in the
DEVICES node of ELK.cfg, shows in the window as "<label>.B10", and fires
on that device wherever it sits.

Each node name and each brace must be on its own line: stock KSP's config
parser does not handle inline "NODE { key = X }" syntax.

PRESETS: a "preset" is one or more of the per-tab files with the keys
already filled in (never ELK.cfg). To use one: close KSP, replace the
matching file(s) in GameData/ELK/PluginData/, and relaunch. See Presets/
on the project page.


------------------------------------------------------------------------------
 A NOTE ABOUT BRAKES
------------------------------------------------------------------------------

Do NOT use whatever key is bound to the stock Brakes action
(Settings > Input > BRAKES, "B" by default) as BRAKES_TOGGLE's key, not even
with a modifier. Any combo containing B can only ever disengage the brakes,
never re-engage them. Pick a key with no stock binding at all and the
conflict goes away entirely.

The toolbar's conflict warning checks every stock keybinding and flags this
collision live. A key typed by hand into the cfg does not get that check
until you open the toolbar once.


------------------------------------------------------------------------------
 TROUBLESHOOTING
------------------------------------------------------------------------------

Nothing happens when I press my key
    Check that the key is written in the right PluginData file, that
    "enabled = true" in ELK.cfg and "hotkeys = true" in that file, that the
    slot's key set is the active one (the on-screen message after
    SET_NEXT tells you), and that no other mod grabs the same key first.
    Every press ELK acts on is logged as "[ELK] <slot> fired" in
    Player.log, and every press it ignores says why.

My key does nothing in map view
    Switch its "map" flag on (the Map button on its row).

No toolbar button in the Space Center
    The toolbar needs both ToolbarControl and ClickThroughBlocker installed,
    and "toolbar = true" in ELK.cfg. It never appears in any other scene.

A mod's tab is missing
    The mod's DLL is not loaded; the window lists it after "Not installed".
    Its cfg file and keys are kept for when it is back.

My edits to a cfg don't stick
    The toolbar rewrites the file when it captures or clears a key. Edit the
    cfg with KSP closed, or use the toolbar - not both at once.


------------------------------------------------------------------------------
 LICENSE AND LINKS
------------------------------------------------------------------------------

MIT - see LICENSE (included in this zip).

Source, changelog and issue tracker:
    https://github.com/Rjoande/ELK

Dependencies (toolbar only; bundled in the "with-dependencies" zip):
    ToolbarControl        https://github.com/linuxgurugamer/ToolbarControl
    ClickThroughBlocker   https://github.com/linuxgurugamer/ClickThroughBlocker

Optional, never required or bundled, reached by reflection:
    AtmosphereAutopilot   https://github.com/Boris-Barboris/AtmosphereAutopilot
    AtmosphereApproach    https://github.com/Rjoande/AtmosphereApproach
    MechJeb2              https://github.com/MuMech/MechJeb2
    NavInstruments Cont.  https://github.com/linuxgurugamer/NavInstruments

ELK succeeds KSP-Stock-Brake-Toggle (same author, now archived), which
contributed the brake-toggle slot. The SAS hotkeys are a fresh
implementation, not a fork of the older GPL-3.0 SASHotkeys.

Author: Rjoande. Built with the help of Claude Code.
