// MechJeb2 reached purely by reflection: ELK never references MechJeb2.dll
// (GPL-3) and copies none of its code; a missing or reshaped MechJeb only
// turns the affected slots into no-ops. Two layers, both verified against
// MechJeb2 2.15.3.0, decompiled:
//
// Layer 1 - the 18 [KSPAction] methods MechJebCore already exposes
// (public void OnXAction(KSPActionParam)), invoked by name on ANY
// MechJebCore PartModule of the ACTIVE vessel. Each of them starts with
// vessel.GetMasterMechJeb() (first core with `running` on that vessel,
// cached per FixedUpdate and vessel id, rebound on vessel change and
// modification), so the effect is vessel-wide whichever part is used,
// exactly like an action group; a stock action group invokes it once per
// core, ELK once. Nothing is cached across presses: the vessel is looked
// up on every key press. Names stable across MechJeb versions.
//
// Layer 2 - what the actions do not cover, through the same master core:
//   MuMech.VesselExtensions.GetMasterMechJeb(Vessel)    public static
//   MechJebCore.GetComputerModule(string typeName)     public, case-insensitive
//   MechJebCore.Thrust / Landing / Node / Target        public fields
//   MechJebCore.Ascent                                  public property = AscentSettings.AscentAutopilot
//   ComputerModule.Enabled (property), DisplayModule.Hidden (field)
//   MechJebModuleSmartASS: public Mode mode, public Target target, Engage(bool resetPID)
//       (nested enums Mode / Target); the NODE button sits outside the
//       mode switch, TARGET_PLUS/MINUS only exist in Mode.TARGET
//   MechJebModuleTranslatron.SetMode(MechJebModuleThrustController.TMode)
//   MechJebModuleTranslatron.trans_spd: public EditableDouble field, the
//       editable "Speed" box of the window (persisted in the save)
//   EditableDoubleMult.Val: public double property; its setter also
//       refreshes the box text
//   MechJebModuleThrustController: public TMode Tmode (property), public float TransSpdAct
//       (the live setpoint the controller flies), public bool TransKillH
//       (the Kill H/S checkbox; read only while Tmode is KEEP_VERTICAL). The window's +/0/-
//       buttons and "Set speed" write trans_spd FIRST and then copy it
//       into TransSpdAct; SetMode(newMode) copies trans_spd into
//       TransSpdAct too. Writing TransSpdAct alone (what MechJeb's own
//       ZeroSpeed/PlusOne/MinusOne actions do) leaves the box stale and
//       the next button press or mode change reverts the change, so the
//       ELK speed slots write both, in that order.
//   MechJebModuleTargetController: public bool NormalTargetExists / PositionTargetExists
//   MechJebModuleLandingAutopilot: LandAtPositionTarget(object), StopLanding(), Enabled
//   MechJebModuleNodeExecutor: ExecuteOneNode(object), Abort(), Enabled
//   MechJebModuleMenu.ShowHideWindow()
// Gates copied from MechJeb's own UI: a module that is Hidden (locked in
// career) is never driven; "land at target" needs a position target and a
// vessel not landed; "execute node" needs a planned node with a burn
// vector. The `controller` object handed to Landing/Node is the same
// display module MechJeb's buttons pass (Landing Guidance, Maneuver
// Planner): its Users pool works by object identity.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkMjBridge
	{
		private const string ModLabel = "MechJeb2";
		private const string DllName = "MechJeb2";
		private const string Ns = "MuMech.";

		public const string OptAutoEngage = "mj_autoengage";
		public const string OptAutoShow = "mj_autoshow";
		public const string OptTransStep = "mj_trans_step";

		private static bool installChecked;
		private static bool installed;
		private static Assembly assembly;

		private static bool resolved;
		private static bool ready;

		private static Type coreType;
		private static MethodInfo getMasterMechJeb;        // static VesselExtensions.GetMasterMechJeb(Vessel)
		private static MethodInfo getComputerModuleByName; // MechJebCore.GetComputerModule(string)
		private static FieldInfo coreThrust;
		private static FieldInfo coreLanding;
		private static FieldInfo coreNode;
		private static FieldInfo coreTarget;
		private static PropertyInfo coreAscent;            // MechJebCore.Ascent
		private static PropertyInfo moduleEnabled;         // ComputerModule.Enabled
		private static FieldInfo displayHidden;            // DisplayModule.Hidden

		private static FieldInfo sassMode;
		private static FieldInfo sassTarget;
		private static MethodInfo sassEngage;
		private static object sassModeTarget;              // Mode.TARGET
		private static object sassTargetNode;              // Target.NODE
		private static object sassTargetPlus;              // Target.TARGET_PLUS
		private static object sassTargetMinus;             // Target.TARGET_MINUS

		private static MethodInfo transSetMode;            // Translatron.SetMode(TMode)
		private static PropertyInfo thrustTmode;           // ThrustController.Tmode
		private static FieldInfo thrustTransSpdAct;
		private static FieldInfo thrustTransKillH;         // ThrustController.TransKillH
		private static FieldInfo transSpd;                 // Translatron.trans_spd (EditableDouble)
		private static PropertyInfo editableVal;           // EditableDoubleMult.Val
		private static object tmodeOff;
		private static object tmodeKeepVertical;
		private static object tmodeKeepSurface;
		private static object tmodeKeepOrbital;

		private static PropertyInfo targetNormalExists;
		private static PropertyInfo targetPositionExists;
		private static MethodInfo landingLandAtTarget;     // LandAtPositionTarget(object)
		private static MethodInfo landingStop;             // StopLanding()
		private static MethodInfo nodeExecuteOne;          // ExecuteOneNode(object)
		private static MethodInfo nodeAbort;               // Abort()
		private static MethodInfo menuShowHide;            // MechJebModuleMenu.ShowHideWindow()

		private static readonly Dictionary<string, MethodInfo> actions = new Dictionary<string, MethodInfo>();

		private static bool warnedNoCore;
		private static bool warnedInvoke;

		// ---- options ----

		public static bool AutoEngage
		{
			get { return ElkConfig.GetBool(ElkGroups.MechJeb2, OptAutoEngage, true); }
		}

		public static bool AutoShow
		{
			get { return ElkConfig.GetBool(ElkGroups.MechJeb2, OptAutoShow, false); }
		}

		public static float TransStep
		{
			get { return ElkConfig.GetFloat(ElkGroups.MechJeb2, OptTransStep, 1f); }
		}

		/// <summary>True if MechJeb2.dll is loaded. Cheap after the first call; drives the window tabs and the cfg creation.</summary>
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

		// ---- resolution ----

		private static void Resolve()
		{
			if (resolved)
				return;
			resolved = true;
			if (!IsInstalled())
				return;

			const BindingFlags pubInst = BindingFlags.Public | BindingFlags.Instance;
			const BindingFlags pubStatic = BindingFlags.Public | BindingFlags.Static;

			coreType = ElkReflection.FindType(assembly, Ns + "MechJebCore", ModLabel);
			Type vesselExt = ElkReflection.FindType(assembly, Ns + "VesselExtensions", ModLabel);
			Type computerModule = ElkReflection.FindType(assembly, Ns + "ComputerModule", ModLabel);
			Type displayModule = ElkReflection.FindType(assembly, Ns + "DisplayModule", ModLabel);
			Type sassType = ElkReflection.FindType(assembly, Ns + "MechJebModuleSmartASS", ModLabel);
			Type transType = ElkReflection.FindType(assembly, Ns + "MechJebModuleTranslatron", ModLabel);
			Type thrustType = ElkReflection.FindType(assembly, Ns + "MechJebModuleThrustController", ModLabel);
			Type targetType = ElkReflection.FindType(assembly, Ns + "MechJebModuleTargetController", ModLabel);
			Type landingType = ElkReflection.FindType(assembly, Ns + "MechJebModuleLandingAutopilot", ModLabel);
			Type nodeType = ElkReflection.FindType(assembly, Ns + "MechJebModuleNodeExecutor", ModLabel);
			Type menuType = ElkReflection.FindType(assembly, Ns + "MechJebModuleMenu", ModLabel);

			getMasterMechJeb = ElkReflection.FindMethod(vesselExt, "GetMasterMechJeb", pubStatic, new Type[] { typeof(Vessel) }, ModLabel);
			getComputerModuleByName = ElkReflection.FindMethod(coreType, "GetComputerModule", pubInst, new Type[] { typeof(string) }, ModLabel);
			coreThrust = ElkReflection.FindField(coreType, "Thrust", pubInst, ModLabel);
			coreLanding = ElkReflection.FindField(coreType, "Landing", pubInst, ModLabel);
			coreNode = ElkReflection.FindField(coreType, "Node", pubInst, ModLabel);
			coreTarget = ElkReflection.FindField(coreType, "Target", pubInst, ModLabel);
			coreAscent = ElkReflection.FindProperty(coreType, "Ascent", pubInst, ModLabel);
			moduleEnabled = ElkReflection.FindProperty(computerModule, "Enabled", pubInst, ModLabel);
			displayHidden = ElkReflection.FindField(displayModule, "Hidden", pubInst, ModLabel);

			sassMode = ElkReflection.FindField(sassType, "mode", pubInst, ModLabel);
			sassTarget = ElkReflection.FindField(sassType, "target", pubInst, ModLabel);
			sassEngage = ElkReflection.FindMethod(sassType, "Engage", pubInst, new Type[] { typeof(bool) }, ModLabel);
			Type modeEnum = ElkReflection.FindNestedType(sassType, "Mode", ModLabel);
			Type targetEnum = ElkReflection.FindNestedType(sassType, "Target", ModLabel);
			sassModeTarget = ParseEnum(modeEnum, "TARGET");
			sassTargetNode = ParseEnum(targetEnum, "NODE");
			sassTargetPlus = ParseEnum(targetEnum, "TARGET_PLUS");
			sassTargetMinus = ParseEnum(targetEnum, "TARGET_MINUS");

			Type tmodeEnum = ElkReflection.FindNestedType(thrustType, "TMode", ModLabel);
			transSetMode = (tmodeEnum == null) ? null
				: ElkReflection.FindMethod(transType, "SetMode", pubInst, new Type[] { tmodeEnum }, ModLabel);
			thrustTmode = ElkReflection.FindProperty(thrustType, "Tmode", pubInst, ModLabel);
			thrustTransSpdAct = ElkReflection.FindField(thrustType, "TransSpdAct", pubInst, ModLabel);
			thrustTransKillH = ElkReflection.FindField(thrustType, "TransKillH", pubInst, ModLabel);
			transSpd = ElkReflection.FindField(transType, "trans_spd", pubInst, ModLabel);
			Type editableType = ElkReflection.FindType(assembly, Ns + "EditableDoubleMult", ModLabel);
			editableVal = ElkReflection.FindProperty(editableType, "Val", pubInst, ModLabel);
			tmodeOff = ParseEnum(tmodeEnum, "OFF");
			tmodeKeepVertical = ParseEnum(tmodeEnum, "KEEP_VERTICAL");
			tmodeKeepSurface = ParseEnum(tmodeEnum, "KEEP_SURFACE");
			tmodeKeepOrbital = ParseEnum(tmodeEnum, "KEEP_ORBITAL");

			targetNormalExists = ElkReflection.FindProperty(targetType, "NormalTargetExists", pubInst, ModLabel);
			targetPositionExists = ElkReflection.FindProperty(targetType, "PositionTargetExists", pubInst, ModLabel);
			landingLandAtTarget = ElkReflection.FindMethod(landingType, "LandAtPositionTarget", pubInst, new Type[] { typeof(object) }, ModLabel);
			landingStop = ElkReflection.FindMethod(landingType, "StopLanding", pubInst, Type.EmptyTypes, ModLabel);
			nodeExecuteOne = ElkReflection.FindMethod(nodeType, "ExecuteOneNode", pubInst, new Type[] { typeof(object) }, ModLabel);
			nodeAbort = ElkReflection.FindMethod(nodeType, "Abort", pubInst, Type.EmptyTypes, ModLabel);
			menuShowHide = ElkReflection.FindMethod(menuType, "ShowHideWindow", pubInst, Type.EmptyTypes, ModLabel);

			string[] actionNames =
			{
				"OnOrbitProgradeAction", "OnOrbitRetrogradeAction", "OnOrbitNormalAction", "OnOrbitAntinormalAction",
				"OnOrbitRadialInAction", "OnOrbitRadialOutAction", "OnKillRotationAction", "OnDeactivateSmartASSAction",
				"OnLandsomewhereAction", "OnLandTargetAction", "OnPanicAction", "OnTranslatronOffAction",
				"OnTranslatronKeepVertAction", "OnAscentAPToggleAction",
			};
			int actionsFound = 0;
			for (int i = 0; i < actionNames.Length; i++)
			{
				MethodInfo m = ElkReflection.FindMethod(coreType, actionNames[i], pubInst, new Type[] { typeof(KSPActionParam) }, ModLabel);
				actions[actionNames[i]] = m;
				if (m != null)
					actionsFound++;
			}

			ready = coreType != null;
			object[] members =
			{
				getMasterMechJeb, getComputerModuleByName, coreThrust, coreLanding, coreNode, coreTarget, coreAscent, moduleEnabled, displayHidden,
				sassMode, sassTarget, sassEngage, sassModeTarget, sassTargetNode, sassTargetPlus, sassTargetMinus,
				transSetMode, thrustTmode, thrustTransSpdAct, thrustTransKillH, transSpd, editableVal, tmodeOff, tmodeKeepVertical, tmodeKeepSurface, tmodeKeepOrbital,
				targetNormalExists, targetPositionExists, landingLandAtTarget, landingStop, nodeExecuteOne, nodeAbort, menuShowHide,
			};
			int found = 0;
			for (int i = 0; i < members.Length; i++)
			{
				if (members[i] != null)
					found++;
			}
			Debug.Log("[ELK] " + ModLabel + " bridge: " + actionsFound + "/" + actionNames.Length + " actions, "
				+ found + "/" + members.Length + " members resolved, core " + (ready ? "active" : "disabled"));
		}

		private static object ParseEnum(Type enumType, string name)
		{
			if (enumType == null)
				return null;
			try
			{
				return Enum.Parse(enumType, name);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + ModLabel + ": enum value " + enumType.Name + "." + name + " missing: " + e.Message);
				return null;
			}
		}

		// ---- layer 1: the KSPActions, on any MechJebCore part of the active vessel ----

		/// <summary>Any MechJebCore PartModule on this vessel, or null (no MechJeb aboard: deliberate no-op).</summary>
		private static object AnyCore(Vessel vessel)
		{
			Resolve();
			if (!ready || vessel == null)
				return null;
			List<Part> parts = vessel.parts;
			for (int p = 0; p < parts.Count; p++)
			{
				PartModuleList modules = parts[p].Modules;
				for (int m = 0; m < modules.Count; m++)
				{
					PartModule module = modules[m];
					if (module != null && coreType.IsInstanceOfType(module))
						return module;
				}
			}
			if (!warnedNoCore)
			{
				warnedNoCore = true;
				Debug.LogWarning("[ELK] " + ModLabel + ": no MechJeb on the active vessel, hotkey ignored (muted from now on)");
			}
			return null;
		}

		/// <summary>Runs one of MechJebCore's own action-group methods, vessel-wide through MechJeb's master core; with mj_autoshow, opens the window of the module it drives.</summary>
		public static void FireAction(Vessel vessel, string actionName)
		{
			FireAction(vessel, actionName, AutoShow);
		}

		private static void FireAction(Vessel vessel, string actionName, bool show)
		{
			object core = AnyCore(vessel);
			if (core == null)
				return;
			MethodInfo action;
			if (!actions.TryGetValue(actionName, out action) || action == null)
				return;
			try
			{
				action.Invoke(core, new object[] { new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate) });
			}
			catch (Exception e)
			{
				WarnInvoke(actionName, e);
				return;
			}
			if (show)
			{
				Show(VisibleModule(Master(vessel), WindowFor(actionName)));
			}
		}

		// ---- `off` actions for sets_disengage: switch off, never open a window ----

		public static void SmartAssOff(Vessel vessel) { FireAction(vessel, "OnDeactivateSmartASSAction", false); }
		public static void TransOff(Vessel vessel) { FireAction(vessel, "OnTranslatronOffAction", false); }

		/// <summary>Abort the node executor; no-op when it is not running.</summary>
		public static void NodeAbort(Vessel vessel)
		{
			object master = Master(vessel);
			object node = CoreField(master, coreNode);
			if (node == null || nodeAbort == null || !IsEnabled(node))
				return;
			try
			{
				nodeAbort.Invoke(node, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Abort", e);
			}
		}

		/// <summary>Ascent autopilot off if it is running (MechJeb's toggle action, guarded by Enabled so it never switches it on).</summary>
		public static void AscentOff(Vessel vessel)
		{
			object master = Master(vessel);
			if (master == null || coreAscent == null)
				return;
			object ascent;
			try
			{
				ascent = coreAscent.GetValue(master, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Ascent", e);
				return;
			}
			if (ascent == null || !IsEnabled(ascent))
				return;
			FireAction(vessel, "OnAscentAPToggleAction", false);
		}

		/// <summary>The display module whose window shows what an action just did.</summary>
		private static string WindowFor(string actionName)
		{
			if (actionName.StartsWith("OnOrbit") || actionName == "OnKillRotationAction" || actionName == "OnDeactivateSmartASSAction")
				return "MechJebModuleSmartASS";
			if (actionName.StartsWith("OnTranslatron") || actionName == "OnPanicAction")
				return "MechJebModuleTranslatron";
			if (actionName.StartsWith("OnLand"))
				return "MechJebModuleLandingGuidance";
			if (actionName.StartsWith("OnAscent"))
				return "MechJebModuleAscentMenu";
			return "";
		}

		// ---- layer 2: modules of the master core ----

		private static object Master(Vessel vessel)
		{
			Resolve();
			if (!ready || getMasterMechJeb == null || vessel == null)
				return null;
			object master;
			try
			{
				master = getMasterMechJeb.Invoke(null, new object[] { vessel });
			}
			catch (Exception e)
			{
				WarnInvoke("GetMasterMechJeb", e);
				return null;
			}
			if (master == null && !warnedNoCore)
			{
				warnedNoCore = true;
				Debug.LogWarning("[ELK] " + ModLabel + ": no MechJeb on the active vessel, hotkey ignored (muted from now on)");
			}
			return master;
		}

		/// <summary>A display module of the master core by type name, unless it is Hidden (locked in career) - MechJeb's own actions refuse those too.</summary>
		private static object VisibleModule(object master, string typeName)
		{
			if (master == null || getComputerModuleByName == null)
				return null;
			try
			{
				object module = getComputerModuleByName.Invoke(master, new object[] { typeName });
				if (module == null)
					return null;
				if (displayHidden != null && displayHidden.DeclaringType.IsInstanceOfType(module) && (bool)displayHidden.GetValue(module))
					return null;
				return module;
			}
			catch (Exception e)
			{
				WarnInvoke(typeName, e);
				return null;
			}
		}

		private static object CoreField(object master, FieldInfo field)
		{
			if (master == null || field == null)
				return null;
			try
			{
				return field.GetValue(master);
			}
			catch (Exception e)
			{
				WarnInvoke(field.Name, e);
				return null;
			}
		}

		private static bool IsEnabled(object module)
		{
			if (module == null || moduleEnabled == null)
				return false;
			try
			{
				return (bool)moduleEnabled.GetValue(module, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Enabled", e);
				return false;
			}
		}

		/// <summary>mj_autoshow: opens the module's window (DisplayModule.Enabled = true) if it is closed.</summary>
		private static void Show(object module)
		{
			if (!AutoShow || module == null || moduleEnabled == null || IsEnabled(module))
				return;
			try
			{
				moduleEnabled.SetValue(module, true, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Enabled set", e);
			}
		}

		// SmartASS: NODE (top row, any mode) and TARGET_PLUS/MINUS (Mode.TARGET).
		private static void SmartAss(Vessel vessel, object target, bool needTargetMode)
		{
			object master = Master(vessel);
			object sass = VisibleModule(master, "MechJebModuleSmartASS");
			if (sass == null || sassTarget == null || sassEngage == null || target == null)
				return;
			try
			{
				if (needTargetMode)
				{
					if (sassMode == null || sassModeTarget == null)
						return;
					sassMode.SetValue(sass, sassModeTarget);
				}
				sassTarget.SetValue(sass, target);
				sassEngage.Invoke(sass, new object[] { true });
			}
			catch (Exception e)
			{
				WarnInvoke("SmartASS engage", e);
				return;
			}
			Show(sass);
		}

		/// <summary>SmartASS NODE: only with a planned maneuver node (the button MechJeb shows only with patched conics unlocked).</summary>
		public static void SmartAssNode(Vessel vessel)
		{
			if (vessel.patchedConicSolver == null || vessel.patchedConicSolver.maneuverNodes.Count == 0)
				return;
			SmartAss(vessel, sassTargetNode, false);
		}

		private static bool TargetFlag(Vessel vessel, PropertyInfo flag)
		{
			object master = Master(vessel);
			object target = CoreField(master, coreTarget);
			if (target == null || flag == null)
				return false;
			try
			{
				return (bool)flag.GetValue(target, null);
			}
			catch (Exception e)
			{
				WarnInvoke(flag.Name, e);
				return false;
			}
		}

		/// <summary>SmartASS TARGET+/-: only with a vessel or body targeted (Mode.TARGET shows nothing otherwise).</summary>
		public static void SmartAssTargetPlus(Vessel vessel)
		{
			if (TargetFlag(vessel, targetNormalExists))
				SmartAss(vessel, sassTargetPlus, true);
		}

		public static void SmartAssTargetMinus(Vessel vessel)
		{
			if (TargetFlag(vessel, targetNormalExists))
				SmartAss(vessel, sassTargetMinus, true);
		}

		// Translatron
		private static object Translatron(Vessel vessel, out object thrust)
		{
			object master = Master(vessel);
			thrust = CoreField(master, coreThrust);
			return VisibleModule(master, "MechJebModuleTranslatron");
		}

		private static void TransSetMode(object translatron, object mode)
		{
			if (translatron == null || transSetMode == null || mode == null)
				return;
			try
			{
				transSetMode.Invoke(translatron, new object[] { mode });
			}
			catch (Exception e)
			{
				WarnInvoke("Translatron.SetMode", e);
			}
		}

		public static void TransKeepSurface(Vessel vessel)
		{
			object thrust;
			object translatron = Translatron(vessel, out thrust);
			TransSetMode(translatron, tmodeKeepSurface);
			Show(translatron);
		}

		public static void TransKeepOrbital(Vessel vessel)
		{
			object thrust;
			object translatron = Translatron(vessel, out thrust);
			TransSetMode(translatron, tmodeKeepOrbital);
			Show(translatron);
		}

		/// <summary>
		/// What the window's +/0/- buttons do: the Speed box (trans_spd) gets
		/// `delta` added, or `absolute` when `relative` is false, and the box is
		/// then copied into the controller's TransSpdAct. With the Translatron
		/// OFF, mj_autoengage first engages KEEP_VERTICAL (otherwise the change
		/// would be invisible), else no-op.
		/// </summary>
		private static void TransSpeed(Vessel vessel, bool relative, double delta, double absolute)
		{
			object thrust;
			object translatron = Translatron(vessel, out thrust);
			if (translatron == null || thrust == null || thrustTmode == null || thrustTransSpdAct == null
				|| transSpd == null || editableVal == null || tmodeOff == null)
				return;
			try
			{
				object mode = thrustTmode.GetValue(thrust, null);
				if (tmodeOff.Equals(mode))
				{
					if (!AutoEngage)
						return;
					TransSetMode(translatron, tmodeKeepVertical);
				}
				object box = transSpd.GetValue(translatron);
				if (box == null)
					return;
				double value = relative ? (double)editableVal.GetValue(box, null) + delta : absolute;
				editableVal.SetValue(box, value, null);
				thrustTransSpdAct.SetValue(thrust, (float)value);
			}
			catch (Exception e)
			{
				WarnInvoke("trans_spd", e);
				return;
			}
			Show(translatron);
		}

		public static void TransSpeedUp(Vessel vessel) { TransSpeed(vessel, true, TransStep, 0.0); }
		public static void TransSpeedDown(Vessel vessel) { TransSpeed(vessel, true, -TransStep, 0.0); }
		/// <summary>The window's "0" button; not MechJeb's OnTranslatronZeroSpeedAction, which writes TransSpdAct only.</summary>
		public static void TransZero(Vessel vessel) { TransSpeed(vessel, false, 0.0, 0.0); }

		/// <summary>
		/// Kill H/S: with the Translatron running, flips the flag like MechJeb's
		/// own action. With it OFF the flag alone is invisible (it only acts in
		/// KEEP_VERTICAL), so mj_autoengage engages KEEP_VERTICAL and switches
		/// the flag ON (engaging in order to clear it would make no sense);
		/// without the option the flag is just flipped, as MechJeb's action does.
		/// </summary>
		public static void TransKillHToggle(Vessel vessel)
		{
			object thrust;
			object translatron = Translatron(vessel, out thrust);
			if (translatron == null || thrust == null || thrustTmode == null || thrustTransKillH == null || tmodeOff == null)
				return;
			try
			{
				bool on = !(bool)thrustTransKillH.GetValue(thrust);
				if (tmodeOff.Equals(thrustTmode.GetValue(thrust, null)) && AutoEngage)
				{
					TransSetMode(translatron, tmodeKeepVertical);
					on = true;
				}
				thrustTransKillH.SetValue(thrust, on);
			}
			catch (Exception e)
			{
				WarnInvoke("TransKillH", e);
				return;
			}
			Show(translatron);
		}

		// Landing autopilot
		/// <summary>Land at the current position target: same gates as MechJeb's button (a position target set, vessel not landed).</summary>
		public static void LandAtTarget(Vessel vessel)
		{
			if (vessel.LandedOrSplashed || !TargetFlag(vessel, targetPositionExists))
				return;
			object master = Master(vessel);
			object landing = CoreField(master, coreLanding);
			object guidance = VisibleModule(master, "MechJebModuleLandingGuidance");
			if (landing == null || guidance == null || landingLandAtTarget == null)
				return;
			try
			{
				landingLandAtTarget.Invoke(landing, new object[] { guidance });
			}
			catch (Exception e)
			{
				WarnInvoke("LandAtPositionTarget", e);
				return;
			}
			Show(guidance);
		}

		/// <summary>Abort the landing autopilot; no-op when it is not running.</summary>
		public static void LandAbort(Vessel vessel)
		{
			object master = Master(vessel);
			object landing = CoreField(master, coreLanding);
			if (landing == null || landingStop == null || !IsEnabled(landing))
				return;
			try
			{
				landingStop.Invoke(landing, null);
			}
			catch (Exception e)
			{
				WarnInvoke("StopLanding", e);
			}
		}

		// Node executor: what MechJeb's own "Execute Next Node" toolbar button does.
		public static void NodeExecuteToggle(Vessel vessel)
		{
			object master = Master(vessel);
			object node = CoreField(master, coreNode);
			if (node == null || nodeExecuteOne == null || nodeAbort == null)
				return;
			try
			{
				if (IsEnabled(node))
				{
					nodeAbort.Invoke(node, null);
					return;
				}
				if (vessel.patchedConicSolver == null || vessel.patchedConicSolver.maneuverNodes.Count == 0)
					return;
				if (vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude <= 0.0001)
				{
					ScreenMessages.PostScreenMessage("Maneuver burn vector not set", 3f, ScreenMessageStyle.UPPER_CENTER);
					return;
				}
				object planner = VisibleModule(master, "MechJebModuleManeuverPlanner");
				if (planner == null)
					return;
				nodeExecuteOne.Invoke(node, new object[] { planner });
				Show(planner);
			}
			catch (Exception e)
			{
				WarnInvoke("node executor", e);
			}
		}

		/// <summary>Show/hide MechJeb's master menu window.</summary>
		public static void MenuToggle(Vessel vessel)
		{
			object master = Master(vessel);
			object menu = VisibleModule(master, "MechJebModuleMenu");
			if (menu == null || menuShowHide == null)
				return;
			try
			{
				menuShowHide.Invoke(menu, null);
			}
			catch (Exception e)
			{
				WarnInvoke("ShowHideWindow", e);
			}
		}

		/// <summary>Native MechJeb hotkey (MechJebCore.Update): Ctrl+V pastes a custom window from the clipboard.</summary>
		public static List<string> DescribeConflicts(ElkBind candidate)
		{
			List<string> hits = new List<string>();
			if (candidate != null && candidate.primary == KeyCode.V)
				hits.Add("MechJeb2 'paste custom window' (Ctrl+V)");
			return hits;
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
