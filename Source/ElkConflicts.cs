// Adapted from KRILL's KrillConflicts.cs (github.com/Rjoande/KRILL, same
// author, MIT): the stock-GameSettings reflection scan is unchanged (this
// is exactly what would have caught the Brakes conflict automatically had
// it existed when SBT shipped, see README's "Why not B" section). KRILL's
// group/set dual-keymap scan is replaced with a single flat scan over
// ELK's own slot registry, since ELK has no equivalent grouping concept.
//
// Downgraded to C# 5 syntax to build with the legacy csc, same as the rest
// of ELK's ported core.

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	/// <summary>
	/// Non-blocking conflict advisory for a candidate bind: produces
	/// human-readable descriptions of what else already uses the
	/// candidate's primary key. Checks both ELK's own slot bindings and
	/// every stock KeyBinding on GameSettings (reflection: stock exposes no
	/// single "all keybindings" list).
	///
	/// Conflict heuristic, ELK vs ELK: two binds conflict when one press
	/// can fire both, i.e. same primary and nested modifier sets (see
	/// ElkBind.ConflictsWith) - "RightControl+Keypad1" and
	/// "RightShift+Keypad1" do not conflict. Stock keybindings and the
	/// native hotkeys of supported mods have no modifiers, so for them the
	/// primary alone decides: a bare key fires under any held modifier.
	/// </summary>
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

			foreach (FieldInfo field in typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType != typeof(KeyBinding))
					continue;
				KeyBinding kb = (KeyBinding)field.GetValue(null);
				if (kb == null)
					continue;
				if (kb.primary != null && !kb.primary.isNone && kb.primary.code == candidate.primary)
				{
					hits.Add("stock '" + field.Name + "'");
				}
				else if (kb.secondary != null && !kb.secondary.isNone && kb.secondary.code == candidate.primary)
				{
					hits.Add("stock '" + field.Name + "' (secondary)");
				}
			}

			// Native hotkeys of supported third-party mods, described by the
			// mod's own group (only installed mods with hotkeys switched on,
			// only groups that declare a scanner). Advisory like everything
			// else here: a scanner that throws costs one warning, not the
			// capture.
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
