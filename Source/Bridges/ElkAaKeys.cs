// AtmosphereAutopilot's OWN hotkeys, reached by reflection: enumeration,
// reading, writing, and the Import / Export / Restore actions of the
// AtmosphereAutopilot tab. Verified against the AA 1.6.1 source:
//
// - Every AA hotkey is a static KeyCode field of a module class, marked
//   [AutoHotkeyAttr("display name")] (public attribute type, public
//   "hotkey_name" field) and [GlobalSerializable("key")] (subclass of
//   AutoSerializableAttr, public "data_name" field). AA's own "Hotkeys
//   manager" window enumerates them the same way (Static | Public |
//   NonPublic), so whatever an AA build declares - 9 keys upstream, more in
//   a fork - is found without a hard-coded list.
// - They persist in GameData/AtmosphereAutopilot/Global_settings.txt, one
//   node per module (node = module name with spaces replaced by '_'). AA
//   reads that file on every vessel load (overwriting the static fields)
//   and rewrites it only when leaving flight. Hence the file, not the
//   static field, is the source of truth (before the first flight of a
//   session the fields still hold the compiled defaults), and every write
//   from here goes to BOTH the file and the field.
// - KeyCode values are stored as Enum.Parse-able literals ("P", "None").
//
// Nothing here runs unless the player clicks a button on the AA tab or
// captures a key (conflict scan): ELK never touches AA's file on its own.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public class ElkAaHotkey
	{
		public FieldInfo field;
		public string displayName;   // AutoHotkeyAttr.hotkey_name
		public string key;           // GlobalSerializable.data_name (equals the field name in every AA build seen)
		public string nodeName;      // Global_settings.txt node the key lives in, or null if unknown
	}

	public static class ElkAaKeys
	{
		private const string ModLabel = "AtmosphereAutopilot";
		private const string HotkeyAttrTypeName = "AtmosphereAutopilot.AutoHotkeyAttr";
		private const string SerialAttrTypeName = "AtmosphereAutopilot.GlobalSerializable";
		private const string BackupNodeName = "ELK_AA_KEYS_BACKUP";

		// ELK slot id -> AA hotkey key (== field name). Slots with no AA
		// counterpart (GUI toggle, select FBW/Cruise, ALT/HDG sync) are
		// simply absent here.
		private static readonly string[,] SlotToKey =
		{
			{ "AA_MASTER_TOGGLE", "master_switch_key" },
			{ "AA_FD_TOGGLE", "fd_switch_key" },
			{ "AA_CF_LEVEL", "level_flight_key" },
			{ "AA_CF_HEADING", "course_hold_key" },
			{ "AA_CF_WAYPOINT", "waypoint_key" },
			{ "AA_CF_VS_TOGGLE", "vspeed_select_key" },
			{ "AA_CF_ALT_TOGGLE", "altitude_select_key" },
			{ "AA_CF_VERTICAL_TOGGLE", "vertical_control_key" },
			{ "AA_CF_SETPOINT_TYPE_TOGGLE", "toggle_vertical_setpoint_type_key" },
			{ "AA_CF_KEYS_MODE_TOGGLE", "switch_key_mode" },
			{ "AA_CF_VS_UP", "vspeed_up_key" },
			{ "AA_CF_VS_DOWN", "vspeed_down_key" },
			{ "AA_CF_ALT_UP", "altitude_up_key" },
			{ "AA_CF_ALT_DOWN", "altitude_down_key" },
			{ "AA_CF_HDG_UP", "course_up_key" },
			{ "AA_CF_HDG_DOWN", "course_down_key" },
			{ "AA_FBW_MODERATION", "moderation_keycode" },
			{ "AA_FBW_ROCKET", "rocket_mode_keycode" },
			{ "AA_FBW_COORD_TURN", "coord_turn_keycode" },
			{ "AA_SPEED_CONTROL", "spd_control_toggle_key" },
			{ "AA_THRUST_BALANCING", "balancing_toggle_key" },
		};

		// Declaring class -> Global_settings.txt node, used only when the file
		// has no node holding the key yet (module names from the AA source).
		private static readonly string[,] ClassToNode =
		{
			{ "TopModuleManager", "Autopilot_module_manager" },
			{ "CruiseController", "Cruise_Flight_controller" },
			{ "StandardFlyByWire", "Standard_Fly-By-Wire" },
			{ "ProgradeThrustController", "Prograde_thrust_controller" },
			{ "FlightModel", "Flight_model" },
		};

		// AA's compiled defaults, used by Restore when no backup exists.
		private static readonly string[,] FactoryDefaults =
		{
			{ "master_switch_key", "P" },
			{ "moderation_keycode", "O" },
			{ "switch_key_mode", "RightAlt" },
		};

		private static bool enumerated;
		private static readonly List<ElkAaHotkey> hotkeys = new List<ElkAaHotkey>();

		public static string SettingsPath
		{
			get { return KSPUtil.ApplicationRootPath + "GameData/AtmosphereAutopilot/Global_settings.txt"; }
		}

		public static string BackupPath
		{
			get { return KSPUtil.ApplicationRootPath + "GameData/AtmosphereAutopilot/ELK_AtmosphereAutopilot_default-backup.txt"; }
		}

		public static bool BackupExists
		{
			get { return File.Exists(BackupPath); }
		}

		/// <summary>All hotkey fields declared by the installed AA build (empty if AA is absent or unreadable).</summary>
		public static List<ElkAaHotkey> Hotkeys
		{
			get
			{
				Enumerate();
				return hotkeys;
			}
		}

		private static void Enumerate()
		{
			if (enumerated)
				return;
			enumerated = true;
			Assembly assembly = ElkAaBridge.Assembly;
			if (assembly == null)
				return;

			Type hotkeyAttr = ElkReflection.FindType(assembly, HotkeyAttrTypeName, ModLabel);
			Type serialAttr = ElkReflection.FindType(assembly, SerialAttrTypeName, ModLabel);
			if (hotkeyAttr == null)
				return;
			FieldInfo nameField = ElkReflection.FindField(hotkeyAttr, "hotkey_name", BindingFlags.Public | BindingFlags.Instance, ModLabel);
			FieldInfo dataNameField = (serialAttr == null) ? null
				: ElkReflection.FindField(serialAttr, "data_name", BindingFlags.Public | BindingFlags.Instance, ModLabel);

			Type[] types;
			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + ModLabel + ": cannot list types: " + e.Message);
				return;
			}

			for (int t = 0; t < types.Length; t++)
			{
				Type type = types[t];
				if (type == null)
					continue;
				FieldInfo[] fields;
				try
				{
					fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				}
				catch (Exception)
				{
					continue;
				}
				for (int f = 0; f < fields.Length; f++)
				{
					FieldInfo field = fields[f];
					if (field.FieldType != typeof(KeyCode))
						continue;
					object[] attrs = field.GetCustomAttributes(hotkeyAttr, true);
					if (attrs == null || attrs.Length == 0)
						continue;

					ElkAaHotkey hk = new ElkAaHotkey();
					hk.field = field;
					hk.displayName = (nameField != null) ? (nameField.GetValue(attrs[0]) as string) : null;
					if (string.IsNullOrEmpty(hk.displayName))
						hk.displayName = field.Name;
					hk.key = field.Name;
					if (serialAttr != null && dataNameField != null)
					{
						object[] sattrs = field.GetCustomAttributes(serialAttr, true);
						if (sattrs != null && sattrs.Length > 0)
						{
							string dataName = dataNameField.GetValue(sattrs[0]) as string;
							if (!string.IsNullOrEmpty(dataName))
								hk.key = dataName;
						}
					}
					hk.nodeName = NodeFor(type.Name);
					hotkeys.Add(hk);
				}
			}
			Debug.Log("[ELK] " + ModLabel + ": " + hotkeys.Count + " native hotkeys found");
		}

		private static string NodeFor(string className)
		{
			for (int i = 0; i < ClassToNode.GetLength(0); i++)
			{
				if (ClassToNode[i, 0] == className)
					return ClassToNode[i, 1];
			}
			return null;
		}

		public static ElkAaHotkey FindByKey(string key)
		{
			List<ElkAaHotkey> all = Hotkeys;
			for (int i = 0; i < all.Count; i++)
			{
				if (all[i].key == key)
					return all[i];
			}
			return null;
		}

		/// <summary>The AA key ELK slot slotId mirrors, or null if the slot has no AA counterpart.</summary>
		public static string KeyForSlot(string slotId)
		{
			for (int i = 0; i < SlotToKey.GetLength(0); i++)
			{
				if (SlotToKey[i, 0] == slotId)
					return SlotToKey[i, 1];
			}
			return null;
		}

		/// <summary>Current value of an AA hotkey: the file's, or the static field's when the file has none.</summary>
		public static KeyCode Read(ElkAaHotkey hk)
		{
			ConfigNode root = ConfigNode.Load(SettingsPath);
			ConfigNode node = (root == null) ? null : NodeHolding(root, hk.key);
			if (node != null)
			{
				KeyCode parsed;
				if (TryParseKey(node.GetValue(hk.key), out parsed))
					return parsed;
			}
			try
			{
				object raw = hk.field.GetValue(null);
				return (raw is KeyCode) ? (KeyCode)raw : KeyCode.None;
			}
			catch (Exception)
			{
				return KeyCode.None;
			}
		}

		/// <summary>Writes an AA hotkey to the static field AND to Global_settings.txt. True if the file was written.</summary>
		public static bool Write(ElkAaHotkey hk, KeyCode value)
		{
			try
			{
				hk.field.SetValue(null, value);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + ModLabel + ": cannot set " + hk.key + ": " + e.Message);
			}

			ConfigNode root = ConfigNode.Load(SettingsPath);
			if (root == null)
				root = new ConfigNode();
			ConfigNode node = NodeHolding(root, hk.key);
			if (node == null)
			{
				if (hk.nodeName == null)
				{
					Debug.LogWarning("[ELK] " + ModLabel + ": no node known for " + hk.key + ", file not updated");
					return false;
				}
				node = root.GetNode(hk.nodeName);
				if (node == null)
					node = root.AddNode(hk.nodeName);
			}
			if (node.HasValue(hk.key))
				node.SetValue(hk.key, value.ToString());
			else
				node.AddValue(hk.key, value.ToString());
			try
			{
				root.Save(SettingsPath);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + ModLabel + ": cannot save " + SettingsPath + ": " + e.Message);
				return false;
			}
		}

		private static ConfigNode NodeHolding(ConfigNode root, string key)
		{
			for (int i = 0; i < root.nodes.Count; i++)
			{
				if (root.nodes[i].HasValue(key))
					return root.nodes[i];
			}
			return null;
		}

		private static bool TryParseKey(string text, out KeyCode value)
		{
			value = KeyCode.None;
			if (string.IsNullOrEmpty(text))
				return false;
			try
			{
				value = (KeyCode)Enum.Parse(typeof(KeyCode), text.Trim(), true);
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		/// <summary>Snapshot of every AA key as it is now, written once and never overwritten. True if a backup exists afterwards.</summary>
		public static bool EnsureBackup()
		{
			if (BackupExists)
				return true;
			ConfigNode root = new ConfigNode();
			ConfigNode node = root.AddNode(BackupNodeName);
			List<ElkAaHotkey> all = Hotkeys;
			for (int i = 0; i < all.Count; i++)
			{
				node.AddValue(all[i].key, Read(all[i]).ToString());
			}
			try
			{
				root.Save(BackupPath);
				Debug.Log("[ELK] " + ModLabel + ": native keys backed up to " + BackupPath);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + ModLabel + ": cannot write backup " + BackupPath + ": " + e.Message);
				return false;
			}
		}

		/// <summary>Copies every AA key that has an ELK slot into that slot (primary only), then clears it in AA so it cannot fire twice.</summary>
		public static List<string> Import()
		{
			List<string> lines = new List<string>();
			if (!EnsureBackup())
				lines.Add("Backup of AA keys failed, nothing imported (see KSP.log).");
			if (lines.Count > 0)
				return lines;

			int imported = 0;
			for (int i = 0; i < SlotToKey.GetLength(0); i++)
			{
				string slotId = SlotToKey[i, 0];
				ElkAaHotkey hk = FindByKey(SlotToKey[i, 1]);
				if (hk == null)
					continue;
				KeyCode kc = Read(hk);
				if (kc == KeyCode.None)
					continue;
				ElkBind bind = new ElkBind();
				bind.primary = kc;
				ElkConfig.SetBind(slotId, bind);
				Write(hk, KeyCode.None);
				imported++;
				lines.Add("  " + hk.displayName + " (" + kc + ") -> " + slotId);
				List<string> conflicts = ElkConflicts.Describe(bind, slotId, ElkConfig.Binds);
				for (int c = 0; c < conflicts.Count; c++)
					lines.Add("    [!] also bound to: " + conflicts[c]);
			}
			lines.Insert(0, imported == 0 ? "No AA key to import." : "Imported " + imported + " key(s) from AA and cleared them there:");
			return lines;
		}

		/// <summary>Copies every bound ELK slot that mirrors an AA key back into AA (modifier combos cannot, they are listed), then clears the ELK slot.</summary>
		public static List<string> Export()
		{
			List<string> lines = new List<string>();
			if (!EnsureBackup())
			{
				lines.Add("Backup of AA keys failed, nothing exported (see KSP.log).");
				return lines;
			}

			int exported = 0;
			List<string> skipped = new List<string>();
			for (int i = 0; i < SlotToKey.GetLength(0); i++)
			{
				string slotId = SlotToKey[i, 0];
				ElkBind bind;
				if (!ElkConfig.Binds.TryGetValue(slotId, out bind) || bind == null || bind.IsNone)
					continue;
				ElkAaHotkey hk = FindByKey(SlotToKey[i, 1]);
				if (hk == null)
				{
					skipped.Add("  " + slotId + " (" + bind.Describe() + "): this AA build has no such key");
					continue;
				}
				if (bind.modifiers.Count > 0)
				{
					skipped.Add("  " + slotId + " (" + bind.Describe() + "): AA keys have no modifiers");
					continue;
				}
				Write(hk, bind.primary);
				ElkConfig.SetBind(slotId, new ElkBind());
				exported++;
				lines.Add("  " + slotId + " (" + bind.primary + ") -> AA " + hk.displayName);
			}
			lines.Insert(0, exported == 0 ? "No ELK key to export." : "Exported " + exported + " key(s) to AA and cleared them here:");
			if (skipped.Count > 0)
			{
				lines.Add("Kept in ELK:");
				lines.AddRange(skipped);
			}
			return lines;
		}

		/// <summary>Writes the backup (or AA's factory defaults if there is none) back into AA. ELK slots are left alone.</summary>
		public static List<string> Restore()
		{
			List<string> lines = new List<string>();
			ConfigNode backup = BackupExists ? ConfigNode.Load(BackupPath) : null;
			ConfigNode node = (backup == null) ? null : backup.GetNode(BackupNodeName);
			bool fromBackup = node != null;

			int restored = 0;
			List<ElkAaHotkey> all = Hotkeys;
			for (int i = 0; i < all.Count; i++)
			{
				ElkAaHotkey hk = all[i];
				KeyCode kc;
				if (fromBackup)
				{
					if (!TryParseKey(node.GetValue(hk.key), out kc))
						continue;
				}
				else
				{
					kc = FactoryDefault(hk.key);
				}
				if (Read(hk) == kc)
					continue;
				Write(hk, kc);
				restored++;
				lines.Add("  " + hk.displayName + " = " + kc);
			}
			lines.Insert(0, (fromBackup ? "Restored from backup" : "No backup found, restored AA factory defaults")
				+ (restored == 0 ? ": nothing to change." : ", " + restored + " key(s) changed:"));

			// A restored AA key that an ELK slot also uses will fire twice.
			ElkBind probe = new ElkBind();
			for (int i = 0; i < all.Count; i++)
			{
				probe.primary = Read(all[i]);
				if (probe.IsNone)
					continue;
				List<string> hits = ElkConflicts.Describe(probe, null, ElkConfig.Binds);
				for (int c = 0; c < hits.Count; c++)
				{
					if (hits[c].StartsWith("ELK "))
						lines.Add("  [!] AA '" + all[i].displayName + "' (" + probe.primary + ") also bound to: " + hits[c]);
				}
			}
			return lines;
		}

		private static KeyCode FactoryDefault(string key)
		{
			for (int i = 0; i < FactoryDefaults.GetLength(0); i++)
			{
				if (FactoryDefaults[i, 0] == key)
					return (KeyCode)Enum.Parse(typeof(KeyCode), FactoryDefaults[i, 1]);
			}
			return KeyCode.None;
		}

		/// <summary>Conflict scanner for ElkConflicts: every AA native key equal to the candidate's primary.</summary>
		public static List<string> DescribeConflicts(ElkBind candidate)
		{
			List<string> hits = new List<string>();
			if (candidate == null || candidate.IsNone)
				return hits;
			List<ElkAaHotkey> all = Hotkeys;
			for (int i = 0; i < all.Count; i++)
			{
				if (Read(all[i]) == candidate.primary)
					hits.Add("AtmosphereAutopilot '" + all[i].displayName + "' (" + candidate.primary + ")");
			}
			return hits;
		}
	}
}
