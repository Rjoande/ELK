==============================================================================
 ELK - Presets
==============================================================================

A preset is nothing more than a ready-made ELK config file with the "key"
fields already filled in. There is no preset format, no preset loader, no
in-game preset browser - installing one means putting the file where the
mod looks for it.

Since ELK 1.1.0 the hotkeys are split over one file per tab of the toolbar
window, all in GameData/ELK/PluginData/:

    ELK.cfg                       global options and the key-set hotkeys
    ELK_Squad.cfg                 stock Brakes and SAS modes
    ELK_AtmosphereAutopilot.cfg   AtmosphereAutopilot (and AtmosphereApproach)
    ELK_MechJeb2.cfg              MechJeb2 (both MechJeb tabs)
    ELK_NavUtilities.cfg          NavUtilities

A preset is any of the per-tab files (one or several), never ELK.cfg: the
global options and the list of key sets stay yours. The mod files are
created by ELK the first time it runs with that mod installed; a preset for
a mod simply replaces that file.


------------------------------------------------------------------------------
 LAYOUT OF THIS FOLDER
------------------------------------------------------------------------------

    Presets/
        README.txt                <- this file
        <PresetName>/
            ELK_Squad.cfg         <- the preset itself (one or more files)
            ELK_MechJeb2.cfg
        <AnotherPreset>/
            ELK_Squad.cfg

One folder per preset, named after the preset; the files inside keep the
exact names above. ELK reads hard-coded paths and never scans for
alternatives (see "Why the name matters" below), so a preset folder is
only a way to keep several of them side by side here.


------------------------------------------------------------------------------
 INSTALLING A PRESET
------------------------------------------------------------------------------

  1. Quit KSP (or at least leave the Space Center and Flight scenes, and
     close the ELK toolbar window - the toolbar rewrites a config file
     whenever it captures or clears a key, and would overwrite your new
     file).

  2. Back up the file(s) you are about to replace if you care about them:

         GameData/ELK/PluginData/ELK_Squad.cfg   ->   ELK_Squad.cfg.bak

  3. Copy each preset file over the same name in GameData/ELK/PluginData/,
     replacing the existing one. Copy the files themselves, not the preset
     folder: there must be no "<PresetName>" directory left in PluginData.

  4. Start KSP. The config is re-read every time you enter the Space Center
     or Flight, so if the game was already running, switching scenes is
     enough - no restart needed.

  5. Open the ELK toolbar button in the Space Center to check the bindings
     it lists, and to see the conflict warnings for your own install: a
     preset author cannot know which other mods or custom stock keybindings
     you have.

If a hotkey does nothing afterwards, check KSP.log for lines starting with
"[ELK]" - the config load logs how many slots ended up bound, and warns
about any "key" value it could not parse.


------------------------------------------------------------------------------
 WHY THE NAME MATTERS
------------------------------------------------------------------------------

ELK builds its config paths from its own assembly location: the folder
above Plugins/ (that is, the mod root), then PluginData/<file>.cfg. Both
the folder and the file names are fixed in code. A file called
MyPreset.cfg, or an ELK_Squad.cfg sitting one folder deeper, is simply
never read.

That path also means a preset only ever applies to the ELK install it is
copied into: it is a per-install file, not a per-save one. (The active key
set, on the other hand, is remembered per vessel inside the save game.)


------------------------------------------------------------------------------
 MAKING YOUR OWN PRESET
------------------------------------------------------------------------------

Easiest way: bind the keys you want in-game through the toolbar, quit KSP,
then copy the per-tab file(s) out of GameData/ELK/PluginData/ - they are
already valid presets.

One caveat: the toolbar writes a file through KSP's own ConfigNode writer,
which does not preserve "//" comments. A cfg that has been through a capture
has lost the shipped explanatory header, so add a short one back by hand
before sharing (see the template below).

Hand-editing works just as well. The structure of every per-tab file is:

    ELK
    {
        hotkeys = true
        sas_autoengage = true       // options of that tab, if any

        BRAKES_TOGGLE
        {
            key = LeftAlt+B
            set =
            map = false
        }
        SAS_RETROGRADE
        {
            key = LeftAlt+R
            set = Stock
            map = true
        }
    }

  - Each node name, each opening brace and each closing brace needs its own
    line. Stock KSP's config parser does not handle inline
    "NODE { key = X }" syntax.
  - A slot you leave out entirely is simply unbound - a preset does not have
    to list every slot. The slot ids (one per row of the toolbar window) are
    listed in the main README.
  - "set": the key set the slot belongs to; empty = active in every set. A
    preset may fill it in, but the set names themselves live in ELK.cfg,
    which a preset should not touch: say in your header which "sets ="
    line it expects.
  - "map": true lets the hotkey fire in map view too. Leave it out and the
    slot keeps its default (on for the stock SAS modes, off elsewhere).
  - Leave "hotkeys = true" alone unless the preset has a reason to change
    it.

KEY FORMAT: optional modifiers separated by "+", then the main key, all
literal UnityEngine.KeyCode names - for example "Y", "LeftAlt+Y",
"LeftControl+LeftShift+G". Names are matched case-insensitively and
surrounding spaces are ignored, but anything that is not a real KeyCode name
makes that one slot unbound (with a warning in KSP.log). An empty value
means "no key". Joystick buttons ("Joystick1Button10") are tied to the
device by name through the DEVICES node of ELK.cfg of the person who
captured them, so they rarely travel well in a preset.

ONE KEY TO AVOID: do not bind BRAKES_TOGGLE to whatever key stock Brakes
uses (Settings > Input > BRAKES, "B" by default), not even with a modifier.
Any combo containing B can only ever disengage the brakes, never re-engage
them.


------------------------------------------------------------------------------
 SHARING A PRESET
------------------------------------------------------------------------------

Nothing has to go through this repo: a preset is one or two small text
files, so a gist, a forum post or a zip works just as well.

To contribute one here, add a folder named after the preset with your
file(s) inside it, and start each file with a header comment saying what it
is:

    // <Preset name> - ELK preset (<tab>)
    // Author: <you>
    // <One or two lines: the idea behind the layout, e.g. which hand it
    // keeps free, which other mod's bindings it stays clear of.>
    //
    // Install: copy this file to GameData/ELK/PluginData/ELK_<Tab>.cfg
    // (replacing the existing one) with KSP closed. See ../README.txt.

Useful things to mention in that header: whether the layout assumes a
specific keyboard layout, which key sets it expects, and any other mod
whose default bindings you deliberately worked around. Keys that are
unbound in stock KSP are the safest material to build a preset from.
