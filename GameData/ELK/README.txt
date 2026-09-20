==============================================================================
 ELK - Extended Logic Keys
 Configurable keyboard hotkeys for stock KSP functions that don't have any.
==============================================================================

ELK assigns keyboard hotkeys to stock functions KSP leaves unbound: toggling
Brakes, and setting a specific SAS autopilot mode (Stability Assist,
Prograde/Retrograde, Normal/Anti-normal, Radial in/out, Target/Anti-target,
Maneuver node). An optional in-game toolbar (Space Center only) lets you
assign keys by pressing them, instead of hand-editing a config file.

Every hotkey calls the same stock API the UI itself uses, so the flight UI
(status light, navball SAS indicator), the right-click PAW, and any other mod
reading that state all stay in sync automatically.


------------------------------------------------------------------------------
 INSTALLATION
------------------------------------------------------------------------------

Copy the ELK folder from this zip into your KSP GameData folder, so you end
up with:

    GameData/ELK/Plugins/ELK.dll
    GameData/ELK/PluginData/ELK.cfg
    GameData/ELK/Textures/

The optional toolbar additionally needs ToolbarControl and
ClickThroughBlocker, both bundled in this zip - copy those folders into
GameData too, unless you already have them (in that case keep the newer
version).

If you'd rather not have the toolbar at all, you can skip both dependencies:
set "toolbar = false" in the cfg and bind your keys by hand.

Built and tested against KSP 1.12.5. No mod dependency for the core hotkeys.


------------------------------------------------------------------------------
 QUICK START
------------------------------------------------------------------------------

Every hotkey ships EMPTY. This is deliberate: ELK never picks a default for
you, so a fresh install can never collide with a binding you already use
elsewhere. Nothing happens until you assign a key.

  1. Launch KSP and go to the Space Center.
  2. Click the ELK button in the toolbar.
  3. Click "Capture" next to a function, then press the key (or combo) you
     want - it is bound the instant you press it.
  4. Repeat for any other function you want a key for. That's it.

In the toolbar window:

  Capture ..... press the key to bind; it is saved immediately
  Clear ....... removes a binding (so does pressing Delete mid-capture)
  Esc ......... cancels a capture without changing anything

A non-blocking warning appears under a row if the key you just picked is also
used by another ELK slot or by a stock keybinding. It is advisory only - you
can keep the key if you want. See "A note about Brakes" below for the one
case where it really matters.

The button is in the Space Center scene only: ELK's hotkeys are global player
bindings with nothing vessel, part, or flight-specific about them, so the
button doesn't clutter Flight/VAB/SPH/Tracking Station.


------------------------------------------------------------------------------
 CONFIGURATION FILE
------------------------------------------------------------------------------

GameData/ELK/PluginData/ELK.cfg - re-read every time you enter Flight or the
Space Center, so no game restart is needed after an edit.

    ELK
    {
        enabled = true
        toolbar = true

        BRAKES_TOGGLE
        {
            key =
        }
        SAS_STABILITY
        {
            key =
        }
        SAS_PROGRADE
        {
            key =
        }
        SAS_RETROGRADE
        {
            key =
        }
        SAS_NORMAL
        {
            key =
        }
        SAS_ANTINORMAL
        {
            key =
        }
        SAS_RADIAL_IN
        {
            key =
        }
        SAS_RADIAL_OUT
        {
            key =
        }
        SAS_TARGET
        {
            key =
        }
        SAS_ANTITARGET
        {
            key =
        }
        SAS_MANEUVER
        {
            key =
        }
    }

  enabled = false   disables every hotkey.
  toolbar = false   hides the Space Center button/window entirely, so the
                    ToolbarControl/ClickThroughBlocker dependencies are not
                    needed.

KEY FORMAT: optional modifiers separated by "+", then the main key, all
literal UnityEngine.KeyCode names - for example:

    key = Y
    key = LeftAlt+Y
    key = LeftControl+LeftShift+G

A key captured through the toolbar is written back in this exact format, so
both ways of setting a key stay compatible with each other.

Each node name and each brace must be on its own line: stock KSP's config
parser does not handle inline "NODE { key = X }" syntax.

PRESETS: a "preset" is just an ELK.cfg with the keys already filled in.
Anyone can put one together and share it (a gist, a forum post, a zip -
whatever). To use one: close KSP, replace GameData/ELK/PluginData/ELK.cfg
with the one you got, and relaunch. The file must keep that exact name and
location - it is the only one ELK reads.


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
    Check that the key is actually written in PluginData/ELK.cfg, that
    "enabled = true", and that no other mod grabs the same key first. If the
    key is bound to stock Brakes, see the note above.

No toolbar button in the Space Center
    The toolbar needs both ToolbarControl and ClickThroughBlocker installed,
    and "toolbar = true" in the cfg. It never appears in any other scene.

My edits to the cfg don't stick
    The toolbar rewrites the file when it captures or clears a key. Edit the
    cfg with KSP closed, or use the toolbar - not both at once.


------------------------------------------------------------------------------
 LICENSE AND LINKS
------------------------------------------------------------------------------

MIT - see LICENSE (included in this zip).

Source, changelog and issue tracker:
    https://github.com/Rjoande/ELK

Dependencies:
    ToolbarControl        https://github.com/linuxgurugamer/ToolbarControl
    ClickThroughBlocker   https://github.com/linuxgurugamer/ClickThroughBlocker

ELK succeeds KSP-Stock-Brake-Toggle (same author, now archived), which
contributed the brake-toggle slot. The SAS hotkeys are a fresh
implementation, not a fork of the older GPL-3.0 SASHotkeys.

Author: Rjoande. Built with the help of Claude Code.
