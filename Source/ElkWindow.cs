// Minimal IMGUI window ("UI minimale" — deliberately much smaller than
// KRILL's 3-column layout): one row per slot (label, current bind, Capture,
// Clear), any live conflict warning shown inline underneath. ELK only ever
// binds whole vessel-level actions, so there's no per-part target to pick
// the way KRILL needs.
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
		// scrollbar or leave dead space under the last row.
		private Rect windowRect = new Rect(200, 100, 540, 400);
		private string capturingSlotId;
		private string lastCaptureSlotId;
		private readonly List<string> lastConflicts = new List<string>();

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
			bool anyBound = false;
			foreach (KeyValuePair<string, ElkBind> kv in ElkConfig.Binds)
			{
				if (!kv.Value.IsNone)
				{
					anyBound = true;
					break;
				}
			}
			if (!anyBound)
			{
				GUILayout.Label("No hotkeys configured yet. Click Capture or install a preset.");
			}

			if (ElkCapture.IsCapturing)
			{
				GUILayout.Label("Capturing: press a key or combo to bind it - Delete clears it, Esc cancels.");
			}

			DrawSasAutoEngageToggle();

			// Width pinned to what a row actually needs (170 + 170 + 85 + 65
			// plus IMGUI's own spacing), so the window keeps one width across
			// every state instead of twitching wider whenever a longer line
			// of text appears above the rows.
			GUILayout.BeginVertical(GUILayout.Width(510));
			for (int i = 0; i < ElkSlots.All.Count; i++)
			{
				DrawSlotRow(ElkSlots.All[i]);
			}
			GUILayout.EndVertical();

			if (GUILayout.Button("Close"))
			{
				Close();
			}

			GUI.DragWindow();
		}

		/// <summary>
		/// The one global option in this window: whether an SAS mode hotkey
		/// may switch SAS on by itself. Committed to the cfg on the click
		/// that changes it, same as a capture - there is no Apply button and
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
			GUILayout.Label(slot.displayName, GUILayout.Width(170));

			bool isCapturingThisRow = capturingSlotId == slot.id && ElkCapture.IsCapturing;
			string bindLabel = isCapturingThisRow ? "Press a key..." : bind.Describe();
			GUILayout.Label(bindLabel, GUILayout.Width(170));

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
