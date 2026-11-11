// SCANsat by reflection only. Each slot toggles one SCANsat window exactly
// like its toolbar button / toolbar menu does (verified on SCANsat 22.1
// source and 21.3 decompiled): read the static Instance, then
// "if (IsVisible) Close(); else Open(...)". Open/Close sync SCANsat's own
// toolbar button and menu toggles themselves, so no icon work here.
// The Settings window is deliberately not exposed.

using System;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkScanBridge
	{
		private const string ModLabel = "SCANsat";
		private const string DllName = "SCANsat";
		private const string MapTypeName = "SCANsat.SCAN_Map.SCANmap";

		public const int SmallMap = 0;
		public const int BigMap = 1;
		public const int ZoomMap = 2;
		public const int Overlay = 3;
		public const int Instruments = 4;

		private static readonly string[] TypeNames =
		{
			"SCANsat.SCAN_Unity.SCAN_UI_MainMap",
			"SCANsat.SCAN_Unity.SCAN_UI_BigMap",
			"SCANsat.SCAN_Unity.SCAN_UI_ZoomMap",
			"SCANsat.SCAN_Unity.SCAN_UI_Overlay",
			"SCANsat.SCAN_Unity.SCAN_UI_Instruments",
		};

		private static readonly string[] WindowLabels = { "small map", "big map", "zoom map", "overlay", "instruments" };

		private static bool installChecked;
		private static bool installed;
		private static Assembly assembly;

		private static bool resolved;
		private static readonly PropertyInfo[] instanceProps = new PropertyInfo[TypeNames.Length];
		private static readonly PropertyInfo[] visibleProps = new PropertyInfo[TypeNames.Length];
		private static readonly MethodInfo[] openMethods = new MethodInfo[TypeNames.Length];
		private static readonly MethodInfo[] closeMethods = new MethodInfo[TypeNames.Length];
		private static readonly object[][] openArgs = new object[TypeNames.Length][];

		private static readonly bool[] warnedNoInstance = new bool[TypeNames.Length];
		private static bool warnedInvoke;

		/// <summary>True if SCANsat.dll is loaded (exact dll name: SCANsat.Unity and SCANmechjeb do not match). Cheap after the first call.</summary>
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

		public static void ToggleSmallMap(Vessel vessel) { Toggle(SmallMap); }
		public static void ToggleBigMap(Vessel vessel) { Toggle(BigMap); }
		public static void ToggleZoomMap(Vessel vessel) { Toggle(ZoomMap); }
		public static void ToggleOverlay(Vessel vessel) { Toggle(Overlay); }
		public static void ToggleInstruments(Vessel vessel) { Toggle(Instruments); }

		private static void Toggle(int w)
		{
			Resolve();
			if (instanceProps[w] == null || visibleProps[w] == null || openMethods[w] == null || closeMethods[w] == null)
				return;

			object window;
			try
			{
				window = instanceProps[w].GetValue(null, null);
			}
			catch (Exception e)
			{
				WarnInvoke(WindowLabels[w] + " Instance", e);
				return;
			}
			if (window == null)
			{
				// SCANcontroller creates the UI objects in its Flight start;
				// before that (or without the scenario) there is nothing to
				// toggle: deliberate no-op.
				if (!warnedNoInstance[w])
				{
					warnedNoInstance[w] = true;
					Debug.LogWarning("[ELK] " + ModLabel + ": " + WindowLabels[w] + " not created yet, hotkey ignored");
				}
				return;
			}

			try
			{
				bool visible = (bool)visibleProps[w].GetValue(window, null);
				if (visible)
					closeMethods[w].Invoke(window, null);
				else
					openMethods[w].Invoke(window, openArgs[w]);
				Debug.Log("[ELK] " + ModLabel + ": " + WindowLabels[w] + (visible ? " closed" : " opened"));
			}
			catch (Exception e)
			{
				WarnInvoke(WindowLabels[w] + " toggle", e);
			}
		}

		private static void Resolve()
		{
			if (resolved)
				return;
			resolved = true;
			if (!IsInstalled())
				return;

			BindingFlags pubStatic = BindingFlags.Public | BindingFlags.Static;
			BindingFlags pubInst = BindingFlags.Public | BindingFlags.Instance;
			int found = 0;
			int ready = 0;
			for (int w = 0; w < TypeNames.Length; w++)
			{
				Type t = ElkReflection.FindType(assembly, TypeNames[w], ModLabel);
				instanceProps[w] = ElkReflection.FindProperty(t, "Instance", pubStatic, ModLabel);
				visibleProps[w] = ElkReflection.FindProperty(t, "IsVisible", pubInst, ModLabel);
				closeMethods[w] = ElkReflection.FindMethod(t, "Close", pubInst, Type.EmptyTypes, ModLabel);

				if (w == ZoomMap)
				{
					// Open(bool v, double lat = 0, double lon = 0, SCANmap m = null):
					// v = true centres on the active vessel, as the toolbar
					// menu does. Optional parameters must be passed explicitly
					// through reflection.
					Type mapType = ElkReflection.FindType(assembly, MapTypeName, ModLabel);
					if (mapType != null)
						openMethods[w] = ElkReflection.FindMethod(t, "Open", pubInst,
							new Type[] { typeof(bool), typeof(double), typeof(double), mapType }, ModLabel);
					openArgs[w] = new object[] { true, 0.0, 0.0, null };
				}
				else
				{
					openMethods[w] = ElkReflection.FindMethod(t, "Open", pubInst, Type.EmptyTypes, ModLabel);
					openArgs[w] = null;
				}

				int members = (instanceProps[w] != null ? 1 : 0) + (visibleProps[w] != null ? 1 : 0)
					+ (openMethods[w] != null ? 1 : 0) + (closeMethods[w] != null ? 1 : 0);
				found += members;
				if (members == 4)
					ready++;
			}
			Debug.Log("[ELK] " + ModLabel + " bridge: " + found + "/" + (TypeNames.Length * 4)
				+ " members resolved, " + ready + "/" + TypeNames.Length + " window hotkeys active");
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
