// Key sets: named layers a slot can belong to, switched by SET_NEXT/SET_PREV.
// Active set is global (ELK.cfg) or per vessel (ElkVesselModule). No sets
// declared = every slot always active.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	public static class ElkSets
	{
		public const string OptSets = "sets";
		public const string OptActive = "active_set";
		public const string OptPerVessel = "sets_per_vessel";
		public const string OptDisengage = "sets_disengage";

		/// <summary>Declared set names in rotation order, trimmed, no blanks, no duplicates. Empty = feature off.</summary>
		public static List<string> Names
		{
			get
			{
				List<string> names = new List<string>();
				string raw = ElkConfig.GetOption(ElkGroups.Global, OptSets);
				if (string.IsNullOrEmpty(raw))
					return names;
				string[] parts = raw.Split(',');
				for (int i = 0; i < parts.Length; i++)
				{
					string name = parts[i].Trim();
					if (name.Length == 0 || names.Contains(name))
						continue;
					names.Add(name);
				}
				return names;
			}
		}

		public static bool Enabled
		{
			get { return Names.Count > 0; }
		}

		public static bool PerVessel
		{
			get { return ElkConfig.GetBool(ElkGroups.Global, OptPerVessel, true); }
		}

		/// <summary>The global default set: active_set if it is a declared name, else the first declared name, else "".</summary>
		public static string GlobalActive
		{
			get
			{
				List<string> names = Names;
				if (names.Count == 0)
					return "";
				string raw = ElkConfig.GetOption(ElkGroups.Global, OptActive);
				return (raw != null && names.Contains(raw.Trim())) ? raw.Trim() : names[0];
			}
		}

		/// <summary>The set in force for this vessel (its own memory when sets_per_vessel is on and it has one), else the global default.</summary>
		public static string Current(Vessel vessel)
		{
			List<string> names = Names;
			if (names.Count == 0)
				return "";
			if (PerVessel && vessel != null)
			{
				ElkVesselModule memory = vessel.FindVesselModuleImplementing<ElkVesselModule>();
				if (memory != null && !string.IsNullOrEmpty(memory.activeSet) && names.Contains(memory.activeSet))
					return memory.activeSet;
			}
			return GlobalActive;
		}

		/// <summary>True if leaving this set switches off what its slots control: sets_disengage absent or "*" = every set, otherwise only the names listed.</summary>
		public static bool DisengageOnExit(string set)
		{
			if (string.IsNullOrEmpty(set))
				return false;
			string raw = ElkConfig.GetOption(ElkGroups.Global, OptDisengage);
			if (raw == null || raw.Trim() == "*")
				return true;
			string[] parts = raw.Split(',');
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i].Trim() == set)
					return true;
			}
			return false;
		}

		/// <summary>Writes sets_disengage: "*" when every declared set is on, else the list of the ones that are (empty = none).</summary>
		public static void SetDisengageOnExit(string set, bool value)
		{
			List<string> names = Names;
			List<string> on = new List<string>();
			for (int i = 0; i < names.Count; i++)
			{
				if (names[i] == set ? value : DisengageOnExit(names[i]))
					on.Add(names[i]);
			}
			ElkConfig.SetOption(ElkGroups.Global, OptDisengage, on.Count == names.Count ? "*" : string.Join(", ", on.ToArray()));
		}

		/// <summary>Makes name the active set: vessel memory in flight (when on), global
		/// default otherwise. Only this path runs Disengage() on the set left;
		/// scene and vessel changes never do.</summary>
		public static void Select(Vessel vessel, string name, bool announce)
		{
			List<string> names = Names;
			if (!names.Contains(name))
				return;
			string previous = Current(vessel);
			bool stored = false;
			if (PerVessel && vessel != null)
			{
				ElkVesselModule memory = vessel.FindVesselModuleImplementing<ElkVesselModule>();
				if (memory != null)
				{
					memory.activeSet = name;
					stored = true;
				}
			}
			if (!stored)
			{
				ElkConfig.SetOption(ElkGroups.Global, OptActive, name);
			}
			if (vessel != null && previous != name)
				Disengage(vessel, previous);
			if (announce)
				Announce(name);
		}

		/// <summary>sets_disengage: each slot of the set being left runs its `off` once per
		/// distinct action; skipped for absent mods and tabs with hotkeys off.
		/// Always-on slots are never touched.</summary>
		private static void Disengage(Vessel vessel, string set)
		{
			if (!DisengageOnExit(set))
				return;
			List<Action<Vessel>> done = new List<Action<Vessel>>();
			List<string> names = new List<string>();
			List<ElkSlot> all = ElkSlots.All;
			for (int i = 0; i < all.Count; i++)
			{
				ElkSlot slot = all[i];
				if (slot.off == null || SetOf(slot.id) != set)
					continue;
				if (!slot.IsAvailable || !ElkConfig.GroupHotkeysEnabled(slot.group))
					continue;
				// Delegate equality: two references to the same static
				// method are the same action, so SmartASS is switched off
				// once, not once per SmartASS slot.
				if (done.Contains(slot.off))
					continue;
				done.Add(slot.off);
				names.Add(slot.off.Method.Name);
				try
				{
					slot.off(vessel);
				}
				catch (Exception e)
				{
					Debug.LogWarning("[ELK] set '" + set + "' left: " + slot.off.Method.Name + " failed: " + e);
				}
			}
			if (done.Count > 0)
				Debug.Log("[ELK] set '" + set + "' left: switched off " + string.Join(", ", names.ToArray()));
		}

		/// <summary>Rotates the active set by delta (+1 next, -1 previous), wrapping around.</summary>
		public static void Step(Vessel vessel, int delta)
		{
			List<string> names = Names;
			if (names.Count == 0)
				return;
			int index = names.IndexOf(Current(vessel));
			if (index < 0)
				index = 0;
			index = ((index + delta) % names.Count + names.Count) % names.Count;
			Select(vessel, names[index], true);
		}

		public static void Announce(string name)
		{
			List<string> names = Names;
			int index = names.IndexOf(name);
			string text = "ELK key set: " + name;
			if (index >= 0)
				text += " (" + (index + 1) + "/" + names.Count + ")";
			ScreenMessages.PostScreenMessage(text, 2f, ScreenMessageStyle.UPPER_CENTER);
		}

		/// <summary>The set a slot is assigned to ("" = always), ignoring names that are not declared.</summary>
		public static string SetOf(string slotId)
		{
			if (slotId == null)
				return "";
			string set = ElkConfig.GetSlotSet(slotId);
			return (set.Length > 0 && Names.Contains(set)) ? set : "";
		}

		/// <summary>True if the slot may fire on this vessel right now: sets off, slot always-on, or its set is the current one.</summary>
		public static bool SlotActive(string slotId, Vessel vessel)
		{
			if (!Enabled)
				return true;
			string set = SetOf(slotId);
			return set.Length == 0 || set == Current(vessel);
		}

		/// <summary>True if two slots can be active at the same time (so a shared key is a real conflict): sets off, either always-on, or same set.</summary>
		public static bool CanCoexist(string slotA, string slotB)
		{
			if (!Enabled)
				return true;
			string a = SetOf(slotA);
			string b = SetOf(slotB);
			return a.Length == 0 || b.Length == 0 || a == b;
		}

		public static string DisplayName(string set)
		{
			return set.Length == 0 ? "always" : set;
		}
	}
}
