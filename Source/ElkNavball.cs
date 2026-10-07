// Reopens the collapsed navball when a hotkey engages something shown on it;
// stock never does (only NavBallToggle moves the panel). Verified on KSP 1.12.5.

using KSP.UI.Screens.Flight;

namespace ELK
{
	public static class ElkNavball
	{
		/// <summary>Expands the navball if it is collapsed, in flight or in map view; a no-op when it is already up or mid-transition.</summary>
		public static void Show()
		{
			NavBallToggle toggle = NavBallToggle.Instance;
			if (toggle == null || toggle.panel == null || !toggle.panel.collapsed)
				return;

			toggle.panel.Expand();

			// In map view the navball button also flips Maneuver Mode, which relaxes
			// the map input lock; expanding the panel alone would leave controls
			// locked. Outside map view Maneuver Mode does not exist.
			if (MapView.MapIsEnabled && !toggle.ManeuverModeActive)
			{
				toggle.OnNavBallToggle();
			}
		}
	}
}
