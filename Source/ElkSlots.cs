// Single registry of every ELK hotkey slot. To propose a new one, add an
// entry here — see CONTRIBUTING.md. No other file needs to change:
// ElkAddon reads this list to build its bind map, ElkWindow reads it to
// render the toolbar's rows, ElkConfig reads it to know which cfg file
// (group) a slot lives in.
//
// SAS slot actions correct a gap in the original (2016, GPL-3.0,
// unrelated) SASHotkeys project: CanSetMode() is checked first, so a press
// for e.g. Target/Maneuver with nothing selected silently no-ops instead of
// forcing an invalid autopilot state. Confirmed against the installed KSP
// 1.12.5 Assembly-CSharp.dll (decompiled): VesselAutopilot.Enable(mode)
// re-engages fly-by-wire in the given mode and is only called when the mode
// actually changes, avoiding a redundant reset on repeated presses of the
// same hotkey.
//
// Third-party slots (group != Stock) reach their mod through a bridge in
// Source/Bridges/ by reflection only; with the mod absent they are hidden
// from the window and a no-op in flight (see ElkSlot.IsAvailable).
//
// `off` is what "switched off" means for a slot: the action ElkSets runs
// for every slot of a key set when that set is left in flight
// (sets_disengage), so the set coming in never fights an autopilot the
// set going out engaged. Slots that hold nothing engaged (window toggles,
// PANIC, set switching) have none. Always-on slots are never switched off.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	public class ElkSlot
	{
		public readonly string id;
		public readonly string displayName;
		public readonly Action<Vessel> fire;

		/// <summary>One of the ElkGroups ids: the window tab and the cfg file this slot belongs to.</summary>
		public readonly string group;

		/// <summary>Null for stock; otherwise the bridge's cached "mod installed" probe.</summary>
		public readonly Func<bool> available;

		/// <summary>Switches off what this slot controls when its key set is left (sets_disengage); null = nothing to switch off.</summary>
		public readonly Action<Vessel> off;

		public ElkSlot(string id, string displayName, Action<Vessel> fire)
			: this(id, displayName, fire, ElkGroups.Squad, null, null)
		{
		}

		public ElkSlot(string id, string displayName, Action<Vessel> fire, Action<Vessel> off)
			: this(id, displayName, fire, ElkGroups.Squad, null, off)
		{
		}

		public ElkSlot(string id, string displayName, Action<Vessel> fire, string group, Func<bool> available)
			: this(id, displayName, fire, group, available, null)
		{
		}

		public ElkSlot(string id, string displayName, Action<Vessel> fire, string group, Func<bool> available, Action<Vessel> off)
		{
			this.id = id;
			this.displayName = displayName;
			this.fire = fire;
			this.group = group;
			this.available = available;
			this.off = off;
		}

		public bool IsAvailable
		{
			get { return available == null || available(); }
		}
	}

	public static class ElkSlots
	{
		public static readonly List<ElkSlot> All = new List<ElkSlot>
		{
			// --- Global (ELK.cfg): key-set switching, always active by nature ---
			// No-op while no set is declared in ELK.cfg (see ElkSets).
			new ElkSlot("SET_NEXT", "Key set: next", FireSetNext, ElkGroups.Global, null),
			new ElkSlot("SET_PREV", "Key set: previous", FireSetPrev, ElkGroups.Global, null),

			// --- Squad (ELK_Squad.cfg): stock functions ---
			new ElkSlot("BRAKES_TOGGLE", "Brakes: toggle", FireBrakesToggle, FireBrakesOff),

			new ElkSlot("SAS_STABILITY", "SAS: Stability Assist", SasSlot(VesselAutopilot.AutopilotMode.StabilityAssist), FireSasOff),
			new ElkSlot("SAS_PROGRADE", "SAS: Prograde", SasSlot(VesselAutopilot.AutopilotMode.Prograde), FireSasOff),
			new ElkSlot("SAS_RETROGRADE", "SAS: Retrograde", SasSlot(VesselAutopilot.AutopilotMode.Retrograde), FireSasOff),
			new ElkSlot("SAS_NORMAL", "SAS: Normal", SasSlot(VesselAutopilot.AutopilotMode.Normal), FireSasOff),
			new ElkSlot("SAS_ANTINORMAL", "SAS: Anti-normal", SasSlot(VesselAutopilot.AutopilotMode.Antinormal), FireSasOff),
			// Stock's enum names are the wrong way round: AutopilotMode.RadialIn
			// orients the vessel along vessel.upAxis (away from the body, the
			// navball's radial-out marker) and RadialOut along -upAxis
			// (verified on KSP 1.12.5 Assembly-CSharp.dll, decompiled;
			// confirmed in flight). The slot ids and labels follow the
			// navball, so each maps to the opposite enum value.
			new ElkSlot("SAS_RADIAL_IN", "SAS: Radial in", SasSlot(VesselAutopilot.AutopilotMode.RadialOut), FireSasOff),
			new ElkSlot("SAS_RADIAL_OUT", "SAS: Radial out", SasSlot(VesselAutopilot.AutopilotMode.RadialIn), FireSasOff),
			new ElkSlot("SAS_TARGET", "SAS: Target", SasSlot(VesselAutopilot.AutopilotMode.Target), FireSasOff),
			new ElkSlot("SAS_ANTITARGET", "SAS: Anti-target", SasSlot(VesselAutopilot.AutopilotMode.AntiTarget), FireSasOff),
			new ElkSlot("SAS_MANEUVER", "SAS: Maneuver node", SasSlot(VesselAutopilot.AutopilotMode.Maneuver), FireSasOff),

			// --- AtmosphereAutopilot ---
			// All through ElkAaBridge (reflection). Every slot is a no-op
			// when AA is absent, when AA has no module map for the active
			// vessel yet, or - for Cruise/FBW slots - when that controller
			// is off and aa_autoengage is false. See the bridge header for
			// the AA members behind each action.
			new ElkSlot("AA_MASTER_TOGGLE", "AA: Master switch", ElkAaBridge.ToggleMaster, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_FD_TOGGLE", "AA: FD (Cruise <-> FBW)", ElkAaBridge.ToggleFd, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_GUI_TOGGLE", "AA: Show/hide window", ElkAaBridge.ToggleGui, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_SELECT_FBW", "AA: Standard FBW", ElkAaBridge.SelectFbw, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_SELECT_CRUISE", "AA: Cruise Flight", ElkAaBridge.SelectCruise, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_LEVEL", "CF: Level (LVL)", ElkAaBridge.CruiseLevel, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			// Heading hold is refused by AA itself beyond 80 deg of latitude.
			new ElkSlot("AA_CF_HEADING", "CF: Heading (HDG)", ElkAaBridge.CruiseHeading, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_WAYPOINT", "CF: Waypoint (NAV)", ElkAaBridge.CruiseWaypoint, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_VS_TOGGLE", "CF: V/S engage/disengage", ElkAaBridge.CruiseVsToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_ALT_TOGGLE", "CF: ALT engage/disengage", ElkAaBridge.CruiseAltToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_VERTICAL_TOGGLE", "CF: Vertical motion on/off", ElkAaBridge.CruiseVerticalToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_SETPOINT_TYPE_TOGGLE", "CF: ALT <-> V/S-FPA", ElkAaBridge.CruiseSetpointTypeToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_KEYS_MODE_TOGGLE", "CF: Keys input mode", ElkAaBridge.CruiseKeysModeToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_VS_UP", "CF: V/S bug up", ElkAaBridge.CruiseVsUp, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_VS_DOWN", "CF: V/S bug down", ElkAaBridge.CruiseVsDown, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_ALT_UP", "CF: ALT bug up", ElkAaBridge.CruiseAltUp, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_ALT_DOWN", "CF: ALT bug down", ElkAaBridge.CruiseAltDown, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_ALT_SYNC", "CF: ALT bug = current", ElkAaBridge.CruiseAltSync, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_HDG_UP", "CF: HDG bug up", ElkAaBridge.CruiseHdgUp, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_HDG_DOWN", "CF: HDG bug down", ElkAaBridge.CruiseHdgDown, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_CF_HDG_SYNC", "CF: HDG bug = current", ElkAaBridge.CruiseHdgSync, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_FBW_MODERATION", "FBW: Moderation", ElkAaBridge.FbwModeration, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_FBW_ROCKET", "FBW: Rocket mode", ElkAaBridge.FbwRocketMode, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_FBW_COORD_TURN", "FBW: Coordinated turn", ElkAaBridge.FbwCoordTurn, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			// Flag only: takes effect while a controller using the thrust
			// controller is active (AA's own rule), silent otherwise.
			new ElkSlot("AA_SPEED_CONTROL", "AA: Speed control", ElkAaBridge.SpeedControlToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),
			new ElkSlot("AA_THRUST_BALANCING", "AA: Thrust balancing", ElkAaBridge.ThrustBalancingToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsInstalled, ElkAaBridge.MasterOff),

			// --- MechJeb2, tab 1: MechJeb's own action-group actions, by name ---
			// Every one is a no-op with no MechJeb aboard the active vessel,
			// and MechJeb itself refuses them while the module is Hidden
			// (locked in career). See ElkMjBridge.
			new ElkSlot("MJ_SASS_OFF", "SmartASS: Off", MjAction("OnDeactivateSmartASSAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_KILLROT", "SmartASS: Kill rotation", MjAction("OnKillRotationAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_PROGRADE", "SmartASS: Prograde", MjAction("OnOrbitProgradeAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_RETROGRADE", "SmartASS: Retrograde", MjAction("OnOrbitRetrogradeAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_NORMAL_PLUS", "SmartASS: Normal+", MjAction("OnOrbitNormalAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_NORMAL_MINUS", "SmartASS: Normal-", MjAction("OnOrbitAntinormalAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			// MechJeb's RADIAL_PLUS points away from the body, RADIAL_MINUS
			// towards it (no stock-style inversion here).
			new ElkSlot("MJ_SASS_RADIAL_PLUS", "SmartASS: Radial+", MjAction("OnOrbitRadialOutAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_RADIAL_MINUS", "SmartASS: Radial-", MjAction("OnOrbitRadialInAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_TRANS_OFF", "Translatron: Off", MjAction("OnTranslatronOffAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_TRANS_KEEP_VERT", "Translatron: Keep vertical", MjAction("OnTranslatronKeepVertAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_TRANS_ZERO", "Translatron: Speed = 0", ElkMjBridge.TransZero, ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),  // the window's "0" button: box + setpoint, unlike MechJeb's own action
			new ElkSlot("MJ_TRANS_KILLH_TOGGLE", "Translatron: Kill H/S", ElkMjBridge.TransKillHToggle, ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),  // bridge, not MechJeb's action: honours mj_autoengage
			new ElkSlot("MJ_PANIC", "Translatron: PANIC", MjAction("OnPanicAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled),
			new ElkSlot("MJ_LAND_SOMEWHERE", "Landing: Land somewhere", MjAction("OnLandsomewhereAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.LandAbort),
			new ElkSlot("MJ_LAND_KSC", "Landing: Land at KSC", MjAction("OnLandTargetAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.LandAbort),
			new ElkSlot("MJ_ASCENT_TOGGLE", "Ascent: Autopilot on/off", MjAction("OnAscentAPToggleAction"), ElkGroups.MechJeb2, ElkMjBridge.IsInstalled, ElkMjBridge.AscentOff),

			// --- MechJeb2, tab 2 ("+"): what the actions do not cover ---
			// Gates as in MechJeb's own windows: NODE needs a planned node,
			// TARGET+/- a target, Land at target a position target and a
			// vessel not landed, Abort a running landing, Execute a node
			// with a burn vector (toggles to Abort while running).
			new ElkSlot("MJ_SASS_NODE", "SmartASS: Node", ElkMjBridge.SmartAssNode, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_TARGET_PLUS", "SmartASS: Target+", ElkMjBridge.SmartAssTargetPlus, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_SASS_TARGET_MINUS", "SmartASS: Target-", ElkMjBridge.SmartAssTargetMinus, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.SmartAssOff),
			new ElkSlot("MJ_TRANS_KEEP_SURF", "Translatron: Keep surface", ElkMjBridge.TransKeepSurface, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_TRANS_KEEP_ORBIT", "Translatron: Keep orbital", ElkMjBridge.TransKeepOrbital, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_TRANS_SPEED_UP", "Translatron: Speed +step", ElkMjBridge.TransSpeedUp, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_TRANS_SPEED_DOWN", "Translatron: Speed -step", ElkMjBridge.TransSpeedDown, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.TransOff),
			new ElkSlot("MJ_LAND_TARGET", "Landing: Land at target", ElkMjBridge.LandAtTarget, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.LandAbort),
			new ElkSlot("MJ_LAND_ABORT", "Landing: Abort", ElkMjBridge.LandAbort, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled),
			new ElkSlot("MJ_NODE_EXECUTE_TOGGLE", "Node: Execute / abort", ElkMjBridge.NodeExecuteToggle, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled, ElkMjBridge.NodeAbort),
			new ElkSlot("MJ_MENU_TOGGLE", "MechJeb: Show/hide menu", ElkMjBridge.MenuToggle, ElkGroups.MechJeb2Plus, ElkMjBridge.IsInstalled),

			// --- NavUtilities (NavInstruments Continued) ---
			// Same as a left click on its toolbar button: opens or closes
			// the HSI window. No-op if the NavUtilities Flight addon is not
			// awake (see ElkNavBridge).
			new ElkSlot("NAV_HSI_TOGGLE", "NavUtilities: HSI window", FireNavHsiToggle, ElkGroups.NavUtilities, ElkNavBridge.IsInstalled),
		};

		/// <summary>Stock SAS slot -> SmartASS slot with the same meaning (Copy from/to SAS in the MechJeb2 tab). Radial follows the navball on both sides.</summary>
		public static readonly string[,] SasToSmartAss =
		{
			{ "SAS_STABILITY", "MJ_SASS_KILLROT" },
			{ "SAS_PROGRADE", "MJ_SASS_PROGRADE" },
			{ "SAS_RETROGRADE", "MJ_SASS_RETROGRADE" },
			{ "SAS_NORMAL", "MJ_SASS_NORMAL_PLUS" },
			{ "SAS_ANTINORMAL", "MJ_SASS_NORMAL_MINUS" },
			{ "SAS_RADIAL_IN", "MJ_SASS_RADIAL_MINUS" },
			{ "SAS_RADIAL_OUT", "MJ_SASS_RADIAL_PLUS" },
			{ "SAS_TARGET", "MJ_SASS_TARGET_PLUS" },
			{ "SAS_ANTITARGET", "MJ_SASS_TARGET_MINUS" },
			{ "SAS_MANEUVER", "MJ_SASS_NODE" },
		};

		private static Action<Vessel> MjAction(string actionName)
		{
			return delegate(Vessel vessel) { ElkMjBridge.FireAction(vessel, actionName); };
		}

		private static void FireSetNext(Vessel vessel)
		{
			ElkSets.Step(vessel, 1);
		}

		private static void FireSetPrev(Vessel vessel)
		{
			ElkSets.Step(vessel, -1);
		}

		private static void FireBrakesToggle(Vessel vessel)
		{
			vessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
		}

		// `off` actions of the stock slots (sets_disengage): the same group
		// flips the B and T keys make. Clearing the SAS group is what
		// disengages the autopilot: VesselAutopilot.Update() sees the flag
		// down and calls Disable() later in the frame.
		private static void FireBrakesOff(Vessel vessel)
		{
			vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
		}

		private static void FireSasOff(Vessel vessel)
		{
			vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
		}

		private static void FireNavHsiToggle(Vessel vessel)
		{
			ElkNavBridge.ToggleHsi();
		}

		private static Action<Vessel> SasSlot(VesselAutopilot.AutopilotMode mode)
		{
			return delegate(Vessel vessel)
			{
				bool sasOn = vessel.ActionGroups[KSPActionGroup.SAS];

				// With SAS off, a mode key is only meaningful if we are
				// allowed to switch SAS on for the player ("as if T had been
				// pressed first"); otherwise the press is a deliberate no-op.
				if (!sasOn && !ElkConfig.SasAutoEngage)
					return;

				// CanSetMode() first, before touching the action group: a
				// Maneuver/Target key with nothing selected must not engage
				// SAS as a side effect of doing nothing else.
				if (!vessel.Autopilot.CanSetMode(mode))
					return;

				if (!sasOn)
				{
					vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
				}

				// The !Enabled half of this test matters and is easy to miss:
				// ActionGroupList.SetGroup() only flips the group flag, it
				// does not engage the autopilot. VesselAutopilot.Update()
				// picks that up later in the frame and, finding itself not
				// yet enabled, calls the parameterless Enable() - which is
				// Enable(StabilityAssist). So if we skipped Enable(mode) just
				// because Autopilot.Mode already equalled the requested mode
				// (its last value persists while SAS is off), SAS would come
				// up in Stability Assist instead of the mode asked for.
				// Verified against KSP 1.12.5 Assembly-CSharp.dll, decompiled.
				if (!vessel.Autopilot.Enabled || vessel.Autopilot.Mode != mode)
				{
					vessel.Autopilot.Enable(mode);
				}
			};
		}

		/// <summary>The slot with this id, or null.</summary>
		public static ElkSlot Find(string id)
		{
			for (int i = 0; i < All.Count; i++)
			{
				if (All[i].id == id)
					return All[i];
			}
			return null;
		}
	}
}
