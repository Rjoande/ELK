// Minimal IMGUI window ("UI minimale" — deliberately much smaller than
// KRILL's 3-column layout): a row of tabs (one per slot group whose mod is
// installed), then one row per slot of the active tab (label, key set
// button while sets are in use, map view toggle, current bind, Capture,
// Clear), any live conflict warning shown inline underneath. ELK only ever binds whole
// vessel-level actions, so there's no per-part target to pick the way
// KRILL needs.
//
// All user-facing strings are plain ASCII on purpose: Unity's default
// IMGUI skin font isn't guaranteed to carry glyphs like an em dash or a
// warning triangle, and a missing glyph renders as a visible tofu box.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	public class ElkWindow : MonoBehaviour
	{
		public static Action OnClosed;

		private static ElkWindow instance;

		// Position only: the size here is just the first frame's guess.
		// GUILayout.Window resizes a window to fit its contents as long as
		// nothing inside it stretches, and now that the rows are laid out
		// directly (no ScrollView, which stretched to fill whatever height
		// the Rect had and scrolled the rest away) that is the case - so the
		// window grows and shrinks by itself with the empty-state line, the
		// capture line and any conflict warnings, and can never show a
		// scrollbar or leave dead space under the last row. Tabs keep it
		// that way: only one group's rows exist at a time.
		private Rect windowRect = new Rect(200, 100, 660, 400);
		private string activeGroupId = ElkGroups.Global;
		private string capturingSlotId;
		private string lastCaptureSlotId;
		private readonly List<string> lastConflicts = new List<string>();

		private const float LabelWidth = 170f;
		private const float SetButtonWidth = 72f;
		private const float MapButtonWidth = 46f;
		private const float BindWidth = 170f;

		public static void Open()
		{
			if (instance != null)
				return;
			GameObject go = new GameObject("ElkWindow");
			instance = go.AddComponent<ElkWindow>();
		}

		public static void Close()
		{
			if (instance == null)
				return;
			Destroy(instance.gameObject);
		}

		public void OnDestroy()
		{
			if (instance == this)
			{
				instance = null;
			}
			ElkCapture.ForceCancel();
			if (OnClosed != null)
			{
				OnClosed();
			}
		}

		public void OnGUI()
		{
			windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "ELK - Extended Logic Keys");
		}

		private void DrawWindow(int id)
		{
			// Width pinned to what a row actually needs (170 + 72 + 46 + 170
			// + 85 + 65 plus IMGUI's own spacing), so the window keeps one
			// width across every state instead of twitching wider whenever
			// a longer line of text appears above the rows.
			GUILayout.BeginVertical(GUILayout.Width(635));

			DrawTabs();
			// The ELK tab holds only the two set-switching hotkeys: no
			// per-tab hotkeys switch there (that is what "enabled" in
			// ELK.cfg is for) and nothing to assign to a set.
			if (activeGroupId != ElkGroups.Global)
			{
				DrawGroupHotkeysToggle();
			}

			if (!AnyBoundIn(activeGroupId))
			{
				GUILayout.Label("No hotkeys configured yet. Click Capture or install a preset.");
			}

			if (ElkCapture.IsCapturing)
			{
				GUILayout.Label("Capturing: press a key or combo to bind it - Delete clears it, Esc cancels.");
			}

			if (activeGroupId == ElkGroups.Global)
			{
				DrawSetsOptions();
				DrawDevices();
			}
			else if (activeGroupId == ElkGroups.Squad)
			{
				DrawSasAutoEngageToggle();
			}
			else if (activeGroupId == ElkGroups.AtmosphereAutopilot)
			{
				DrawAaOptions();
			}
			else if (activeGroupId == ElkGroups.MechJeb2)
			{
				DrawMjOptions();
			}

			DrawSetAllRows();
			DrawMapAllRows();

			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				// A slot gated on an add-on of the tab's mod (APR on the AA
				// tab) is hidden while that add-on is absent; its binding
				// stays in the cfg.
				if (ElkSlots.All[i].group == activeGroupId && ElkSlots.All[i].IsAvailable)
				{
					DrawSlotRow(ElkSlots.All[i]);
				}
			}

			GUILayout.EndVertical();

			if (GUILayout.Button("Close"))
			{
				Close();
			}

			GUI.DragWindow();
		}

		/// <summary>
		/// One toggle-styled button per installed group. Groups whose mod
		/// is absent are listed in one line instead of getting a tab.
		/// Switching is blocked during a capture so the capturing row stays
		/// on screen until it resolves.
		/// </summary>
		private void DrawTabs()
		{
			List<ElkGroup> shown = new List<ElkGroup>();
			List<string> missing = new List<string>();
			for (int i = 0; i < ElkGroups.All.Count; i++)
			{
				ElkGroup group = ElkGroups.All[i];
				if (group.IsInstalled)
					shown.Add(group);
				else
					missing.Add(group.tabLabel);
			}

			GUI.enabled = !ElkCapture.IsCapturing;
			GUILayout.BeginHorizontal();
			for (int i = 0; i < shown.Count; i++)
			{
				bool active = activeGroupId == shown[i].id;
				if (GUILayout.Toggle(active, shown[i].tabLabel, GUI.skin.button) && !active)
				{
					activeGroupId = shown[i].id;
				}
			}
			GUILayout.EndHorizontal();
			GUI.enabled = true;

			if (missing.Count > 0)
			{
				GUILayout.Label("Not installed: " + string.Join(", ", missing.ToArray()));
			}
			GUILayout.Space(4);
		}

		/// <summary>
		/// Per-tab master switch ("hotkeys" in that tab's cfg): off
		/// hibernates every slot of the tab, and for a third-party tab every
		/// interaction with that mod, while the bindings stay on disk and
		/// editable. Committed on the click that changes it, like the other
		/// options.
		/// </summary>
		private void DrawGroupHotkeysToggle()
		{
			bool enabled = ElkConfig.GroupHotkeysEnabled(activeGroupId);
			GUI.enabled = !ElkCapture.IsCapturing;
			bool wanted = GUILayout.Toggle(enabled, " Hotkeys of this tab enabled");
			GUI.enabled = true;
			if (wanted != enabled)
			{
				ElkConfig.SetGroupHotkeys(activeGroupId, wanted);
			}
			if (!wanted)
			{
				GUILayout.Label("Off: the bindings below are kept but do nothing.");
			}
			GUILayout.Space(6);
		}

		// ELK tab: the key sets. The list is committed as typed (any text
		// is a valid list; blanks and duplicates are ignored when read), the
		// default set is stepped with the same code the SET_NEXT/SET_PREV
		// hotkeys run, on no vessel, so it lands in ELK.cfg.
		private string setsText;

		private void DrawSetsOptions()
		{
			GUI.enabled = !ElkCapture.IsCapturing;

			// Master switch: the "enabled" value of ELK.cfg. Off, ElkAddon
			// fires nothing at all, in every tab and set.
			bool enabledNow = ElkConfig.Enabled;
			bool enabledWanted = GUILayout.Toggle(enabledNow, " Mod enabled (every hotkey of every tab)");
			if (enabledWanted != enabledNow)
			{
				ElkConfig.SetEnabled(enabledWanted);
			}
			if (!enabledWanted)
			{
				GUILayout.Label("Off: no hotkey fires until this is switched back on.");
			}
			GUILayout.Space(6);

			if (setsText == null)
			{
				string raw = ElkConfig.GetOption(ElkGroups.Global, ElkSets.OptSets);
				setsText = (raw == null) ? "" : raw;
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label("Key sets (comma-separated, empty = off)", GUILayout.Width(250));
			string edited = GUILayout.TextField(setsText, 120);
			GUILayout.EndHorizontal();
			if (edited != setsText)
			{
				setsText = edited;
				ElkConfig.SetOption(ElkGroups.Global, ElkSets.OptSets, edited);
			}

			if (ElkSets.Enabled)
			{
				bool perVessel = ElkSets.PerVessel;
				bool wanted = GUILayout.Toggle(perVessel, " Remember the active set per vessel (saved with the game)");
				if (wanted != perVessel)
				{
					ElkConfig.SetBool(ElkGroups.Global, ElkSets.OptPerVessel, wanted);
				}

				GUILayout.BeginHorizontal();
				GUILayout.Label((perVessel ? "Default set for new vessels: " : "Active set: ") + ElkSets.GlobalActive, GUILayout.Width(300));
				if (GUILayout.Button("<", GUILayout.Width(30)))
				{
					ElkSets.Step(null, -1);
				}
				if (GUILayout.Button(">", GUILayout.Width(30)))
				{
					ElkSets.Step(null, 1);
				}
				GUILayout.EndHorizontal();

				List<string> setNames = ElkSets.Names;
				for (int i = 0; i < setNames.Count; i++)
				{
					bool off = ElkSets.DisengageOnExit(setNames[i]);
					bool wantOff = GUILayout.Toggle(off, " Leaving set '" + setNames[i] + "' switches off what its keys control");
					if (wantOff != off)
					{
						ElkSets.SetDisengageOnExit(setNames[i], wantOff);
					}
				}
				GUILayout.Label("A slot marked 'always' works in every set; the others only in their own set.");
			}
			GUI.enabled = true;
			GUILayout.Space(6);
		}

		// ELK tab: the joysticks known by name (DEVICES node of ELK.cfg).
		// One row per device: its short label (editable, committed as soon
		// as the text is a valid unused label), the full name Unity reports,
		// where that device sits right now, and Forget.
		private readonly Dictionary<int, string> deviceLabelText = new Dictionary<int, string>();

		private void DrawDevices()
		{
			List<ElkJoysticks.Device> devices = ElkJoysticks.Devices;
			if (devices.Count == 0)
				return;
			GUI.enabled = !ElkCapture.IsCapturing;
			GUILayout.Label("Joysticks known by name (a captured joystick button follows its device, not its number):");
			int forget = 0;
			for (int i = 0; i < devices.Count; i++)
			{
				ElkJoysticks.Device d = devices[i];
				string text;
				if (!deviceLabelText.TryGetValue(d.index, out text))
				{
					text = d.label;
					deviceLabelText[d.index] = text;
				}
				GUILayout.BeginHorizontal();
				string edited = GUILayout.TextField(text, ElkJoysticks.MaxLabelLength, GUILayout.Width(85));
				if (edited != text)
				{
					deviceLabelText[d.index] = edited;
					ElkJoysticks.SetLabel(d.index, edited);
				}
				GUILayout.Label("Joystick" + d.index + " = " + d.name, GUILayout.Width(330));
				GUILayout.Label(ElkJoysticks.Status(d), GUILayout.Width(100));
				if (GUILayout.Button("Forget", GUILayout.Width(60)))
				{
					forget = d.index;
				}
				GUILayout.EndHorizontal();
			}
			if (forget != 0)
			{
				deviceLabelText.Remove(forget);
				ElkJoysticks.Forget(forget);
			}
			GUI.enabled = true;
			GUILayout.Space(6);
		}

		/// <summary>Bulk assignment of every slot of the tab to one set (or to 'always'); shown only while sets are in use.</summary>
		private void DrawSetAllRows()
		{
			if (!ElkSets.Enabled || activeGroupId == ElkGroups.Global)
				return;
			GUI.enabled = !ElkCapture.IsCapturing;
			GUILayout.BeginHorizontal();
			GUILayout.Label("Set all rows to:", GUILayout.Width(110));
			List<string> choices = SetChoices();
			for (int i = 0; i < choices.Count; i++)
			{
				if (GUILayout.Button(ElkSets.DisplayName(choices[i]), GUILayout.Width(SetButtonWidth)))
				{
					for (int s = 0; s < ElkSlots.All.Count; s++)
					{
						if (ElkSlots.All[s].group == activeGroupId)
							ElkConfig.SetSlotSet(ElkSlots.All[s].id, choices[i]);
					}
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUI.enabled = true;
			GUILayout.Space(4);
		}

		/// <summary>Bulk switch of the map view flag for every slot of the tab. Not in the ELK tab: set switching always works in map view.</summary>
		private void DrawMapAllRows()
		{
			if (activeGroupId == ElkGroups.Global)
				return;
			GUI.enabled = !ElkCapture.IsCapturing;
			GUILayout.BeginHorizontal();
			GUILayout.Label("Map view, all rows:", GUILayout.Width(130));
			bool on = GUILayout.Button("On", GUILayout.Width(MapButtonWidth));
			bool off = GUILayout.Button("Off", GUILayout.Width(MapButtonWidth));
			GUILayout.Label("(Map = the key also works in map view)");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUI.enabled = true;
			if (on || off)
			{
				for (int s = 0; s < ElkSlots.All.Count; s++)
				{
					if (ElkSlots.All[s].group == activeGroupId)
						ElkConfig.SetSlotMap(ElkSlots.All[s].id, on);
				}
			}
			GUILayout.Space(4);
		}

		private static List<string> SetChoices()
		{
			List<string> choices = new List<string>();
			choices.Add("");
			choices.AddRange(ElkSets.Names);
			return choices;
		}

		private static bool AnyBoundIn(string groupId)
		{
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				ElkSlot slot = ElkSlots.All[i];
				if (slot.group != groupId)
					continue;
				ElkBind bind;
				if (ElkConfig.Binds.TryGetValue(slot.id, out bind) && bind != null && !bind.IsNone)
					return true;
			}
			return false;
		}

		/// <summary>
		/// The one option of the Squad tab: whether an SAS mode hotkey may
		/// switch SAS on by itself. Committed to the cfg on the click that
		/// changes it, same as a capture - there is no Apply button and
		/// nothing to undo, so writing on change keeps the window stateless.
		/// </summary>
		private void DrawSasAutoEngageToggle()
		{
			GUI.enabled = !ElkCapture.IsCapturing;
			// Leading space: IMGUI draws the toggle's label flush against
			// its checkbox otherwise.
			bool wanted = GUILayout.Toggle(ElkConfig.SasAutoEngage,
				" Vector key also switch SAS on");
			GUI.enabled = true;
			if (wanted != ElkConfig.SasAutoEngage)
			{
				ElkConfig.SetSasAutoEngage(wanted);
			}

			GUI.enabled = !ElkCapture.IsCapturing;
			bool wantedNavball = GUILayout.Toggle(ElkConfig.SasNavball,
				" Vector key also shows the navball if hidden");
			GUI.enabled = true;
			if (wantedNavball != ElkConfig.SasNavball)
			{
				ElkConfig.SetSasNavball(wantedNavball);
			}
			GUILayout.Space(6);
		}

		// AtmosphereAutopilot tab: two checkboxes, three step fields and the
		// Import / Export / Restore buttons for AA's own keys, with the last
		// action's report underneath (kept until the next action or a tab
		// switch). Step fields are committed as soon as the text parses to
		// a different value; a partial or invalid entry is simply not
		// committed yet, so the cfg never holds garbage.
		private readonly Dictionary<string, string> aaStepText = new Dictionary<string, string>();
		private readonly List<string> aaReport = new List<string>();
		private string aaReportGroupId;

		private void DrawAaOptions()
		{
			GUI.enabled = !ElkCapture.IsCapturing;

			DrawBoolOption(ElkGroups.AtmosphereAutopilot, ElkAaBridge.OptAutoEngage, true,
				" Cruise/FBW keys also switch the autopilot on");
			DrawBoolOption(ElkGroups.AtmosphereAutopilot, ElkAaBridge.OptAutoShow, false,
				" Also open the AA window when switching it on");

			GUILayout.BeginHorizontal();
			DrawStepField(ElkGroups.AtmosphereAutopilot, "V/S step", ElkAaBridge.OptVsStep, 1f);
			DrawStepField(ElkGroups.AtmosphereAutopilot, "ALT step", ElkAaBridge.OptAltStep, 50f);
			DrawStepField(ElkGroups.AtmosphereAutopilot, "HDG step", ElkAaBridge.OptHdgStep, 1f);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			bool hotkeysOn = ElkConfig.GroupHotkeysEnabled(ElkGroups.AtmosphereAutopilot);
			GUI.enabled = !ElkCapture.IsCapturing && hotkeysOn;
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Import from AA", GUILayout.Width(130)))
			{
				SetAaReport(ElkAaKeys.Import());
			}
			if (GUILayout.Button("Export to AA", GUILayout.Width(130)))
			{
				SetAaReport(ElkAaKeys.Export());
			}
			if (GUILayout.Button(ElkAaKeys.BackupExists ? "Restore AA keys" : "Reset AA keys", GUILayout.Width(130)))
			{
				SetAaReport(ElkAaKeys.Restore());
			}
			GUILayout.EndHorizontal();
			GUI.enabled = true;
			GUILayout.Label("Import moves AA's own keys into the slots below and clears them in AA; Export does the reverse.");

			if (aaReportGroupId == activeGroupId)
			{
				for (int i = 0; i < aaReport.Count; i++)
				{
					GUILayout.Label(aaReport[i]);
				}
			}
			GUILayout.Space(6);
		}

		private void SetAaReport(List<string> lines)
		{
			aaReport.Clear();
			aaReport.AddRange(lines);
			aaReportGroupId = activeGroupId;
		}

		private void DrawBoolOption(string groupId, string key, bool fallback, string label)
		{
			bool current = ElkConfig.GetBool(groupId, key, fallback);
			bool wanted = GUILayout.Toggle(current, label);
			if (wanted != current)
			{
				ElkConfig.SetBool(groupId, key, wanted);
			}
		}

		private void DrawStepField(string groupId, string label, string key, float fallback)
		{
			float current = ElkConfig.GetFloat(groupId, key, fallback);
			string text;
			if (!aaStepText.TryGetValue(key, out text))
			{
				text = current.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
				aaStepText[key] = text;
			}
			GUILayout.Label(label, GUILayout.Width(65));
			string edited = GUILayout.TextField(text, 8, GUILayout.Width(55));
			if (edited != text)
			{
				aaStepText[key] = edited;
				float parsed;
				if (float.TryParse(edited, System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed != current)
				{
					ElkConfig.SetFloat(groupId, key, parsed);
				}
			}
			GUILayout.Space(8);
		}

		// MechJeb2 tab: two checkboxes, the Translatron speed step, and Copy
		// from/to SAS (stock SAS slots <-> SmartASS slots, same meaning; the
		// source is never cleared - with key sets both copies coexist).
		private readonly List<string> mjReport = new List<string>();

		private void DrawMjOptions()
		{
			GUI.enabled = !ElkCapture.IsCapturing;

			DrawBoolOption(ElkGroups.MechJeb2, ElkMjBridge.OptAutoEngage, true,
				" Translatron speed and Kill H/S keys also switch it on (Keep vertical)");
			DrawBoolOption(ElkGroups.MechJeb2, ElkMjBridge.OptAutoShow, false,
				" Also open the MechJeb window of the module a key drives");
			DrawBoolOption(ElkGroups.MechJeb2, ElkMjBridge.OptNavball, false,
				" SmartASS key also shows the navball if hidden");

			GUILayout.BeginHorizontal();
			DrawStepField(ElkGroups.MechJeb2, "Speed step", ElkMjBridge.OptTransStep, 1f);
			GUILayout.Label("m/s (Translatron: Speed +/-step)");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			bool hotkeysOn = ElkConfig.GroupHotkeysEnabled(ElkGroups.MechJeb2);
			GUI.enabled = !ElkCapture.IsCapturing && hotkeysOn;
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Copy from SAS", GUILayout.Width(130)))
			{
				mjReport.Clear();
				mjReport.AddRange(ElkConfig.CopyBinds(ElkSlots.SasToSmartAss, false, "SAS", "SmartASS"));
			}
			if (GUILayout.Button("Copy to SAS", GUILayout.Width(130)))
			{
				mjReport.Clear();
				mjReport.AddRange(ElkConfig.CopyBinds(ElkSlots.SasToSmartAss, true, "SmartASS", "SAS"));
			}
			GUILayout.EndHorizontal();
			GUI.enabled = true;
			GUILayout.Label("Copies the 10 stock SAS keys onto the matching SmartASS slots (or back); the source keeps its keys.");
			for (int i = 0; i < mjReport.Count; i++)
			{
				GUILayout.Label(mjReport[i]);
			}
			GUILayout.Space(6);
		}

		private void DrawSlotRow(ElkSlot slot)
		{
			ElkBind bind;
			if (!ElkConfig.Binds.TryGetValue(slot.id, out bind) || bind == null)
			{
				bind = new ElkBind();
			}

			GUILayout.BeginHorizontal();
			GUILayout.Label(slot.displayName, GUILayout.Width(LabelWidth));

			if (ElkSets.Enabled)
			{
				DrawSetButton(slot);
			}
			DrawMapButton(slot);

			bool isCapturingThisRow = capturingSlotId == slot.id && ElkCapture.IsCapturing;
			string bindLabel = isCapturingThisRow ? "Press a key..." : bind.Describe();
			GUILayout.Label(bindLabel, GUILayout.Width(BindWidth));

			GUI.enabled = !ElkCapture.IsCapturing;
			if (GUILayout.Button("Capture", GUILayout.Width(85)))
			{
				BeginCapture(slot.id);
			}
			if (GUILayout.Button("Clear", GUILayout.Width(65)))
			{
				CommitClear(slot.id);
			}
			GUI.enabled = true;

			GUILayout.EndHorizontal();

			if (lastCaptureSlotId == slot.id && lastConflicts.Count > 0)
			{
				for (int i = 0; i < lastConflicts.Count; i++)
				{
					GUILayout.Label("  [!] also bound to: " + lastConflicts[i]);
				}
			}
		}

		/// <summary>One small button showing the slot's set; a click cycles always -> set 1 -> set 2 ... The set-switching slots themselves are always-on by nature and get a fixed label.</summary>
		private void DrawSetButton(ElkSlot slot)
		{
			if (slot.group == ElkGroups.Global)
			{
				GUILayout.Label("always", GUILayout.Width(SetButtonWidth));
				return;
			}
			string current = ElkSets.SetOf(slot.id);
			GUI.enabled = !ElkCapture.IsCapturing;
			if (GUILayout.Button(ElkSets.DisplayName(current), GUILayout.Width(SetButtonWidth)))
			{
				List<string> choices = SetChoices();
				int index = choices.IndexOf(current);
				string next = choices[(index + 1) % choices.Count];
				ElkConfig.SetSlotSet(slot.id, next);
			}
			GUI.enabled = true;
		}

		/// <summary>Button-styled toggle, pressed = the slot also fires in map view. The set-switching slots always do and get a fixed label.</summary>
		private void DrawMapButton(ElkSlot slot)
		{
			if (slot.group == ElkGroups.Global)
			{
				GUILayout.Label("in map", GUILayout.Width(MapButtonWidth));
				return;
			}
			bool current = ElkConfig.GetSlotMap(slot.id);
			GUI.enabled = !ElkCapture.IsCapturing;
			bool wanted = GUILayout.Toggle(current, "Map", GUI.skin.button, GUILayout.Width(MapButtonWidth));
			GUI.enabled = true;
			if (wanted != current)
			{
				ElkConfig.SetSlotMap(slot.id, wanted);
			}
		}

		private void BeginCapture(string slotId)
		{
			capturingSlotId = slotId;
			ElkCapture.Begin(
				delegate(ElkBind captured) { OnCaptured(slotId, captured); },
				delegate { OnCancelled(); },
				delegate { CommitClear(slotId); });
		}

		private void OnCaptured(string slotId, ElkBind bind)
		{
			capturingSlotId = null;
			lastCaptureSlotId = slotId;
			lastConflicts.Clear();
			lastConflicts.AddRange(ElkConflicts.Describe(bind, slotId, ElkConfig.Binds));
			ElkConfig.SetBind(slotId, bind);
		}

		private void OnCancelled()
		{
			capturingSlotId = null;
		}

		private void CommitClear(string slotId)
		{
			capturingSlotId = null;
			lastCaptureSlotId = null;
			lastConflicts.Clear();
			ElkConfig.SetBind(slotId, new ElkBind());
		}
	}
}
