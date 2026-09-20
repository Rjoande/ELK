// Ported from KRILL's KrillBind.cs (github.com/Rjoande/KRILL, same author,
// MIT) with one deliberate change: serialization. KRILL stores a bind as
// several ConfigNode values (primary = X, repeated modifier = Y); ELK
// stores it as a single human-editable string in the "key" field
// ("LeftAlt+Y", "LeftControl+LeftShift+G" — modifiers then primary,
// '+'-joined literal KeyCode names) so the no-toolbar, hand-edited cfg path
// stays simple, and a captured bind's on-screen Describe() text is directly
// usable as the cfg value too.
//
// Downgraded to C# 5 syntax throughout (no expression-bodied members, no
// null-conditional operator, no Enum.TryParse<T>): this project builds with
// the legacy .NET Framework csc against KSP's own assemblies, same as SBT.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ELK
{
	/// <summary>
	/// One player keybind: a primary KeyCode plus zero or more modifier
	/// KeyCodes that must be held when the primary is freshly pressed.
	/// </summary>
	public class ElkBind
	{
		public KeyCode primary = KeyCode.None;
		public readonly List<KeyCode> modifiers = new List<KeyCode>();

		public bool IsNone
		{
			get { return primary == KeyCode.None; }
		}

		/// <summary>True on the frame the primary is freshly pressed while every required modifier is held.</summary>
		public bool Matches()
		{
			if (IsNone || !Input.GetKeyDown(primary))
				return false;
			for (int i = 0; i < modifiers.Count; i++)
			{
				if (!Input.GetKey(modifiers[i]))
					return false;
			}
			return true;
		}

		/// <summary>True if this and other share the same primary key (the relevant conflict test — see ElkConflicts).</summary>
		public bool SharesPrimaryWith(ElkBind other)
		{
			return other != null && !IsNone && primary == other.primary;
		}

		/// <summary>Human-readable form for the toolbar UI and conflict messages, e.g. "LeftShift+J".</summary>
		public string Describe()
		{
			if (IsNone)
				return "-";
			if (modifiers.Count == 0)
				return primary.ToString();
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < modifiers.Count; i++)
			{
				sb.Append(modifiers[i]).Append('+');
			}
			sb.Append(primary);
			return sb.ToString();
		}

		/// <summary>Cfg "key" value for this bind — same text as Describe(), empty string for IsNone.</summary>
		public string ToKeySpec()
		{
			return IsNone ? "" : Describe();
		}

		/// <summary>
		/// Parses a "key" cfg value: '+'-separated tokens, the last is the
		/// primary, any earlier ones are modifiers, all literal KeyCode
		/// names. Tolerant: returns an IsNone bind (with a logged warning)
		/// instead of throwing on bad input.
		/// </summary>
		public static ElkBind Parse(string spec)
		{
			ElkBind bind = new ElkBind();
			if (string.IsNullOrEmpty(spec))
				return bind;

			string[] tokens = spec.Split('+');
			string keyToken = tokens[tokens.Length - 1].Trim();

			KeyCode parsedPrimary;
			try
			{
				parsedPrimary = (KeyCode)Enum.Parse(typeof(KeyCode), keyToken, true);
			}
			catch (ArgumentException)
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
				try
				{
					parsedMod = (KeyCode)Enum.Parse(typeof(KeyCode), t, true);
				}
				catch (ArgumentException)
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
