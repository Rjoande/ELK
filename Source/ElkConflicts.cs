// Non-blocking conflict warnings, adapted from KRILL's KrillConflicts (MIT):
// other ELK slots, stock KeyBindings and native hotkeys of supported mods.

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	/// <summary>Non-blocking conflict advisory: ELK slots (same primary, nested
	/// modifiers) and stock KeyBindings via reflection. Stock and mod hotkeys
	/// have no modifiers, so only the primary counts for them.</summary>
	public static class ElkConflicts
	{
		/// <summary>excludeSlotId: the slot being edited, skipped so a bind never "conflicts" with its own current value.</summary>
		public static List<string> Describe(ElkBind candidate, string excludeSlotId, Dictionary<string, ElkBind> slotBinds)
		{
			List<string> hits = new List<string>();
			if (candidate == null || candidate.IsNone)
				return hits;

			foreach (KeyValuePair<string, ElkBind> kv in slotBinds)
			{
				if (kv.Key == excludeSlotId)
					continue;
				// Two slots in different key sets can never be active
				// together, so sharing a key between them is the point,
				// not a conflict (see ElkSets.CanCoexist).
				if (candidate.ConflictsWith(kv.Value) && ElkSets.CanCoexist(excludeSlotId, kv.Key))
				{
					hits.Add("ELK '" + kv.Key + "' (" + kv.Value.Describe() + ")");
				}
			}

			// Stock keybindings hold the physical joystick index: compare
			// against where the candidate's device is right now (None while
			// it is not connected: nothing can clash with it).
			KeyCode physical = ElkJoysticks.Physical(candidate.primary);
			foreach (FieldInfo field in typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType != typeof(KeyBinding) || physical == KeyCode.None)
					continue;
				KeyBinding kb = (KeyBinding)field.GetValue(null);
				if (kb == null)
					continue;
				if (kb.primary != null && !kb.primary.isNone && kb.primary.code == physical)
				{
					hits.Add("stock '" + field.Name + "'");
				}
				else if (kb.secondary != null && !kb.secondary.isNone && kb.secondary.code == physical)
				{
					hits.Add("stock '" + field.Name + "' (secondary)");
				}
			}

			// Native hotkeys of installed supported mods (groups with a scanner and
			// hotkeys on). A throwing scanner costs one warning.
			for (int i = 0; i < ElkGroups.All.Count; i++)
			{
				ElkGroup group = ElkGroups.All[i];
				if (group.conflicts == null || !group.IsInstalled || !ElkConfig.GroupHotkeysEnabled(group.id))
					continue;
				try
				{
					List<string> modHits = group.conflicts(candidate);
					if (modHits != null)
						hits.AddRange(modHits);
				}
				catch (System.Exception e)
				{
					Debug.LogWarning("[ELK] " + group.tabLabel + " conflict scan failed: " + e.Message);
				}
			}
			return hits;
		}
	}
}
