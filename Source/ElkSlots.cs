// Single registry of every ELK hotkey slot. To propose a new one, add an
// entry here — see CONTRIBUTING.md. No other file needs to change:
// ElkAddon reads this list to build its bind map, ElkWindow reads it to
// render the toolbar's rows.
//
// SAS slot actions correct a gap in the original (2016, GPL-3.0,
// unrelated) SASHotkeys project: CanSetMode() is checked first, so a press
// for e.g. Target/Maneuver with nothing selected silently no-ops instead of
// forcing an invalid autopilot state. Confirmed against the installed KSP
// 1.12.5 Assembly-CSharp.dll (decompiled): VesselAutopilot.Enable(mode)
// re-engages fly-by-wire in the given mode and is only called when the mode
// actually changes, avoiding a redundant reset on repeated presses of the
// same hotkey.

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

		public ElkSlot(string id, string displayName, Action<Vessel> fire)
		{
			this.id = id;
			this.displayName = displayName;
			this.fire = fire;
		}
	}

	public static class ElkSlots
	{
		public static readonly List<ElkSlot> All = new List<ElkSlot>
		{
			new ElkSlot("BRAKES_TOGGLE", "Brakes: toggle", FireBrakesToggle),

			new ElkSlot("SAS_STABILITY", "SAS: Stability Assist", SasSlot(VesselAutopilot.AutopilotMode.StabilityAssist)),
			new ElkSlot("SAS_PROGRADE", "SAS: Prograde", SasSlot(VesselAutopilot.AutopilotMode.Prograde)),
			new ElkSlot("SAS_RETROGRADE", "SAS: Retrograde", SasSlot(VesselAutopilot.AutopilotMode.Retrograde)),
			new ElkSlot("SAS_NORMAL", "SAS: Normal", SasSlot(VesselAutopilot.AutopilotMode.Normal)),
			new ElkSlot("SAS_ANTINORMAL", "SAS: Anti-normal", SasSlot(VesselAutopilot.AutopilotMode.Antinormal)),
			new ElkSlot("SAS_RADIAL_IN", "SAS: Radial in", SasSlot(VesselAutopilot.AutopilotMode.RadialIn)),
			new ElkSlot("SAS_RADIAL_OUT", "SAS: Radial out", SasSlot(VesselAutopilot.AutopilotMode.RadialOut)),
			new ElkSlot("SAS_TARGET", "SAS: Target", SasSlot(VesselAutopilot.AutopilotMode.Target)),
			new ElkSlot("SAS_ANTITARGET", "SAS: Anti-target", SasSlot(VesselAutopilot.AutopilotMode.AntiTarget)),
			new ElkSlot("SAS_MANEUVER", "SAS: Maneuver node", SasSlot(VesselAutopilot.AutopilotMode.Maneuver)),
		};

		private static void FireBrakesToggle(Vessel vessel)
		{
			vessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
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
	}
}
