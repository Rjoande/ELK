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
				if (!vessel.Autopilot.CanSetMode(mode))
					return;
				if (!vessel.ActionGroups[KSPActionGroup.SAS])
				{
					vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
				}
				if (vessel.Autopilot.Mode != mode)
				{
					vessel.Autopilot.Enable(mode);
				}
			};
		}
	}
}
