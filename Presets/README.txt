==============================================================================
 ELK - Presets
==============================================================================

A preset is nothing more than a ready-made ELK.cfg: the same config file the
mod ships with, but with the "key" fields already filled in. There is no
preset format, no preset loader, no in-game preset browser - installing one
means putting its ELK.cfg where the mod looks for it.


------------------------------------------------------------------------------
 LAYOUT OF THIS FOLDER
------------------------------------------------------------------------------

    Presets/
        README.txt              <- this file
        <PresetName>/
            ELK.cfg             <- the preset itself
        <AnotherPreset>/
            ELK.cfg

One folder per preset, named after the preset; the file inside is always
called ELK.cfg. The file name is not a convention you can bend: ELK reads
exactly one hard-coded path and never scans for alternatives (see "Why the
name matters" below). Giving each preset its own folder is what lets several
of them coexist here without renaming the file.


------------------------------------------------------------------------------
 INSTALLING A PRESET
------------------------------------------------------------------------------

  1. Quit KSP (or at least leave the Space Center and Flight scenes, and
     close the ELK toolbar window - the toolbar rewrites the config file
     whenever it captures or clears a key, and would overwrite your new
     file).

  2. Back up your current config if you care about it:

         GameData/ELK/PluginData/ELK.cfg   ->   ELK.cfg.bak

  3. Copy the preset's ELK.cfg over that same path, replacing the existing
     file:

         GameData/ELK/PluginData/ELK.cfg

     Copy the file itself, not the preset folder: there must be no
     "<PresetName>" directory left in PluginData.

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

ELK builds its config path from its own assembly location: the folder above
Plugins/ (that is, the mod root), then PluginData/ELK.cfg. Both the folder
and the file name are fixed in code. A file called MyPreset.cfg, or an
ELK.cfg sitting one folder deeper, is simply never read - the mod logs
"config not found" and every hotkey stays unbound.

That path also means a preset only ever applies to the ELK install it is
copied into: it is a per-install file, not a per-save one.


------------------------------------------------------------------------------
 MAKING YOUR OWN PRESET
------------------------------------------------------------------------------

Easiest way: bind the keys you want in-game through the toolbar, quit KSP,
then copy GameData/ELK/PluginData/ELK.cfg out - that file is already a
valid preset.

One caveat: the toolbar writes the file through KSP's own ConfigNode writer,
which does not preserve "//" comments. A cfg that has been through a capture
has lost the shipped explanatory header, so add a short one back by hand
before sharing (see the template below).

Hand-editing works just as well. The structure is:

    ELK
    {
        enabled = true
        toolbar = true

        BRAKES_TOGGLE
        {
            key = LeftAlt+B
        }
        SAS_RETROGRADE
        {
            key = LeftAlt+R
        }
    }

  - Each node name, each opening brace and each closing brace needs its own
    line. Stock KSP's config parser does not handle inline
    "NODE { key = X }" syntax.
  - A slot you leave out entirely is simply unbound - a preset does not have
    to list all eleven.
  - Leave "enabled = true" and "toolbar = true" alone unless the preset has
    a reason to change them; turning the toolbar off in a shared preset hides
    the one UI a user has for fixing your key choices.

KEY FORMAT: optional modifiers separated by "+", then the main key, all
literal UnityEngine.KeyCode names - for example "Y", "LeftAlt+Y",
"LeftControl+LeftShift+G". Names are matched case-insensitively and
surrounding spaces are ignored, but anything that is not a real KeyCode name
makes that one slot unbound (with a warning in KSP.log). An empty value
means "no key".

SLOT IDS (all eleven, with the label the toolbar shows):

    BRAKES_TOGGLE       Brakes: toggle
    SAS_STABILITY       SAS: Stability Assist
    SAS_PROGRADE        SAS: Prograde
    SAS_RETROGRADE      SAS: Retrograde
    SAS_NORMAL          SAS: Normal
    SAS_ANTINORMAL      SAS: Anti-normal
    SAS_RADIAL_IN       SAS: Radial in
    SAS_RADIAL_OUT      SAS: Radial out
    SAS_TARGET          SAS: Target
    SAS_ANTITARGET      SAS: Anti-target
    SAS_MANEUVER        SAS: Maneuver node

ONE KEY TO AVOID: do not bind BRAKES_TOGGLE to whatever key stock Brakes
uses (Settings > Input > BRAKES, "B" by default), not even with a modifier.
Any combo containing B can only ever disengage the brakes, never re-engage
them.


------------------------------------------------------------------------------
 SHARING A PRESET
------------------------------------------------------------------------------

Nothing has to go through this repo: a preset is one small text file, so a
gist, a forum post or a zip works just as well.

To contribute one here, add a folder named after the preset with your
ELK.cfg inside it, and start the file with a header comment saying what it
is:

    // <Preset name> - ELK preset
    // Author: <you>
    // <One or two lines: the idea behind the layout, e.g. which hand it
    // keeps free, which other mod's bindings it stays clear of.>
    //
    // Install: copy this file to GameData/ELK/PluginData/ELK.cfg
    // (replacing the existing one) with KSP closed. See ../README.txt.

Useful things to mention in that header: whether the layout assumes a
specific keyboard layout, and any other mod whose default bindings you
deliberately worked around. Keys that are unbound in stock KSP are the safest
material to build a preset from.
