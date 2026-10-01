// Owns the cfg files in PluginData: one per slot group (ELK.cfg = global
// master, ELK_Squad.cfg = stock slots, ELK_<Mod>.cfg per supported mod,
// see ElkGroups), their loading, the in-memory slot->bind and slot->set
// maps, the generic option store, and writing a single value back to disk
// (used by the toolbar's capture flow and option clicks). Static and
// scene-independent on purpose: ElkAddon (Flight scene) and
// ElkToolbarApp/ElkWindow (Space Center scene) share this one source of
// truth without depending on which scene's MonoBehaviour.Awake() happened
// to run first this session — PluginData's path is computed from the
// assembly's own location, never from a KSPAddon lifecycle callback.
//
// Third-party cfg files are not shipped: ELK writes an empty template the
// first time it loads with that mod installed, and never deletes one (the
// mod may come back, and the player's bindings with it). A file present
// while its mod is absent is still parsed, so nothing is lost.
// ELK_Squad.cfg ships with the mod; if it is missing while ELK.cfg still
// holds the stock slots (an install upgraded from 1.0.x, where everything
// lived in ELK.cfg), ELK creates it by copying those keys over - ELK.cfg
// keeps its old nodes, inert, until the player tidies them.
//
// Known trade-off: every write round-trips the file through ConfigNode's
// own writer, which does not preserve the shipped file's "//" comments.
// The first toolbar-driven change on a fresh install replaces the
// annotated default cfg with an uncommented one — accepted (same
// limitation applies to every ConfigNode-based KSP mod, stock's own
// settings.cfg included); the toolbar window is the in-game source of
// explanation at that point.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ELK
{
	public static class ElkConfig
	{
		public const string Version = "1.0.1";

		private const string OptSasAutoEngage = "sas_autoengage";

		// ELK.dll sits in GameData/ELK/Plugins/, and PluginData is a SIBLING
		// of Plugins/ (both directly under GameData/ELK/), not nested inside
		// it - so the mod root is one level above the assembly's own
		// directory. Getting this wrong silently produces a "config not
		// found" path like ".../ELK/Plugins/PluginData/ELK.cfg" that never
		// matches the actual shipped file - confirmed via KSP.log after a
		// user report that no hotkey fired at all.
		private static readonly string pluginDataDir = Path.Combine(
			Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)),
			"PluginData");

		public static bool Enabled { get; private set; }
		public static bool ToolbarEnabled { get; private set; }

		/// <summary>When true, an SAS mode hotkey switches SAS on if it is off, instead of no-opping (see ElkSlots.SasSlot). Lives in ELK_Squad.cfg; ELK.cfg's old value is the fallback for upgraded installs.</summary>
		public static bool SasAutoEngage
		{
			get { return GetBool(ElkGroups.Squad, OptSasAutoEngage, GetBool(ElkGroups.Global, OptSasAutoEngage, true)); }
		}

		private static readonly Dictionary<string, ElkBind> binds = new Dictionary<string, ElkBind>();

		public static Dictionary<string, ElkBind> Binds
		{
			get { return binds; }
		}

		// Key set each slot is assigned to ("" = always active), from the
		// slot node's "set" value. See ElkSets.
		private static readonly Dictionary<string, string> slotSets = new Dictionary<string, string>();

		public static string GetSlotSet(string slotId)
		{
			string set;
			return slotSets.TryGetValue(slotId, out set) ? set : "";
		}

		// Per-group "hotkeys" switch: false hibernates every slot of that
		// group (and, for third-party groups, every interaction with the
		// mod) while keeping the bindings on disk. Missing = true.
		private static readonly Dictionary<string, bool> groupHotkeys = new Dictionary<string, bool>();

		/// <summary>True unless the group's cfg says hotkeys = false.</summary>
		public static bool GroupHotkeysEnabled(string groupId)
		{
			bool b;
			return !groupHotkeys.TryGetValue(groupId, out b) || b;
		}

		// Every plain "key = value" of every group file, raw, keyed by group
		// then by key: the generic store behind per-mod options (steps,
		// auto-engage, sets...) so bridges never touch ConfigNode themselves.
		private static readonly Dictionary<string, Dictionary<string, string>> options = new Dictionary<string, Dictionary<string, string>>();

		public static string GetOption(string groupId, string key)
		{
			Dictionary<string, string> group;
			string value;
			if (options.TryGetValue(groupId, out group) && group.TryGetValue(key, out value))
				return value;
			return null;
		}

		public static bool GetBool(string groupId, string key, bool fallback)
		{
			bool b;
			string raw = GetOption(groupId, key);
			return (raw != null && bool.TryParse(raw, out b)) ? b : fallback;
		}

		public static float GetFloat(string groupId, string key, float fallback)
		{
			float f;
			string raw = GetOption(groupId, key);
			return (raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) ? f : fallback;
		}

		public static void SetBool(string groupId, string key, bool value)
		{
			// Lowercase on purpose: bool.ToString() would write "True", which
			// parses back fine but reads as an outlier next to the hand-written
			// "enabled = true" / "toolbar = true" the shipped cfg already has.
			SetOption(groupId, key, value ? "true" : "false");
		}

		public static void SetFloat(string groupId, string key, float value)
		{
			SetOption(groupId, key, value.ToString("G", CultureInfo.InvariantCulture));
		}

		/// <summary>Sets an option in memory and persists it to the group's file at once (commit-on-click, like SetBind).</summary>
		public static void SetOption(string groupId, string key, string text)
		{
			ElkGroup group = ElkGroups.Find(groupId);
			if (group == null)
				return;
			Dictionary<string, string> values;
			if (!options.TryGetValue(groupId, out values))
			{
				values = new Dictionary<string, string>();
				options[groupId] = values;
			}
			values[key] = text;

			string path = PathFor(group);
			ConfigNode root;
			ConfigNode node = OpenElkNode(path, out root);
			if (node.HasValue(key))
				node.SetValue(key, text);
			else
				node.AddValue(key, text);
			root.Save(path);
		}

		/// <summary>Full path of a group's cfg file.</summary>
		public static string PathFor(ElkGroup group)
		{
			return Path.Combine(pluginDataDir, group.cfgFileName);
		}

		public static void Load()
		{
			Enabled = true;
			ToolbarEnabled = true;
			binds.Clear();
			slotSets.Clear();
			groupHotkeys.Clear();
			options.Clear();
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				binds[ElkSlots.All[i].id] = new ElkBind();
			}

			StringBuilder summary = new StringBuilder();
			for (int g = 0; g < ElkGroups.All.Count; g++)
			{
				ElkGroup group = ElkGroups.All[g];
				string path = PathFor(group);

				bool created = false;
				if (!File.Exists(path))
				{
					if (group.id == ElkGroups.Squad)
						created = WriteSquadFromLegacy(group, path);
					else if (!group.IsStock && group.IsInstalled)
						created = WriteTemplate(group, path);
				}

				ConfigNode root = ConfigNode.Load(path);
				ConfigNode node = (root == null) ? null : root.GetNode("ELK");
				if (node == null)
				{
					// The two stock files ship with the mod, so their absence
					// is a real problem; a missing third-party file just
					// means that mod is not installed.
					if (group.IsStock)
					{
						Debug.LogWarning("[ELK] config not found or missing ELK node (" + path + "), its hotkeys unbound");
					}
					continue;
				}

				bool b;
				if (group.id == ElkGroups.Global)
				{
					if (bool.TryParse(node.GetValue("enabled"), out b)) Enabled = b;
					if (bool.TryParse(node.GetValue("toolbar"), out b)) ToolbarEnabled = b;
				}
				if (bool.TryParse(node.GetValue("hotkeys"), out b)) groupHotkeys[group.id] = b;

				Dictionary<string, string> values = new Dictionary<string, string>();
				for (int v = 0; v < node.values.Count; v++)
				{
					values[node.values[v].name] = node.values[v].value;
				}
				options[group.id] = values;

				int total = 0;
				int bound = 0;
				for (int i = 0; i < ElkSlots.All.Count; i++)
				{
					ElkSlot slot = ElkSlots.All[i];
					if (slot.group != group.id)
						continue;
					total++;
					ConfigNode slotNode = node.GetNode(slot.id);
					if (slotNode == null)
						continue;
					ElkBind bind = ElkBind.Parse(slotNode.GetValue("key"));
					binds[slot.id] = bind;
					if (!bind.IsNone)
						bound++;
					string set = slotNode.GetValue("set");
					slotSets[slot.id] = (set == null) ? "" : set.Trim();
				}
				if (summary.Length > 0)
					summary.Append(", ");
				summary.Append(group.cfgFileName).Append(' ').Append(bound).Append('/').Append(total);
				if (created)
					summary.Append(" (created)");
				if (!GroupHotkeysEnabled(group.id))
					summary.Append(" (hotkeys off)");
			}

			List<string> sets = ElkSets.Names;
			Debug.Log("[ELK] v" + Version + ": enabled=" + Enabled + " toolbar=" + ToolbarEnabled
				+ " sas_autoengage=" + SasAutoEngage
				+ " sets=" + (sets.Count == 0 ? "off" : string.Join("/", sets.ToArray()) + " default " + ElkSets.GlobalActive + (ElkSets.PerVessel ? " per vessel" : " global"))
				+ "; slots bound: " + summary);
		}

		/// <summary>Sets a slot's bind in memory and immediately persists that slot's group file to disk (toolbar capture/clear commit — no restart needed).</summary>
		public static void SetBind(string slotId, ElkBind bind)
		{
			binds[slotId] = bind;
			SetSlotValue(slotId, "key", bind.ToKeySpec());
		}

		/// <summary>Assigns a slot to a key set ("" = always active) and persists it.</summary>
		public static void SetSlotSet(string slotId, string set)
		{
			set = (set == null) ? "" : set.Trim();
			slotSets[slotId] = set;
			SetSlotValue(slotId, "set", set);
		}

		/// <summary>
		/// Copies binds from one slot to another along a two-column map
		/// (reverse = right to left), overwriting the destination, never
		/// touching the source or the slots' key sets. Returns a report.
		/// </summary>
		public static List<string> CopyBinds(string[,] map, bool reverse, string fromLabel, string toLabel)
		{
			List<string> lines = new List<string>();
			int copied = 0;
			for (int i = 0; i < map.GetLength(0); i++)
			{
				string from = reverse ? map[i, 1] : map[i, 0];
				string to = reverse ? map[i, 0] : map[i, 1];
				ElkBind source;
				if (!binds.TryGetValue(from, out source) || source == null || source.IsNone)
					continue;
				ElkBind copy = ElkBind.Parse(source.ToKeySpec());
				ElkBind previous;
				bool overwritten = binds.TryGetValue(to, out previous) && previous != null && !previous.IsNone
					&& previous.ToKeySpec() != copy.ToKeySpec();
				SetBind(to, copy);
				copied++;
				lines.Add("  " + from + " (" + copy.Describe() + ") -> " + to + (overwritten ? " (replaced " + previous.Describe() + ")" : ""));
			}
			lines.Insert(0, copied == 0 ? "Nothing to copy: no " + fromLabel + " key is bound."
				: "Copied " + copied + " key(s) from " + fromLabel + " to " + toLabel + " (" + fromLabel + " unchanged):");
			return lines;
		}

		/// <summary>The mod-wide master switch ("enabled" in ELK.cfg): off, no hotkey fires anywhere; the toolbar stays so it can be switched back on.</summary>
		public static void SetEnabled(bool value)
		{
			Enabled = value;
			SetBool(ElkGroups.Global, "enabled", value);
		}

		/// <summary>Sets the SAS auto-engage option and persists it to ELK_Squad.cfg, same commit-on-click path as SetBind.</summary>
		public static void SetSasAutoEngage(bool value)
		{
			SetBool(ElkGroups.Squad, OptSasAutoEngage, value);
		}

		/// <summary>Switches a group's hotkeys on or off in memory and persists it to that group's file.</summary>
		public static void SetGroupHotkeys(string groupId, bool value)
		{
			if (ElkGroups.Find(groupId) == null)
				return;
			groupHotkeys[groupId] = value;
			SetBool(groupId, "hotkeys", value);
		}

		private static void SetSlotValue(string slotId, string valueName, string text)
		{
			ElkSlot slot = ElkSlots.Find(slotId);
			ElkGroup group = (slot == null) ? null : ElkGroups.Find(slot.group);
			if (group == null)
			{
				Debug.LogWarning("[ELK] unknown slot " + slotId + ", " + valueName + " not persisted");
				return;
			}
			string path = PathFor(group);

			ConfigNode root;
			ConfigNode node = OpenElkNode(path, out root);
			ConfigNode slotNode = node.GetNode(slotId);
			if (slotNode == null)
			{
				slotNode = node.AddNode(slotId);
			}
			if (slotNode.HasValue(valueName))
			{
				slotNode.SetValue(valueName, text);
			}
			else
			{
				slotNode.AddValue(valueName, text);
			}
			root.Save(path);
		}

		/// <summary>Writes a group's empty template (hotkeys on, the group's default options, every slot node with empty key and set). True on success.</summary>
		private static bool WriteTemplate(ElkGroup group, string path)
		{
			ConfigNode root = new ConfigNode();
			ConfigNode node = root.AddNode("ELK");
			node.AddValue("hotkeys", "true");
			if (group.defaultOptions != null)
			{
				for (int i = 0; i < group.defaultOptions.Length; i++)
				{
					node.AddValue(group.defaultOptions[i].Key, group.defaultOptions[i].Value);
				}
			}
			AddSlotNodes(group, node, null);
			return SaveNew(root, path, group.tabLabel + " detected, created " + path);
		}

		/// <summary>ELK_Squad.cfg missing: recreates it, carrying over the stock keys an old single-file ELK.cfg may still hold.</summary>
		private static bool WriteSquadFromLegacy(ElkGroup group, string path)
		{
			ConfigNode legacyRoot = ConfigNode.Load(PathFor(ElkGroups.Find(ElkGroups.Global)));
			ConfigNode legacy = (legacyRoot == null) ? null : legacyRoot.GetNode("ELK");

			ConfigNode root = new ConfigNode();
			ConfigNode node = root.AddNode("ELK");
			node.AddValue("hotkeys", "true");
			string sasAutoEngage = (legacy == null) ? null : legacy.GetValue(OptSasAutoEngage);
			node.AddValue(OptSasAutoEngage, string.IsNullOrEmpty(sasAutoEngage) ? "true" : sasAutoEngage.Trim());
			int migrated = AddSlotNodes(group, node, legacy);
			return SaveNew(root, path, "created " + path + (migrated > 0 ? " (" + migrated + " keys migrated from ELK.cfg)" : ""));
		}

		/// <summary>Adds one node per slot of every group sharing this group's cfg file (two tabs may share one file); keys (and sets) copied from legacyNode when it has them. Returns how many keys were copied.</summary>
		private static int AddSlotNodes(ElkGroup group, ConfigNode node, ConfigNode legacyNode)
		{
			int copied = 0;
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				ElkSlot slot = ElkSlots.All[i];
				ElkGroup slotGroup = ElkGroups.Find(slot.group);
				if (slotGroup == null || slotGroup.cfgFileName != group.cfgFileName)
					continue;
				string key = "";
				string set = "";
				ConfigNode old = (legacyNode == null) ? null : legacyNode.GetNode(slot.id);
				if (old != null)
				{
					string oldKey = old.GetValue("key");
					if (!string.IsNullOrEmpty(oldKey))
					{
						key = oldKey.Trim();
						copied++;
					}
					string oldSet = old.GetValue("set");
					if (!string.IsNullOrEmpty(oldSet))
						set = oldSet.Trim();
				}
				ConfigNode slotNode = node.AddNode(slot.id);
				slotNode.AddValue("key", key);
				slotNode.AddValue("set", set);
			}
			return copied;
		}

		private static bool SaveNew(ConfigNode root, string path, string logLine)
		{
			try
			{
				if (!Directory.Exists(pluginDataDir))
				{
					Directory.CreateDirectory(pluginDataDir);
				}
				root.Save(path);
				Debug.Log("[ELK] " + logLine);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] could not create " + path + ": " + e.Message);
				return false;
			}
		}

		/// <summary>Loads a group's cfg (creating the file's root and ELK node in memory if absent) and hands back the ELK node to write into.</summary>
		private static ConfigNode OpenElkNode(string path, out ConfigNode root)
		{
			root = ConfigNode.Load(path);
			if (root == null)
			{
				root = new ConfigNode();
			}
			ConfigNode node = root.GetNode("ELK");
			if (node == null)
			{
				node = root.AddNode("ELK");
			}
			return node;
		}
	}
}
