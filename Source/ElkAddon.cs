// Flight-scene runtime: fires each bound slot's action on a matching
// keypress. All the actual logic (cfg I/O, bind matching, slot actions)
// lives in ElkConfig/ElkBind/ElkSlots — this class is just the driver loop,
// same shape as SBT's ToggleStockBrakeAddon.

using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ElkAddon : MonoBehaviour
	{
		// Shorter than any intentional double press of a bug encoder,
		// longer than the frame or two between duplicated key-down events.
		private const float RepeatGuardSeconds = 0.15f;

		private readonly Dictionary<string, float> lastFire = new Dictionary<string, float>();

		public void Awake()
		{
			ElkConfig.Load();
			GameEvents.onVesselChange.Add(OnVesselChange);
		}

		public void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(OnVesselChange);
		}

		// Fires on entering flight and on every vessel switch: with key sets
		// in use, tell the player which set this vessel is in.
		private void OnVesselChange(Vessel vessel)
		{
			if (ElkConfig.Enabled && ElkSets.Enabled && vessel != null)
			{
				ElkSets.Announce(ElkSets.Current(vessel));
			}
		}

		// Stock's own lock id for map view (MapView.enterMapView, reshaped
		// by NavBallToggle as the navball is shown or hidden there).
		private const string MapViewLockId = "MapView";

		// True while a text field has keyboard focus (renaming a vessel or
		// a maneuver...) or anything else locks the keyboard - same guard
		// SBT already uses. InputLockManager.IsLocked(mask) is true as soon
		// as ONE bit of the mask is locked, and map view always holds a lock
		// that overlaps KEYBOARDINPUT (every ship control with the navball
		// hidden, action groups and staging with it shown), so in map view
		// that one lock is left out and every other lock still counts.
		// Verified against KSP 1.12.5 Assembly-CSharp.dll, decompiled.
		private static bool KeyboardLocked(bool inMap)
		{
			if (!InputLockManager.IsLocked(ControlTypes.KEYBOARDINPUT))
				return false;
			if (!inMap)
				return true;

			ulong others = 0uL;
			foreach (KeyValuePair<string, ulong> entry in InputLockManager.lockStack)
			{
				if (entry.Key != MapViewLockId)
					others |= entry.Value;
			}
			return (others & (ulong)ControlTypes.KEYBOARDINPUT) != 0uL;
		}

		public void Update()
		{
			if (!ElkConfig.Enabled)
				return;

			if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null)
				return;

			bool inMap = MapView.MapIsEnabled;
			if (KeyboardLocked(inMap))
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
					// Group switched off in its cfg (hotkeys = false), or
					// third-party slot whose mod is not installed: the bind
					// is kept but the press is a no-op.
					if (!ElkConfig.GroupHotkeysEnabled(slot.group) || !slot.IsAvailable)
						continue;
					// Slot assigned to a key set other than this vessel's
					// current one: silent, the same key may belong to
					// another slot in the active set.
					if (!ElkSets.SlotActive(slot.id, vessel))
						continue;
					// Map view: only the slots flagged for it (map = true in
					// their cfg node) fire; set switching always does.
					if (inMap && slot.group != ElkGroups.Global && !ElkConfig.GetSlotMap(slot.id))
					{
						Debug.Log("[ELK] " + slot.id + ": not fired in map view (map = false)");
						continue;
					}

					// Some controllers deliver one physical press as two
					// key-down events a frame or two apart, which would flip
					// a toggle twice. A second fire of the same slot within
					// RepeatGuardSeconds is dropped; both cases are logged so
					// a duplicate is visible in Player.log.
					float now = Time.unscaledTime;
					float last;
					if (lastFire.TryGetValue(slot.id, out last) && now - last < RepeatGuardSeconds)
					{
						Debug.Log("[ELK] " + slot.id + ": repeat " + ((now - last) * 1000f).ToString("F0") + " ms after the last press ignored (frame " + Time.frameCount + ")");
						continue;
					}
					lastFire[slot.id] = now;
					Debug.Log("[ELK] " + slot.id + " fired (" + bind.Describe() + ", frame " + Time.frameCount + ")");
					slot.fire(vessel);
				}
			}
		}
	}
}
