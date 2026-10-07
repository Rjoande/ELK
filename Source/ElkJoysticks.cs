// Joystick buttons bound by device NAME: the K of JoystickKButtonN in a cfg
// is a logical index into ELK.cfg's DEVICES node, resolved against
// Input.GetJoystickNames() at match time (Windows reorders devices per launch).

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ELK
{
	public static class ElkJoysticks
	{
		public const string NodeName = "DEVICES";
		public const int MaxLabelLength = 10;

		// Unity's KeyCode lays joystick buttons out as JoystickButton0..19
		// (any joystick) followed by Joystick1Button0..19 .. Joystick8Button0..19.
		public const int MaxJoysticks = 8;
		public const int ButtonsPerJoystick = 20;

		// Input.GetJoystickNames() is an engine call that allocates a
		// string[]: cached, refreshed a few times per second at most, which
		// still catches a device plugged in or back during a flight.
		private const float RefreshSeconds = 2f;

		public class Device
		{
			public int index;
			public string name;
			public string label;
		}

		private static readonly List<Device> devices = new List<Device>();
		private static string[] names = new string[0];
		private static float namesRefreshedAt = -1f;
		private static readonly HashSet<string> warnedMissing = new HashSet<string>();

		/// <summary>Known devices, in cfg order (read-only use; edit through SetLabel/Forget).</summary>
		public static List<Device> Devices
		{
			get { return devices; }
		}

		public static bool IsSpecificButton(KeyCode code)
		{
			return code >= KeyCode.Joystick1Button0 && code <= KeyCode.Joystick8Button19;
		}

		/// <summary>1-based joystick number of a JoystickKButtonN code.</summary>
		public static int JoystickOf(KeyCode code)
		{
			return ((int)code - (int)KeyCode.Joystick1Button0) / ButtonsPerJoystick + 1;
		}

		public static int ButtonOf(KeyCode code)
		{
			return ((int)code - (int)KeyCode.Joystick1Button0) % ButtonsPerJoystick;
		}

		public static KeyCode Code(int joystick, int button)
		{
			return (KeyCode)((int)KeyCode.Joystick1Button0 + (joystick - 1) * ButtonsPerJoystick + button);
		}

		// ---- DEVICES node ------------------------------------------------

		public static void Load(ConfigNode elkNode)
		{
			devices.Clear();
			warnedMissing.Clear();
			namesRefreshedAt = -1f;
			ConfigNode node = (elkNode == null) ? null : elkNode.GetNode(NodeName);
			if (node != null)
			{
				ConfigNode[] entries = node.GetNodes("DEVICE");
				for (int i = 0; i < entries.Length; i++)
				{
					int index;
					string name = entries[i].GetValue("name");
					if (!int.TryParse(entries[i].GetValue("index"), out index) || index < 1 || index > MaxJoysticks
						|| string.IsNullOrEmpty(name) || ByIndex(index) != null)
					{
						Debug.LogWarning("[ELK] DEVICES: entry " + i + " ignored (index 1-" + MaxJoysticks + " and a name are required, index must be unique)");
						continue;
					}
					Device d = new Device();
					d.index = index;
					d.name = name;
					string label = entries[i].GetValue("label");
					d.label = IsValidLabel(label) && ByLabel(label) == null ? label : DefaultLabel(name, index);
					devices.Add(d);
				}
			}
			if (devices.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < devices.Count; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append("Joystick").Append(devices[i].index).Append('=').Append(devices[i].label)
						.Append(" (").Append(devices[i].name).Append(": ").Append(Status(devices[i])).Append(')');
				}
				Debug.Log("[ELK] joysticks known by name: " + sb);
			}
		}

		public static void Save(ConfigNode elkNode)
		{
			elkNode.RemoveNodes(NodeName);
			if (devices.Count == 0)
				return;
			ConfigNode node = elkNode.AddNode(NodeName);
			for (int i = 0; i < devices.Count; i++)
			{
				ConfigNode entry = node.AddNode("DEVICE");
				entry.AddValue("index", devices[i].index.ToString());
				entry.AddValue("name", devices[i].name);
				entry.AddValue("label", devices[i].label);
			}
		}

		public static Device ByIndex(int index)
		{
			for (int i = 0; i < devices.Count; i++)
			{
				if (devices[i].index == index)
					return devices[i];
			}
			return null;
		}

		public static Device ByName(string name)
		{
			for (int i = 0; i < devices.Count; i++)
			{
				if (devices[i].name == name)
					return devices[i];
			}
			return null;
		}

		public static Device ByLabel(string label)
		{
			for (int i = 0; i < devices.Count; i++)
			{
				if (string.Equals(devices[i].label, label, StringComparison.OrdinalIgnoreCase))
					return devices[i];
			}
			return null;
		}

		/// <summary>Short, unique, ASCII, without blanks, '+' or '.' (the bind syntax's own separators).</summary>
		public static bool IsValidLabel(string label)
		{
			if (string.IsNullOrEmpty(label) || label.Length > MaxLabelLength)
				return false;
			for (int i = 0; i < label.Length; i++)
			{
				char c = label[i];
				if (c <= ' ' || c > '~' || c == '+' || c == '.')
					return false;
			}
			return true;
		}

		/// <summary>First word of the device name, kept to the allowed characters, made unique; "JoyN" if nothing usable is left.</summary>
		private static string DefaultLabel(string name, int index)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < name.Length && sb.Length < MaxLabelLength; i++)
			{
				char c = name[i];
				if (c <= ' ')
				{
					if (sb.Length > 0) break;
					continue;
				}
				if (c > '~' || c == '+' || c == '.')
					continue;
				sb.Append(c);
			}
			string label = sb.Length > 0 ? sb.ToString() : "Joy" + index;
			if (ByLabel(label) == null)
				return label;
			string stem = label.Length > MaxLabelLength - 1 ? label.Substring(0, MaxLabelLength - 1) : label;
			for (int n = 2; n < 100; n++)
			{
				string candidate = stem + n;
				if (ByLabel(candidate) == null)
					return candidate;
			}
			return "Joy" + index;
		}

		/// <summary>Renames a device's label and persists ELK.cfg. False (nothing written) if the label is invalid or taken by another device.</summary>
		public static bool SetLabel(int index, string label)
		{
			Device d = ByIndex(index);
			if (d == null || !IsValidLabel(label))
				return false;
			Device other = ByLabel(label);
			if (other != null && other != d)
				return false;
			if (d.label == label)
				return true;
			d.label = label;
			ElkConfig.SaveDevices();
			return true;
		}

		/// <summary>Drops an entry: binds using its index fall back to that index taken literally.</summary>
		public static void Forget(int index)
		{
			Device d = ByIndex(index);
			if (d == null)
				return;
			devices.Remove(d);
			ElkConfig.SaveDevices();
			Debug.Log("[ELK] joystick " + index + " (" + d.name + ") forgotten, Joystick" + index + " binds are literal again");
		}

		// ---- current devices ---------------------------------------------

		private static string[] Names()
		{
			float now = Time.realtimeSinceStartup;
			if (namesRefreshedAt < 0f || now - namesRefreshedAt > RefreshSeconds)
			{
				namesRefreshedAt = now;
				try
				{
					string[] fresh = Input.GetJoystickNames();
					names = (fresh == null) ? new string[0] : fresh;
				}
				catch (Exception e)
				{
					Debug.LogWarning("[ELK] Input.GetJoystickNames failed: " + e.Message);
				}
			}
			return names;
		}

		/// <summary>Name Unity reports for joystick number k (1-based) right now, "" if none.</summary>
		public static string NameOf(int joystick)
		{
			string[] n = Names();
			int i = joystick - 1;
			if (i < 0 || i >= n.Length || n[i] == null)
				return "";
			// Stock's InputDevices.TrimDeviceName does the same Trim(): a
			// name must compare equal between a capture and a later launch.
			return n[i].Trim();
		}

		/// <summary>Joystick number (1-based) the named device occupies right now, 0 if absent. Two identical devices: the lowest number wins, as in stock.</summary>
		public static int CurrentIndexOf(string name)
		{
			string[] n = Names();
			for (int i = 0; i < n.Length && i < MaxJoysticks; i++)
			{
				if (n[i] != null && n[i].Trim() == name)
					return i + 1;
			}
			return 0;
		}

		public static string Status(Device d)
		{
			int now = CurrentIndexOf(d.name);
			return now == 0 ? "not connected" : "now Joystick" + now;
		}

		// ---- translation -------------------------------------------------

		/// <summary>KeyCode to poll for a bind: a JoystickKButtonN with a DEVICES entry maps
		/// to that device's current button, or None while disconnected.
		/// Other codes are returned as is.</summary>
		public static KeyCode Physical(KeyCode logical)
		{
			if (!IsSpecificButton(logical))
				return logical;
			Device d = ByIndex(JoystickOf(logical));
			if (d == null)
				return logical;
			int now = CurrentIndexOf(d.name);
			if (now == 0)
			{
				if (warnedMissing.Add(d.name))
					Debug.LogWarning("[ELK] joystick " + d.name + " (" + d.label + ") is not connected: its hotkeys are inert");
				return KeyCode.None;
			}
			warnedMissing.Remove(d.name);
			return Code(now, ButtonOf(logical));
		}

		/// <summary>Capture-time counterpart: the code to STORE for a physical button. The
		/// device is looked up or registered; a nameless device stays literal with
		/// a warning.</summary>
		public static KeyCode Logical(KeyCode physical)
		{
			if (!IsSpecificButton(physical))
				return physical;
			int k = JoystickOf(physical);
			string name = NameOf(k);
			if (name.Length == 0)
			{
				Debug.LogWarning("[ELK] joystick " + k + " reports no name: " + physical + " bound by index only");
				return physical;
			}
			Device d = ByName(name);
			if (d == null)
			{
				int index = ByIndex(k) == null ? k : 0;
				for (int i = 1; index == 0 && i <= MaxJoysticks; i++)
				{
					if (ByIndex(i) == null)
						index = i;
				}
				if (index == 0)
				{
					Debug.LogWarning("[ELK] all " + MaxJoysticks + " logical joystick indexes are taken: " + physical + " bound by index only");
					return physical;
				}
				d = new Device();
				d.index = index;
				d.name = name;
				d.label = DefaultLabel(name, index);
				devices.Add(d);
				ElkConfig.SaveDevices();
				Debug.Log("[ELK] joystick registered: Joystick" + index + " = " + name + " (" + d.label + "), now at " + k);
			}
			return Code(d.index, ButtonOf(physical));
		}

		/// <summary>Side-effect-free Logical(): the logical code if the device at that physical index is already known, the physical code otherwise. For comparing a third-party mod's own (physical) key against ELK binds.</summary>
		public static KeyCode LogicalIfKnown(KeyCode physical)
		{
			if (!IsSpecificButton(physical))
				return physical;
			Device d = ByName(NameOf(JoystickOf(physical)));
			return d == null ? physical : Code(d.index, ButtonOf(physical));
		}

		/// <summary>Window text for one bind token: "VKBsim.B10" for a named device, the KeyCode name otherwise.</summary>
		public static string Describe(KeyCode logical)
		{
			if (IsSpecificButton(logical))
			{
				Device d = ByIndex(JoystickOf(logical));
				if (d != null)
					return d.label + ".B" + ButtonOf(logical);
			}
			return logical.ToString();
		}

		/// <summary>Parses the "label.Bn" form of a bind token (case-insensitive label). False if the token is not of that form or the label is unknown.</summary>
		public static bool TryParse(string token, out KeyCode code)
		{
			code = KeyCode.None;
			int dot = token.IndexOf(".B", StringComparison.OrdinalIgnoreCase);
			if (dot <= 0)
				return false;
			int button;
			if (!int.TryParse(token.Substring(dot + 2), out button) || button < 0 || button >= ButtonsPerJoystick)
				return false;
			Device d = ByLabel(token.Substring(0, dot));
			if (d == null)
				return false;
			code = Code(d.index, button);
			return true;
		}
	}
}