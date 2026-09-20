// Owns PluginData/ELK.cfg: loading, the in-memory slot->bind map, and
// writing a single slot's bind back to disk (used by the toolbar's capture
// flow). Static and scene-independent on purpose: ElkAddon (Flight scene)
// and ElkToolbarApp/ElkWindow (Space Center scene) share this one source of
// truth without depending on which scene's MonoBehaviour.Awake() happened
// to run first this session — ConfigPath is computed from the assembly's
// own location, never from a KSPAddon lifecycle callback.
//
// Known trade-off: SetBind() round-trips the file through ConfigNode's own
// writer, which does not preserve the shipped file's "//" comments. The
// first toolbar-driven capture on a fresh install replaces the annotated
// default cfg with an uncommented one — accepted (same limitation applies
// to every ConfigNode-based KSP mod, stock's own settings.cfg included);
// the toolbar window is the in-game source of explanation at that point.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkConfig
	{
		public const string Version = "1.0.0";

		// ELK.dll sits in GameData/ELK/Plugins/, and PluginData is a SIBLING
		// of Plugins/ (both directly under GameData/ELK/), not nested inside
		// it - so the mod root is one level above the assembly's own
		// directory. Getting this wrong silently produces a "config not
		// found" path like ".../ELK/Plugins/PluginData/ELK.cfg" that never
		// matches the actual shipped file - confirmed via KSP.log after a
		// user report that no hotkey fired at all.
		private static readonly string configPath = Path.Combine(
			Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)),
			Path.Combine("PluginData", "ELK.cfg"));

		public static bool Enabled { get; private set; }
		public static bool ToolbarEnabled { get; private set; }

		private static readonly Dictionary<string, ElkBind> binds = new Dictionary<string, ElkBind>();

		public static Dictionary<string, ElkBind> Binds
		{
			get { return binds; }
		}

		public static void Load()
		{
			Enabled = true;
			ToolbarEnabled = true;
			binds.Clear();
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				binds[ElkSlots.All[i].id] = new ElkBind();
			}

			ConfigNode root = ConfigNode.Load(configPath);
			ConfigNode node = (root == null) ? null : root.GetNode("ELK");
			if (node == null)
			{
				Debug.LogWarning("[ELK] config not found or missing ELK node (" + configPath + "), all hotkeys unbound");
				return;
			}

			bool b;
			if (bool.TryParse(node.GetValue("enabled"), out b)) Enabled = b;
			if (bool.TryParse(node.GetValue("toolbar"), out b)) ToolbarEnabled = b;

			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				ElkSlot slot = ElkSlots.All[i];
				ConfigNode slotNode = node.GetNode(slot.id);
				if (slotNode == null)
					continue;
				binds[slot.id] = ElkBind.Parse(slotNode.GetValue("key"));
			}

			Debug.Log("[ELK] v" + Version + ": enabled=" + Enabled + " toolbar=" + ToolbarEnabled
				+ ", " + CountBound() + "/" + ElkSlots.All.Count + " slots bound");
		}

		/// <summary>Sets a slot's bind in memory and immediately persists the whole cfg to disk (toolbar capture/clear commit — no restart needed).</summary>
		public static void SetBind(string slotId, ElkBind bind)
		{
			binds[slotId] = bind;

			ConfigNode root = ConfigNode.Load(configPath);
			if (root == null)
			{
				root = new ConfigNode();
			}
			ConfigNode node = root.GetNode("ELK");
			if (node == null)
			{
				node = root.AddNode("ELK");
			}
			ConfigNode slotNode = node.GetNode(slotId);
			if (slotNode == null)
			{
				slotNode = node.AddNode(slotId);
			}
			if (slotNode.HasValue("key"))
			{
				slotNode.SetValue("key", bind.ToKeySpec());
			}
			else
			{
				slotNode.AddValue("key", bind.ToKeySpec());
			}
			root.Save(configPath);
		}

		private static int CountBound()
		{
			int n = 0;
			foreach (KeyValuePair<string, ElkBind> kv in binds)
			{
				if (!kv.Value.IsNone)
					n++;
			}
			return n;
		}
	}
}
