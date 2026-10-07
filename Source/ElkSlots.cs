// Single registry of every hotkey slot (see CONTRIBUTING.md). `off` = what
// "switched off" means for a set change; `mapDefault` = fires in map view
// when the cfg is silent. Third-party slots call a bridge by reflection.

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

		/// <summary>Whether the slot fires in map view when its cfg node has no "map" value.</summary>
		public bool mapDefault;

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

			new ElkSlot("SAS_STABILITY", "SAS: Stability Assist", SasSlot(VesselAutopilot.AutopilotMode.StabilityAssist), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_PROGRADE", "SAS: Prograde", SasSlot(VesselAutopilot.AutopilotMode.Prograde), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_RETROGRADE", "SAS: Retrograde", SasSlot(VesselAutopilot.AutopilotMode.Retrograde), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_NORMAL", "SAS: Normal", SasSlot(VesselAutopilot.AutopilotMode.Normal), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_ANTINORMAL", "SAS: Anti-normal", SasSlot(VesselAutopilot.AutopilotMode.Antinormal), FireSasOff) { mapDefault = true },
			// Stock's RadialIn/RadialOut are inverted relative to the navball (RadialIn
			// faces vessel.upAxis, verified decompiled), so each slot maps to the
			// opposite enum value.
			new ElkSlot("SAS_RADIAL_IN", "SAS: Radial in", SasSlot(VesselAutopilot.AutopilotMode.RadialOut), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_RADIAL_OUT", "SAS: Radial out", SasSlot(VesselAutopilot.AutopilotMode.RadialIn), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_TARGET", "SAS: Target", SasSlot(VesselAutopilot.AutopilotMode.Target), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_ANTITARGET", "SAS: Anti-target", SasSlot(VesselAutopilot.AutopilotMode.AntiTarget), FireSasOff) { mapDefault = true },
			new ElkSlot("SAS_MANEUVER", "SAS: Maneuver node", SasSlot(VesselAutopilot.AutopilotMode.Maneuver), FireSasOff) { mapDefault = true },

			// --- AtmosphereAutopilot --- All through ElkAaBridge; no-op when AA is
			// absent or has no module map yet. aa_autoengage applies to Cruise and FBW
			// keys; select, FD and APR always switch on; the rest need an existing module.
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
			// AtmosphereApproach (AA add-on): row shown only with its DLL
			// loaded. Selects the Approach controller first, like AAPR's own
			// hotkey; arming without a tuned runway is refused by AAPR.
			new ElkSlot("AA_APR_TOGGLE", "APR: Arm/disarm", ElkAaBridge.ApproachArmToggle, ElkGroups.AtmosphereAutopilot, ElkAaBridge.IsApproachInstalled, ElkAaBridge.MasterOff),

			// --- MechJeb2, tab 1: MechJeb's own action-group actions, by name ---
			// No-op without MechJeb aboard or while the module is Hidden.
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

			// --- MechJeb2, tab 2 ("+") --- Gates as in MechJeb's windows: NODE needs a
			// node, TARGET+/- a target, Land at target a position target and a vessel
			// not landed, Abort a running landing, Execute a node with a burn vector.
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

			// --- NavUtilities --- Like a left click on its toolbar button (HSI
			// window); no-op if its Flight addon is not awake.
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

		// `off` of the stock slots: the same flips as the B and T keys. Clearing
		// SAS makes VesselAutopilot.Update() call Disable() later in the frame.
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

				// The !Enabled half matters: SetGroup() only flips the flag and
				// VesselAutopilot.Update() later calls Enable() = StabilityAssist, so Mode
				// equal to the request must still call Enable(mode). Verified decompiled.
				if (!vessel.Autopilot.Enabled || vessel.Autopilot.Mode != mode)
				{
					vessel.Autopilot.Enable(mode);
				}

				// Only reached when the key did select a mode: a press that
				// no-ops above leaves a hidden navball hidden.
				if (ElkConfig.SasNavball)
				{
					ElkNavball.Show();
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
