// Slot groups: "Global" (ELK.cfg: the master file with the global options
// and the set-switching hotkeys), "Squad" (ELK_Squad.cfg: the stock SAS and
// Brakes slots) and one per supported third-party mod. A group is one tab
// in the toolbar window and one cfg file in PluginData, so presets for
// different mods never step on each other and a preset never carries the
// global options. Third-party groups carry an "installed" probe (is the
// mod's DLL loaded?) that hides the tab and lets ElkConfig create the
// group's cfg on first detection; they may also carry a conflict scanner
// for the mod's own native hotkeys and the default values of their own
// options (written into the generated template).

using System;
using System.Collections.Generic;

namespace ELK
{
	public class ElkGroup
	{
		public readonly string id;
		public readonly string tabLabel;
		public readonly string cfgFileName;

		/// <summary>Null for the two stock groups (always present); otherwise a cheap, cached probe.</summary>
		public readonly Func<bool> installed;

		/// <summary>Optional: describes the mod's own native hotkeys that share a candidate's primary key (see ElkConflicts). Null = the mod has none.</summary>
		public readonly Func<ElkBind, List<string>> conflicts;

		/// <summary>Option key/value pairs written into the group's generated cfg template (null = none). Read back through ElkConfig.GetBool/GetFloat.</summary>
		public readonly KeyValuePair<string, string>[] defaultOptions;

		public ElkGroup(string id, string tabLabel, string cfgFileName, Func<bool> installed,
			Func<ElkBind, List<string>> conflicts, KeyValuePair<string, string>[] defaultOptions)
		{
			this.id = id;
			this.tabLabel = tabLabel;
			this.cfgFileName = cfgFileName;
			this.installed = installed;
			this.conflicts = conflicts;
			this.defaultOptions = defaultOptions;
		}

		/// <summary>Global and Squad: shipped files, no mod behind them.</summary>
		public bool IsStock
		{
			get { return installed == null; }
		}

		public bool IsInstalled
		{
			get { return installed == null || installed(); }
		}
	}

	public static class ElkGroups
	{
		public const string Global = "Global";
		public const string Squad = "Squad";
		public const string AtmosphereAutopilot = "AtmosphereAutopilot";
		public const string MechJeb2 = "MechJeb2";
		public const string MechJeb2Plus = "MechJeb2Plus";
		public const string NavUtilities = "NavUtilities";

		public static readonly List<ElkGroup> All = new List<ElkGroup>
		{
			new ElkGroup(Global, "ELK", "ELK.cfg", null, null, null),
			new ElkGroup(Squad, "Squad", "ELK_Squad.cfg", null, null, null),
			new ElkGroup(AtmosphereAutopilot, "AtmosphereAutopilot", "ELK_AtmosphereAutopilot.cfg",
				ElkAaBridge.IsInstalled, ElkAaKeys.DescribeConflicts,
				new KeyValuePair<string, string>[]
				{
					new KeyValuePair<string, string>(ElkAaBridge.OptAutoEngage, "true"),
					new KeyValuePair<string, string>(ElkAaBridge.OptAutoShow, "false"),
					new KeyValuePair<string, string>(ElkAaBridge.OptVsStep, "1"),
					new KeyValuePair<string, string>(ElkAaBridge.OptAltStep, "50"),
					new KeyValuePair<string, string>(ElkAaBridge.OptHdgStep, "1"),
				}),
			// Two tabs, ONE file (ELK_MechJeb2.cfg): the plain tab carries the
			// options and the conflict scanner, the "+" tab only its slots.
			// The shared "hotkeys" switch of the file therefore governs both.
			new ElkGroup(MechJeb2, "MechJeb2", "ELK_MechJeb2.cfg",
				ElkMjBridge.IsInstalled, ElkMjBridge.DescribeConflicts,
				new KeyValuePair<string, string>[]
				{
					new KeyValuePair<string, string>(ElkMjBridge.OptAutoEngage, "true"),
					new KeyValuePair<string, string>(ElkMjBridge.OptAutoShow, "false"),
					new KeyValuePair<string, string>(ElkMjBridge.OptTransStep, "1"),
					new KeyValuePair<string, string>(ElkMjBridge.OptNavball, "false"),
				}),
			new ElkGroup(MechJeb2Plus, "MechJeb2+", "ELK_MechJeb2.cfg", ElkMjBridge.IsInstalled, null, null),
			new ElkGroup(NavUtilities, "NavUtilities", "ELK_NavUtilities.cfg", ElkNavBridge.IsInstalled, null, null),
		};

		public static ElkGroup Find(string id)
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
