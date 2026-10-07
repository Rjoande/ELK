// Key bind (primary + modifiers), ported from KRILL's KrillBind (MIT).
// Stored as one '+'-joined string ("LeftAlt+Y"); joystick buttons go through
// ElkJoysticks. C# 5 syntax only (legacy csc).

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ELK
{
	/// <summary>One keybind: a primary KeyCode plus modifiers held when it is pressed.</summary>
	public class ElkBind
	{
		public KeyCode primary = KeyCode.None;
		public readonly List<KeyCode> modifiers = new List<KeyCode>();

		public bool IsNone
		{
			get { return primary == KeyCode.None; }
		}

		/// <summary>True on the frame the primary is freshly pressed while every required modifier is held. Joystick codes are polled where their device is right now (ElkJoysticks.Physical); a device not connected never matches.</summary>
		public bool Matches()
		{
			if (IsNone)
				return false;
			KeyCode key = ElkJoysticks.Physical(primary);
			if (key == KeyCode.None || !Input.GetKeyDown(key))
				return false;
			for (int i = 0; i < modifiers.Count; i++)
			{
				KeyCode mod = ElkJoysticks.Physical(modifiers[i]);
				if (mod == KeyCode.None || !Input.GetKey(mod))
					return false;
			}
			return true;
		}

		/// <summary>True if this and other share the same primary key, whatever the modifiers.</summary>
		public bool SharesPrimaryWith(ElkBind other)
		{
			return other != null && !IsNone && primary == other.primary;
		}

		/// <summary>True if one press can fire both binds: same primary and nested modifier
		/// sets (Matches() ignores extra held keys); disjoint modifiers don't
		/// conflict.</summary>
		public bool ConflictsWith(ElkBind other)
		{
			if (!SharesPrimaryWith(other))
				return false;
			return ModifiersContainedIn(other) || other.ModifiersContainedIn(this);
		}

		private bool ModifiersContainedIn(ElkBind other)
		{
			for (int i = 0; i < modifiers.Count; i++)
			{
				if (!other.modifiers.Contains(modifiers[i]))
					return false;
			}
			return true;
		}

		/// <summary>Human-readable form for the toolbar UI, the log and conflict messages, e.g. "LeftShift+J" or "VKBsim.B10" for a joystick known by name.</summary>
		public string Describe()
		{
			return IsNone ? "-" : Join(true);
		}

		/// <summary>Cfg "key" value for this bind: canonical KeyCode names only, empty string for IsNone.</summary>
		public string ToKeySpec()
		{
			return IsNone ? "" : Join(false);
		}

		private string Join(bool labels)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < modifiers.Count; i++)
			{
				sb.Append(labels ? ElkJoysticks.Describe(modifiers[i]) : modifiers[i].ToString()).Append('+');
			}
			sb.Append(labels ? ElkJoysticks.Describe(primary) : primary.ToString());
			return sb.ToString();
		}

		/// <summary>One token of a key spec: a literal KeyCode name, or the "label.Bn" form of a joystick known by name.</summary>
		private static bool ParseToken(string token, out KeyCode code)
		{
			try
			{
				code = (KeyCode)Enum.Parse(typeof(KeyCode), token, true);
				return true;
			}
			catch (ArgumentException)
			{
				return ElkJoysticks.TryParse(token, out code);
			}
		}

		/// <summary>Parses a "key" cfg value: '+'-separated literal KeyCode names, last =
		/// primary. Tolerant: bad input gives an IsNone bind and a warning.</summary>
		public static ElkBind Parse(string spec)
		{
			ElkBind bind = new ElkBind();
			if (string.IsNullOrEmpty(spec))
				return bind;

			string[] tokens = spec.Split('+');
			string keyToken = tokens[tokens.Length - 1].Trim();

			KeyCode parsedPrimary;
			if (!ParseToken(keyToken, out parsedPrimary))
			{
				Debug.LogWarning("[ELK] unparsable key spec: '" + spec + "'");
				return bind;
			}
			if (parsedPrimary == KeyCode.None)
				return bind;

			List<KeyCode> mods = new List<KeyCode>();
			for (int i = 0; i < tokens.Length - 1; i++)
			{
				string t = tokens[i].Trim();
				KeyCode parsedMod;
				if (!ParseToken(t, out parsedMod))
				{
					Debug.LogWarning("[ELK] unparsable modifier '" + t + "' in key spec: '" + spec + "'");
					return new ElkBind();
				}
				if (parsedMod != KeyCode.None)
				{
					mods.Add(parsedMod);
				}
			}

			bind.primary = parsedPrimary;
			bind.modifiers.AddRange(mods);
			return bind;
		}
	}
}
