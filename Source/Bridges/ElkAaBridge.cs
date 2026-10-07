// AtmosphereAutopilot (AA) reached purely by reflection: ELK never
// references AtmosphereAutopilot.dll (GPL-3) and copies none of its code;
// a missing or reshaped AA only turns the affected slots into no-ops.
// Every member below is verified against the AA 1.6.1 source; the ones
// marked "fork" exist as hotkeys only in Rjoande's GA fork, but the state
// they act on is upstream, so ELK offers those actions on official AA too.
//
//   AtmosphereAutopilot.AtmosphereAutopilot
//       public static Instance; public getVesselModules(Vessel) -> Dictionary<Type, AutopilotModule>;
//       public mainMenuGUIUpdate() - AA calls it after every hotkey-driven state change
//   AtmosphereAutopilot.AutopilotModule : GUIWindow
//       public bool Active { get; set; }  (setter = Activate()/Deactivate())
//   AtmosphereAutopilot.GUIWindow
//       public bool ToggleGUI(); public virtual void ShowGUI(); public bool IsShown()
//   AtmosphereAutopilot.TopModuleManager
//       public StateController activateAutopilot(Type)  (switches master on too)
//   AtmosphereAutopilot.CruiseController
//       internal bool LevelFlightMode / WaypointMode, private bool CourseHoldMode (setters
//       with side effects, CourseHold refuses beyond 80 deg latitude);
//       public bool vertical_control; public HeightMode height_mode (nested enum
//       Altitude, VerticalSpeed, FlightPathAngle); private HeightMode
//       prev_height_change_mode_by_hotkey; public DelayedFieldFloat desired_course,
//       desired_altitude (ASL, compared with vessel.altitude), desired_vertsetpoint;
//       public static bool use_keys
//   AtmosphereAutopilot.DelayedFieldFloat: public float Value { get; set; }
//   AtmosphereAutopilot.StandardFlyByWire: public bool moderation_switch, RocketMode,
//       Coord_turn (properties whose setters post the status message)
//   AtmosphereAutopilot.ProgradeThrustController: public bool spd_control_enabled
//   AtmosphereAutopilot.FlightModel (partial, EngineBalancing.cs): public bool balance_engines
//   AtmosphereAutopilot.MessageManager: public static post_status_message(string)
//
// AA itself only runs a module's own hotkeys while that module is Active
// (AtmosphereAutopilot.Update); ELK goes one step further with
// aa_autoengage: a Cruise/FBW key pressed while that controller is off
// switches master and controller on first (activateAutopilot), the same
// spirit as sas_autoengage for the stock SAS keys.
//
// A vessel's controller instances only exist once its master has been
// switched on at least once (TopModuleManager.OnActivate -> create_context,
// decompiled 1.6.1); until then getVesselModules holds the manager alone.
// activateAutopilot(Type) sets Active first, so it creates them itself.
//
// AtmosphereApproach (AAPR, Rjoande's APR add-on, GPL-3): an AA
// StateController living in AtmosphereApproach.dll, reached the same way
// and only when that DLL is loaded. Members verified against AAPR 0.1:
//
//   AtmosphereApproach.ApproachController : StateController
//       public bool Armed { get; set; } - the window's APR toggle and the
//       native hotkey both set it; the setter refuses to arm without a
//       NavInstruments runway tuned on the current body and posts its own
//       status messages.

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkAaBridge
	{
		private const string ModLabel = "AtmosphereAutopilot";
		private const string DllName = "AtmosphereAutopilot";
		private const string Ns = "AtmosphereAutopilot.";

		public const string OptAutoEngage = "aa_autoengage";
		public const string OptAutoShow = "aa_autoshow";
		public const string OptVsStep = "aa_vs_step";
		public const string OptAltStep = "aa_alt_step";
		public const string OptHdgStep = "aa_hdg_step";

		private static bool installChecked;
		private static bool installed;
		private static Assembly assembly;

		private static bool resolved;
		private static bool ready;

		private static Type cruiseType;
		private static Type fbwType;
		private static Type ptcType;
		private static Type flightModelType;
		private static Type topType;

		private static PropertyInfo instanceProp;        // AtmosphereAutopilot.Instance
		private static MethodInfo getVesselModules;      // Instance.getVesselModules(Vessel)
		private static MethodInfo mainMenuGuiUpdate;     // Instance.mainMenuGUIUpdate()
		private static PropertyInfo activeProp;          // AutopilotModule.Active
		private static MethodInfo showGui;               // GUIWindow.ShowGUI()
		private static MethodInfo toggleGui;             // GUIWindow.ToggleGUI()
		private static MethodInfo activateAutopilot;     // TopModuleManager.activateAutopilot(Type)

		private static PropertyInfo levelFlightMode;
		private static PropertyInfo courseHoldMode;
		private static PropertyInfo waypointMode;
		private static FieldInfo verticalControl;
		private static FieldInfo heightMode;
		private static FieldInfo prevHeightMode;
		private static FieldInfo desiredCourse;
		private static FieldInfo desiredAltitude;
		private static FieldInfo desiredVertSetpoint;
		private static FieldInfo useKeys;
		private static PropertyInfo delayedValue;         // DelayedFieldFloat.Value
		private static object hmAltitude;
		private static object hmVerticalSpeed;
		private static object hmFlightPathAngle;

		private static PropertyInfo fbwModeration;
		private static PropertyInfo fbwRocketMode;
		private static PropertyInfo fbwCoordTurn;
		private static FieldInfo ptcSpeedControl;
		private static FieldInfo fmBalanceEngines;
		private static MethodInfo postStatusMessage;

		private const string AprDllName = "AtmosphereApproach";
		private const string AprControllerTypeName = "AtmosphereApproach.ApproachController";

		private static bool aprChecked;
		private static bool aprInstalled;
		private static Assembly aprAssembly;
		private static Type aprType;
		private static PropertyInfo aprArmed;            // ApproachController.Armed

		private static bool warnedNoModules;
		private static bool warnedNoController;
		private static bool warnedInvoke;

		public static Assembly Assembly
		{
			get
			{
				IsInstalled();
				return assembly;
			}
		}

		/// <summary>True if AtmosphereAutopilot.dll is loaded. Cheap after the first call; drives the window tab and the cfg creation.</summary>
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

		/// <summary>AtmosphereApproach's assembly when loaded (it cannot load without AA), else null. ElkAaKeys scans it for AAPR's native hotkey.</summary>
		public static Assembly ApproachAssembly
		{
			get
			{
				IsApproachInstalled();
				return aprAssembly;
			}
		}

		/// <summary>True if AtmosphereApproach.dll is loaded: shows the APR row in the AA tab and gates its slot.</summary>
		public static bool IsApproachInstalled()
		{
			if (!aprChecked)
			{
				aprChecked = true;
				aprAssembly = ElkReflection.FindAssembly(AprDllName);
				aprInstalled = aprAssembly != null;
			}
			return aprInstalled;
		}

		// ---- options (read live from the AA cfg through ElkConfig) ----

		public static bool AutoEngage
		{
			get { return ElkConfig.GetBool(ElkGroups.AtmosphereAutopilot, OptAutoEngage, true); }
		}

		public static bool AutoShow
		{
			get { return ElkConfig.GetBool(ElkGroups.AtmosphereAutopilot, OptAutoShow, false); }
		}

		public static float VsStep
		{
			get { return ElkConfig.GetFloat(ElkGroups.AtmosphereAutopilot, OptVsStep, 1f); }
		}

		public static float AltStep
		{
			get { return ElkConfig.GetFloat(ElkGroups.AtmosphereAutopilot, OptAltStep, 50f); }
		}

		public static float HdgStep
		{
			get { return ElkConfig.GetFloat(ElkGroups.AtmosphereAutopilot, OptHdgStep, 1f); }
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
			const BindingFlags anyInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

			Type mainType = ElkReflection.FindType(assembly, Ns + "AtmosphereAutopilot", ModLabel);
			Type moduleType = ElkReflection.FindType(assembly, Ns + "AutopilotModule", ModLabel);
			Type guiWindowType = ElkReflection.FindType(assembly, Ns + "GUIWindow", ModLabel);
			Type delayedType = ElkReflection.FindType(assembly, Ns + "DelayedFieldFloat", ModLabel);
			Type messageType = ElkReflection.FindType(assembly, Ns + "MessageManager", ModLabel);
			topType = ElkReflection.FindType(assembly, Ns + "TopModuleManager", ModLabel);
			cruiseType = ElkReflection.FindType(assembly, Ns + "CruiseController", ModLabel);
			fbwType = ElkReflection.FindType(assembly, Ns + "StandardFlyByWire", ModLabel);
			ptcType = ElkReflection.FindType(assembly, Ns + "ProgradeThrustController", ModLabel);
			flightModelType = ElkReflection.FindType(assembly, Ns + "FlightModel", ModLabel);

			instanceProp = ElkReflection.FindProperty(mainType, "Instance", BindingFlags.Public | BindingFlags.Static, ModLabel);
			getVesselModules = ElkReflection.FindMethod(mainType, "getVesselModules", pubInst, new Type[] { typeof(Vessel) }, ModLabel);
			mainMenuGuiUpdate = ElkReflection.FindMethod(mainType, "mainMenuGUIUpdate", pubInst, Type.EmptyTypes, ModLabel);
			activeProp = ElkReflection.FindProperty(moduleType, "Active", pubInst, ModLabel);
			showGui = ElkReflection.FindMethod(guiWindowType, "ShowGUI", pubInst, Type.EmptyTypes, ModLabel);
			toggleGui = ElkReflection.FindMethod(guiWindowType, "ToggleGUI", pubInst, Type.EmptyTypes, ModLabel);
			activateAutopilot = ElkReflection.FindMethod(topType, "activateAutopilot", pubInst, new Type[] { typeof(Type) }, ModLabel);

			levelFlightMode = ElkReflection.FindProperty(cruiseType, "LevelFlightMode", anyInst, ModLabel);
			courseHoldMode = ElkReflection.FindProperty(cruiseType, "CourseHoldMode", anyInst, ModLabel);
			waypointMode = ElkReflection.FindProperty(cruiseType, "WaypointMode", anyInst, ModLabel);
			verticalControl = ElkReflection.FindField(cruiseType, "vertical_control", pubInst, ModLabel);
			heightMode = ElkReflection.FindField(cruiseType, "height_mode", pubInst, ModLabel);
			prevHeightMode = ElkReflection.FindField(cruiseType, "prev_height_change_mode_by_hotkey", anyInst, ModLabel);
			desiredCourse = ElkReflection.FindField(cruiseType, "desired_course", pubInst, ModLabel);
			desiredAltitude = ElkReflection.FindField(cruiseType, "desired_altitude", pubInst, ModLabel);
			desiredVertSetpoint = ElkReflection.FindField(cruiseType, "desired_vertsetpoint", pubInst, ModLabel);
			useKeys = ElkReflection.FindField(cruiseType, "use_keys", BindingFlags.Public | BindingFlags.Static, ModLabel);
			delayedValue = ElkReflection.FindProperty(delayedType, "Value", pubInst, ModLabel);
			Type heightModeType = ElkReflection.FindNestedType(cruiseType, "HeightMode", ModLabel);
			if (heightModeType != null)
			{
				hmAltitude = ParseEnum(heightModeType, "Altitude");
				hmVerticalSpeed = ParseEnum(heightModeType, "VerticalSpeed");
				hmFlightPathAngle = ParseEnum(heightModeType, "FlightPathAngle");
			}

			fbwModeration = ElkReflection.FindProperty(fbwType, "moderation_switch", pubInst, ModLabel);
			fbwRocketMode = ElkReflection.FindProperty(fbwType, "RocketMode", pubInst, ModLabel);
			fbwCoordTurn = ElkReflection.FindProperty(fbwType, "Coord_turn", pubInst, ModLabel);
			ptcSpeedControl = ElkReflection.FindField(ptcType, "spd_control_enabled", pubInst, ModLabel);
			fmBalanceEngines = ElkReflection.FindField(flightModelType, "balance_engines", pubInst, ModLabel);
			postStatusMessage = ElkReflection.FindMethod(messageType, "post_status_message", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(string) }, ModLabel);

			ready = instanceProp != null && getVesselModules != null && activeProp != null && topType != null;
			object[] members =
			{
				instanceProp, getVesselModules, mainMenuGuiUpdate, activeProp, showGui, toggleGui, activateAutopilot,
				levelFlightMode, courseHoldMode, waypointMode, verticalControl, heightMode, prevHeightMode,
				desiredCourse, desiredAltitude, desiredVertSetpoint, useKeys, delayedValue, hmAltitude, hmVerticalSpeed, hmFlightPathAngle,
				fbwModeration, fbwRocketMode, fbwCoordTurn, ptcSpeedControl, fmBalanceEngines, postStatusMessage,
				topType, cruiseType, fbwType, ptcType, flightModelType,
			};
			int found = 0;
			for (int i = 0; i < members.Length; i++)
			{
				if (members[i] != null)
					found++;
			}
			Debug.Log("[ELK] " + ModLabel + " bridge: " + found + "/" + members.Length + " members resolved, core " + (ready ? "active" : "disabled"));

			if (IsApproachInstalled())
			{
				aprType = ElkReflection.FindType(aprAssembly, AprControllerTypeName, AprDllName);
				aprArmed = ElkReflection.FindProperty(aprType, "Armed", pubInst, AprDllName);
				int aprFound = (aprType != null ? 1 : 0) + (aprArmed != null ? 1 : 0);
				Debug.Log("[ELK] " + AprDllName + " bridge: " + aprFound + "/2 members resolved, APR hotkey " + (aprArmed != null ? "active" : "disabled"));
			}
		}

		private static object ParseEnum(Type enumType, string name)
		{
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

		// ---- instance access ----

		private static object MainInstance()
		{
			try
			{
				return instanceProp.GetValue(null, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Instance", e);
				return null;
			}
		}

		/// <summary>AA's module of the given type for this vessel, or null (AA not ready, vessel unknown to AA, type unresolved).</summary>
		private static object Module(Vessel vessel, Type type)
		{
			Resolve();
			if (!ready || type == null)
				return null;
			object main = MainInstance();
			if (main == null)
				return null;
			IDictionary modules;
			try
			{
				modules = getVesselModules.Invoke(main, new object[] { vessel }) as IDictionary;
			}
			catch (Exception e)
			{
				WarnInvoke("getVesselModules", e);
				return null;
			}
			if (modules == null)
			{
				// AA creates a vessel's module map on vessel switch; a
				// vessel it has never seen yet has none: deliberate no-op.
				if (!warnedNoModules)
				{
					warnedNoModules = true;
					Debug.LogWarning("[ELK] " + ModLabel + ": no module map for the active vessel yet, hotkey ignored");
				}
				return null;
			}
			return modules.Contains(type) ? modules[type] : null;
		}

		private static bool IsActive(object module)
		{
			try
			{
				return (bool)activeProp.GetValue(module, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Active", e);
				return false;
			}
		}

		private static void RefreshMainMenu()
		{
			if (mainMenuGuiUpdate == null)
				return;
			object main = MainInstance();
			if (main == null)
				return;
			try
			{
				mainMenuGuiUpdate.Invoke(main, null);
			}
			catch (Exception e)
			{
				WarnInvoke("mainMenuGUIUpdate", e);
			}
		}

		private static void Show(object window)
		{
			if (window == null || showGui == null)
				return;
			try
			{
				showGui.Invoke(window, null);
			}
			catch (Exception e)
			{
				WarnInvoke("ShowGUI", e);
			}
		}

		private static void Message(string text)
		{
			if (postStatusMessage == null)
				return;
			try
			{
				postStatusMessage.Invoke(null, new object[] { text });
			}
			catch (Exception e)
			{
				WarnInvoke("post_status_message", e);
			}
		}

		/// <summary>activateAutopilot(type): switches master on and makes that controller the active one. True on success.</summary>
		private static bool Activate(Vessel vessel, Type controllerType)
		{
			object top = Module(vessel, topType);
			if (top == null || activateAutopilot == null || controllerType == null)
				return false;
			try
			{
				activateAutopilot.Invoke(top, new object[] { controllerType });
			}
			catch (Exception e)
			{
				WarnInvoke("activateAutopilot", e);
				return false;
			}
			RefreshMainMenu();
			if (AutoShow)
			{
				Show(top);
				Show(Module(vessel, controllerType));
			}
			return true;
		}

		/// <summary>
		/// The controller a slot needs, activating it first when
		/// aa_autoengage allows; null = no-op (AA absent, vessel unknown,
		/// controller off with auto-engage disabled).
		/// </summary>
		private static object Engaged(Vessel vessel, Type controllerType)
		{
			bool switchedOn;
			return Engaged(vessel, controllerType, out switchedOn);
		}

		/// <summary>switchedOn: this press switched the controller on (aa_autoengage), so a toggle slot forces its "on" state instead of flipping whatever AA had saved.</summary>
		private static object Engaged(Vessel vessel, Type controllerType, out bool switchedOn)
		{
			switchedOn = false;
			object controller = Module(vessel, controllerType);
			if (controller != null && IsActive(controller))
				return controller;
			if (!AutoEngage)
				return null;
			// A null controller on a vessel AA knows means its master has
			// never been on: activateAutopilot builds the modules, so
			// look the controller up again afterwards.
			if (!Activate(vessel, controllerType))
				return null;
			switchedOn = true;
			return (controller != null) ? controller : Module(vessel, controllerType);
		}

		/// <summary>
		/// A module instance whether active or not, for slots that only set a
		/// value or a flag and never switch anything on. Null, logged once, on
		/// a vessel whose master has never been on (AA has not built it yet).
		/// </summary>
		private static object Existing(Vessel vessel, Type type, string what)
		{
			object module = Module(vessel, type);
			if (module == null && !warnedNoController && Module(vessel, topType) != null)
			{
				warnedNoController = true;
				Debug.Log("[ELK] " + ModLabel + ": " + what + " not created yet (master never switched on for this vessel), hotkey ignored");
			}
			return module;
		}

		// ---- slot actions: AtmosphereApproach ----

		/// <summary>
		/// APR arm/disarm, what AAPR's own hotkey does: select the Approach
		/// controller first if another autopilot (or none) is flying, then
		/// flip Armed. Deliberately ignores aa_autoengage, like the FD and
		/// select keys; the Armed setter applies AAPR's own runway gate.
		/// </summary>
		public static void ApproachArmToggle(Vessel vessel)
		{
			Resolve();
			if (!ready || aprType == null || aprArmed == null)
				return;
			object apr = Module(vessel, aprType);
			if (apr == null || !IsActive(apr))
			{
				if (!Activate(vessel, aprType))
					return;
				apr = Module(vessel, aprType);
				if (apr == null)
					return;
			}
			try
			{
				bool armed = (bool)aprArmed.GetValue(apr, null);
				aprArmed.SetValue(apr, !armed, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Armed", e);
			}
		}

		// ---- slot actions: manager ----

		public static void ToggleMaster(Vessel vessel)
		{
			object top = Module(vessel, topType);
			if (top == null)
				return;
			bool wasActive = IsActive(top);
			try
			{
				activeProp.SetValue(top, !wasActive, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Active set", e);
				return;
			}
			RefreshMainMenu();
			if (!wasActive && AutoShow)
				Show(top);
		}

		/// <summary>sets_disengage: master off, which stops every AA controller; no-op if already off.</summary>
		public static void MasterOff(Vessel vessel)
		{
			object top = Module(vessel, topType);
			if (top == null || !IsActive(top))
				return;
			try
			{
				activeProp.SetValue(top, false, null);
			}
			catch (Exception e)
			{
				WarnInvoke("Active set", e);
				return;
			}
			RefreshMainMenu();
		}

		public static void ToggleGui(Vessel vessel)
		{
			object top = Module(vessel, topType);
			if (top == null || toggleGui == null)
				return;
			try
			{
				toggleGui.Invoke(top, null);
			}
			catch (Exception e)
			{
				WarnInvoke("ToggleGUI", e);
			}
		}

		public static void SelectFbw(Vessel vessel)
		{
			Activate(vessel, fbwType);
		}

		public static void SelectCruise(Vessel vessel)
		{
			Activate(vessel, cruiseType);
		}

		/// <summary>
		/// FD key: with the master on, Cruise Flight <-> Standard FBW; with the
		/// master off (or never on), always Standard FBW, AA's own default
		/// controller on a fresh vessel.
		/// </summary>
		public static void ToggleFd(Vessel vessel)
		{
			object top = Module(vessel, topType);
			if (top == null)
				return;
			object cruise = Module(vessel, cruiseType);
			bool toCruise = IsActive(top) && cruise != null && !IsActive(cruise);
			Activate(vessel, toCruise ? cruiseType : fbwType);
		}

		// ---- slot actions: Cruise Flight ----

		private static void SetCruiseMode(Vessel vessel, PropertyInfo modeProp, string message)
		{
			object cruise = Engaged(vessel, cruiseType);
			if (cruise == null || modeProp == null)
				return;
			try
			{
				modeProp.SetValue(cruise, true, null);
			}
			catch (Exception e)
			{
				WarnInvoke(modeProp.Name, e);
				return;
			}
			Message(message);
			RefreshMainMenu();
		}

		public static void CruiseLevel(Vessel vessel)
		{
			SetCruiseMode(vessel, levelFlightMode, "Level flight mode selected");
		}

		public static void CruiseHeading(Vessel vessel)
		{
			// CourseHoldMode's setter silently refuses beyond 80 deg of
			// latitude (AA's own rule); the message still shows, as in AA.
			SetCruiseMode(vessel, courseHoldMode, "Heading hold mode selected");
		}

		public static void CruiseWaypoint(Vessel vessel)
		{
			SetCruiseMode(vessel, waypointMode, "Waypoint tracking mode selected");
		}

		/// <summary>Fork's V/S and ALT keys: engage vertical control in that mode, or disengage it if already in that mode.</summary>
		private static void CruiseHeightToggle(Vessel vessel, object mode, string engaged, string disengaged)
		{
			object cruise = Engaged(vessel, cruiseType);
			if (cruise == null || verticalControl == null || heightMode == null || mode == null)
				return;
			try
			{
				bool vertical = (bool)verticalControl.GetValue(cruise);
				object current = heightMode.GetValue(cruise);
				if (vertical && mode.Equals(current))
				{
					verticalControl.SetValue(cruise, false);
					Message(disengaged);
				}
				else
				{
					verticalControl.SetValue(cruise, true);
					heightMode.SetValue(cruise, mode);
					Message(engaged);
				}
			}
			catch (Exception e)
			{
				WarnInvoke("height toggle", e);
			}
		}

		public static void CruiseVsToggle(Vessel vessel)
		{
			CruiseHeightToggle(vessel, hmVerticalSpeed, "Vertical speed control engaged", "Vertical speed control disengaged");
		}

		public static void CruiseAltToggle(Vessel vessel)
		{
			CruiseHeightToggle(vessel, hmAltitude, "Altitude hold engaged", "Altitude hold disengaged");
		}

		public static void CruiseVerticalToggle(Vessel vessel)
		{
			bool switchedOn;
			object cruise = Engaged(vessel, cruiseType, out switchedOn);
			if (cruise == null || verticalControl == null)
				return;
			try
			{
				// Just switched on by this press: the key means "vertical
				// motion on", not "flip whatever AA had saved".
				bool vertical = switchedOn || !(bool)verticalControl.GetValue(cruise);
				verticalControl.SetValue(cruise, vertical);
				Message(vertical ? "Vertical motion control enabled" : "Vertical motion control disabled");
			}
			catch (Exception e)
			{
				WarnInvoke("vertical_control", e);
			}
		}

		/// <summary>
		/// AA's upstream "altitude / vertical speed" key: Altitude goes back
		/// to the last changing mode used (V/S or FPA, remembered by AA in
		/// prev_height_change_mode_by_hotkey), V/S or FPA go to Altitude.
		/// Without that private field the fallback is plain Altitude <-> V/S.
		/// </summary>
		public static void CruiseSetpointTypeToggle(Vessel vessel)
		{
			object cruise = Engaged(vessel, cruiseType);
			if (cruise == null || heightMode == null || hmAltitude == null || hmVerticalSpeed == null)
				return;
			try
			{
				object current = heightMode.GetValue(cruise);
				object next;
				if (hmAltitude.Equals(current))
				{
					object prev = (prevHeightMode != null) ? prevHeightMode.GetValue(cruise) : null;
					next = (prev != null && (hmVerticalSpeed.Equals(prev) || (hmFlightPathAngle != null && hmFlightPathAngle.Equals(prev))))
						? prev : hmVerticalSpeed;
				}
				else
				{
					if (prevHeightMode != null)
						prevHeightMode.SetValue(cruise, current);
					next = hmAltitude;
				}
				heightMode.SetValue(cruise, next);
				if (hmAltitude.Equals(next))
					Message("Altitude control");
				else if (hmVerticalSpeed.Equals(next))
					Message("Vertical speed control");
				else
					Message("Flight path angle control");
			}
			catch (Exception e)
			{
				WarnInvoke("height_mode toggle", e);
			}
		}

		/// <summary>use_keys is a static AA preference: flipped as is, no controller involved, nothing switched on.</summary>
		public static void CruiseKeysModeToggle(Vessel vessel)
		{
			Resolve();
			if (!ready || useKeys == null)
				return;
			try
			{
				bool on = !(bool)useKeys.GetValue(null);
				useKeys.SetValue(null, on);
				Message(on ? "CF key input mode enabled" : "CF key input mode disabled");
			}
			catch (Exception e)
			{
				WarnInvoke("use_keys", e);
			}
		}

		private static bool ReadBug(object cruise, FieldInfo bugField, out object bug, out float value)
		{
			bug = null;
			value = 0f;
			if (bugField == null || delayedValue == null)
				return false;
			try
			{
				bug = bugField.GetValue(cruise);
				if (bug == null)
					return false;
				value = (float)delayedValue.GetValue(bug, null);
				return true;
			}
			catch (Exception e)
			{
				WarnInvoke(bugField.Name + " read", e);
				return false;
			}
		}

		private static void WriteBug(object bug, float value, string fieldName)
		{
			try
			{
				delayedValue.SetValue(bug, value, null);
			}
			catch (Exception e)
			{
				WarnInvoke(fieldName + " write", e);
			}
		}

		/// <summary>Bug encoders never switch anything on: the bug is preset with AA off or FBW flying, and used once Cruise Flight is.</summary>
		private static void CruiseBug(Vessel vessel, FieldInfo bugField, float delta, bool wrapCourse)
		{
			object cruise = Existing(vessel, cruiseType, "Cruise Flight");
			object bug;
			float value;
			if (cruise == null || !ReadBug(cruise, bugField, out bug, out value))
				return;
			value += delta;
			if (wrapCourse)
			{
				if (value > 360f)
					value -= 360f;
				if (value < 0f)
					value += 360f;
			}
			WriteBug(bug, value, bugField.Name);
		}

		public static void CruiseVsUp(Vessel vessel) { CruiseBug(vessel, desiredVertSetpoint, VsStep, false); }
		public static void CruiseVsDown(Vessel vessel) { CruiseBug(vessel, desiredVertSetpoint, -VsStep, false); }
		public static void CruiseAltUp(Vessel vessel) { CruiseBug(vessel, desiredAltitude, AltStep, false); }
		public static void CruiseAltDown(Vessel vessel) { CruiseBug(vessel, desiredAltitude, -AltStep, false); }
		public static void CruiseHdgUp(Vessel vessel) { CruiseBug(vessel, desiredCourse, HdgStep, true); }
		public static void CruiseHdgDown(Vessel vessel) { CruiseBug(vessel, desiredCourse, -HdgStep, true); }

		/// <summary>
		/// ALT bug = current altitude ASL (what AA's altitude hold compares
		/// against), then altitude hold engaged (never disengaged: unlike
		/// the ALT toggle, "sync" always means "hold this"). The plain bug
		/// encoders only move the bug.
		/// </summary>
		public static void CruiseAltSync(Vessel vessel)
		{
			object cruise = Engaged(vessel, cruiseType);
			object bug;
			float value;
			if (cruise == null || !ReadBug(cruise, desiredAltitude, out bug, out value))
				return;
			WriteBug(bug, (float)vessel.altitude, "desired_altitude");
			if (verticalControl != null && heightMode != null && hmAltitude != null)
			{
				try
				{
					verticalControl.SetValue(cruise, true);
					heightMode.SetValue(cruise, hmAltitude);
				}
				catch (Exception e)
				{
					WarnInvoke("ALT sync engage", e);
				}
			}
			Message("Altitude hold engaged at current altitude");
		}

		/// <summary>HDG bug = current compass heading of the active vessel, then heading hold selected (same as the HDG mode key).</summary>
		public static void CruiseHdgSync(Vessel vessel)
		{
			object cruise = Engaged(vessel, cruiseType);
			object bug;
			float value;
			if (cruise == null || !ReadBug(cruise, desiredCourse, out bug, out value))
				return;
			float heading = (float)FlightGlobals.ship_heading;
			if (heading < 0f)
				heading += 360f;
			if (heading >= 360f)
				heading -= 360f;
			WriteBug(bug, heading, "desired_course");
			if (courseHoldMode != null)
			{
				try
				{
					courseHoldMode.SetValue(cruise, true, null);
				}
				catch (Exception e)
				{
					WarnInvoke("HDG sync engage", e);
				}
			}
			Message("Heading hold engaged on current heading");
		}

		// ---- slot actions: Standard Fly-By-Wire ----

		private static void FbwToggle(Vessel vessel, PropertyInfo prop)
		{
			bool switchedOn;
			object fbw = Engaged(vessel, fbwType, out switchedOn);
			if (fbw == null || prop == null)
				return;
			try
			{
				// The setters post AA's own "enabled/disabled" message. A
				// press that switched FBW on forces the flag on rather than
				// flipping the saved value.
				bool on = switchedOn || !(bool)prop.GetValue(fbw, null);
				prop.SetValue(fbw, on, null);
			}
			catch (Exception e)
			{
				WarnInvoke(prop.Name, e);
				return;
			}
			RefreshMainMenu();
		}

		public static void FbwModeration(Vessel vessel) { FbwToggle(vessel, fbwModeration); }
		public static void FbwRocketMode(Vessel vessel) { FbwToggle(vessel, fbwRocketMode); }
		public static void FbwCoordTurn(Vessel vessel) { FbwToggle(vessel, fbwCoordTurn); }

		// ---- slot actions: other modules ----

		/// <summary>Speed control flag of the thrust controller; only has an effect while a controller that uses it is active (AA's own rule).</summary>
		public static void SpeedControlToggle(Vessel vessel)
		{
			object ptc = Existing(vessel, ptcType, "Prograde thrust controller");
			if (ptc == null || ptcSpeedControl == null)
				return;
			try
			{
				bool on = !(bool)ptcSpeedControl.GetValue(ptc);
				ptcSpeedControl.SetValue(ptc, on);
				Message(on ? "Speed control enabled" : "Speed control disabled");
			}
			catch (Exception e)
			{
				WarnInvoke("spd_control_enabled", e);
			}
		}

		public static void ThrustBalancingToggle(Vessel vessel)
		{
			object fm = Existing(vessel, flightModelType, "Flight model");
			if (fm == null || fmBalanceEngines == null)
				return;
			try
			{
				bool on = !(bool)fmBalanceEngines.GetValue(fm);
				fmBalanceEngines.SetValue(fm, on);
				Message(on ? "Thrust balancing enabled" : "Thrust balancing disabled");
			}
			catch (Exception e)
			{
				WarnInvoke("balance_engines", e);
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
