// Flight-scene runtime: fires each bound slot's action on a matching
// keypress. All the actual logic (cfg I/O, bind matching, slot actions)
// lives in ElkConfig/ElkBind/ElkSlots — this class is just the driver loop,
// same shape as SBT's ToggleStockBrakeAddon.

using UnityEngine;

namespace ELK
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ElkAddon : MonoBehaviour
	{
		public void Awake()
		{
			ElkConfig.Load();
		}

		public void Update()
		{
			if (!ElkConfig.Enabled)
				return;

			if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null)
				return;

			// Skip while a text field has keyboard focus (e.g. renaming a
			// vessel/maneuver) — same guard SBT already uses.
			if (InputLockManager.IsLocked(ControlTypes.KEYBOARDINPUT))
				return;

			Vessel vessel = FlightGlobals.ActiveVessel;
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				ElkSlot slot = ElkSlots.All[i];
				ElkBind bind;
				if (!ElkConfig.Binds.TryGetValue(slot.id, out bind) || bind.IsNone)
					continue;
				if (bind.Matches())
				{
					slot.fire(vessel);
				}
			}
		}
	}
}
