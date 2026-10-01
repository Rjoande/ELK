// NavUtilities (NavInstruments Continued) reached purely by reflection:
// ELK never references NavUtilitiesUpdated.dll, so a missing or reshaped
// NavUtilities only makes the slot a no-op. Member names verified against
// NavUtilitiesUpdated.dll 0.8.1.1, decompiled:
//
//   NavInstruments.NavUtilLib.GlobalVariables.Settings.appReference
//       public static NavUtilLibApp - the Flight addon instance, set in Awake
//   NavInstruments.NavUtilLib.GlobalVariables.Settings.hsiState
//       public static bool - true while the HSI window is shown
//   NavInstruments.NavUtilLib.NavUtilLibApp.displayHSI()
//       public instance - what the toolbar button's left click calls
//   NavInstruments.NavUtilLib.NavUtilLibApp.toolbarControl
//       private static ToolbarControl_NS.ToolbarControl
//
// Why the icon needs a separate sync: NavUtilities registers its toolbar
// button with null onTrue/onFalse and a left-click callback. The stock
// ApplicationLauncherButton.SetTrue/SetFalse only ever raise onTrue/onFalse
// (UIRadioButton.SetState, decompiled), never the click callback, so there
// is no way to "click" the button from code; calling displayHSI() directly
// toggles the window but leaves the icon's toggled look behind. Hence:
// displayHSI(), then SetTrue(false)/SetFalse(false) on the ToolbarControl
// to match hsiState without firing any callback. The ToolbarControl type is
// reached through the field's own FieldType, so this bridge compiles and
// runs without ToolbarControl.dll too.

using System;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkNavBridge
	{
		private const string ModLabel = "NavUtilities";
		private const string DllName = "NavUtilitiesUpdated";
		private const string AppTypeName = "NavInstruments.NavUtilLib.NavUtilLibApp";
		private const string SettingsTypeName = "NavInstruments.NavUtilLib.GlobalVariables.Settings";

		private static bool installChecked;
		private static bool installed;
		private static Assembly assembly;

		private static bool resolved;
		private static bool ready;
		private static FieldInfo appReferenceField;
		private static FieldInfo hsiStateField;
		private static FieldInfo toolbarControlField;
		private static MethodInfo displayHsiMethod;
		private static MethodInfo setTrueMethod;
		private static MethodInfo setFalseMethod;

		private static bool warnedNoApp;
		private static bool warnedInvoke;

		/// <summary>True if NavUtilitiesUpdated.dll is loaded. Cheap after the first call; drives the window tab and the cfg creation.</summary>
		public static bool IsInstalled()
		{
			if (!installChecked)
			{
				installChecked = true;
				assembly = ElkReflection.FindAssembly(DllName);
				installed = assembly != null;
			}
			return installed;
		}

		/// <summary>Opens or closes the HSI window, exactly like a left click on the NavUtilities toolbar button, and keeps the icon in sync.</summary>
		public static void ToggleHsi()
		{
			Resolve();
			if (!ready)
				return;

			object app;
			try
			{
				app = appReferenceField.GetValue(null);
			}
			catch (Exception e)
			{
				WarnInvoke("reading appReference", e);
				return;
			}
			if (app == null)
			{
				// The NavUtilities Flight addon has not run its Awake yet
				// (or is gone): nothing to toggle, deliberate no-op.
				if (!warnedNoApp)
				{
					warnedNoApp = true;
					Debug.LogWarning("[ELK] " + ModLabel + ": HSI app instance not present, hotkey ignored");
				}
				return;
			}

			try
			{
				displayHsiMethod.Invoke(app, null);
			}
			catch (Exception e)
			{
				WarnInvoke("displayHSI", e);
				return;
			}
			SyncToolbarIcon();
		}

		private static void Resolve()
		{
			if (resolved)
				return;
			resolved = true;
			if (!IsInstalled())
				return;

			Type appType = ElkReflection.FindType(assembly, AppTypeName, ModLabel);
			Type settingsType = ElkReflection.FindType(assembly, SettingsTypeName, ModLabel);

			appReferenceField = ElkReflection.FindField(settingsType, "appReference", BindingFlags.Public | BindingFlags.Static, ModLabel);
			hsiStateField = ElkReflection.FindField(settingsType, "hsiState", BindingFlags.Public | BindingFlags.Static, ModLabel);
			displayHsiMethod = ElkReflection.FindMethod(appType, "displayHSI", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes, ModLabel);
			toolbarControlField = ElkReflection.FindField(appType, "toolbarControl", BindingFlags.NonPublic | BindingFlags.Static, ModLabel);
			if (toolbarControlField != null)
			{
				Type tcType = toolbarControlField.FieldType;
				setTrueMethod = ElkReflection.FindMethod(tcType, "SetTrue", BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(bool) }, ModLabel);
				setFalseMethod = ElkReflection.FindMethod(tcType, "SetFalse", BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(bool) }, ModLabel);
			}

			// The toggle itself needs only the app instance and displayHSI;
			// the icon sync is a nice-to-have that degrades on its own.
			ready = appReferenceField != null && displayHsiMethod != null;
			int found = (appReferenceField != null ? 1 : 0) + (hsiStateField != null ? 1 : 0)
				+ (displayHsiMethod != null ? 1 : 0) + (toolbarControlField != null ? 1 : 0)
				+ (setTrueMethod != null ? 1 : 0) + (setFalseMethod != null ? 1 : 0);
			Debug.Log("[ELK] " + ModLabel + " bridge: " + found + "/6 members resolved, hotkey " + (ready ? "active" : "disabled"));
		}

		private static void SyncToolbarIcon()
		{
			if (hsiStateField == null || toolbarControlField == null || setTrueMethod == null || setFalseMethod == null)
				return;
			try
			{
				object toolbarControl = toolbarControlField.GetValue(null);
				if (toolbarControl == null)
					return;
				bool shown = (bool)hsiStateField.GetValue(null);
				MethodInfo setter = shown ? setTrueMethod : setFalseMethod;
				// false = no makeCall: adjust the icon only, never re-enter
				// NavUtilities' own callbacks.
				setter.Invoke(toolbarControl, new object[] { false });
			}
			catch (Exception e)
			{
				WarnInvoke("toolbar icon sync", e);
			}
		}

		private static void WarnInvoke(string what, Exception e)
		{
			if (warnedInvoke)
				return;
			warnedInvoke = true;
			Debug.LogWarning("[ELK] " + ModLabel + ": " + what + " failed, further errors muted: " + e);
		}
	}
}
