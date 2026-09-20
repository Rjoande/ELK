// Ported from KRILL's KrillToolbarApp.cs registration pattern
// (github.com/Rjoande/KRILL, same author, MIT), simplified to a single
// scene: ELK's hotkeys are global player bindings with nothing vessel- or
// part-specific about them, so unlike KRILL (VAB/SPH/Flight/MapView) there
// is no reason to show the button anywhere but the Space Center — avoids
// cluttering scenes where it would add nothing. No per-scene subclass split
// needed either, for the same reason (KRILL needs one only because it spans
// several distinct KSPAddon startup enums).
//
// AddToAllToolbars' 8-arg overload and TC_ClickHandler's signature
// (public delegate void TC_ClickHandler();) confirmed against the locally
// installed GameData/001_ToolbarControl/Plugins/ToolbarControl.dll via
// ilspycmd.

using KSP.UI.Screens;
using ToolbarControl_NS;
using UnityEngine;

namespace ELK
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class ElkToolbarRegistration : MonoBehaviour
	{
		public void Start()
		{
			ToolbarControl.RegisterMod(ElkToolbarApp.MODID, ElkToolbarApp.MODNAME);
		}
	}

	[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
	public class ElkToolbarApp : MonoBehaviour
	{
		internal const string MODID = "ELK_NS";
		internal const string MODNAME = "ELK";

		private ToolbarControl toolbarControl;

		public void Awake()
		{
			ElkConfig.Load();
		}

		public void Start()
		{
			if (!ElkConfig.ToolbarEnabled)
				return;

			toolbarControl = gameObject.AddComponent<ToolbarControl>();
			toolbarControl.AddToAllToolbars(
				ElkWindow.Open, ElkWindow.Close,
				ApplicationLauncher.AppScenes.SPACECENTER,
				MODID, "ElkButton",
				"ELK/Textures/ELK_38",
				"ELK/Textures/ELK_24",
				MODNAME);
			ElkWindow.OnClosed = OnWindowClosed;
		}

		private void OnWindowClosed()
		{
			if (toolbarControl != null)
			{
				toolbarControl.SetFalse(false);
			}
		}

		public void Update()
		{
			// Drives ElkCapture's "press it now" scan while the toolbar
			// window has one pending — the only driver ELK needs, since the
			// window only ever exists in this one scene.
			if (ElkCapture.NeedsTick)
			{
				ElkCapture.Tick();
			}
		}

		public void OnDestroy()
		{
			ElkCapture.ForceCancel();
			ElkWindow.OnClosed = null;
			if (toolbarControl != null)
			{
				toolbarControl.OnDestroy();
				Destroy(toolbarControl);
			}
		}
	}
}
