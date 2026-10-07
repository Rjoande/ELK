// Brings the navball back when a hotkey engages something the player then
// needs to see on it (stock SAS vectors, optionally MechJeb's SmartASS).
// Stock never does this by itself: nothing in the SAS or autopilot code
// touches the navball panel, only NavBallToggle does (its own button, the
// NAVBALL_TOGGLE key, and entering/leaving map view). Verified against KSP
// 1.12.5 Assembly-CSharp.dll, decompiled.

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

			// In map view the navball's own button does two things: it moves
			// the panel and it flips "Maneuver Mode", which is what shrinks
			// the map's input lock so pitch/yaw/roll/throttle and SAS work
			// again. Expanding the panel alone would leave the navball up
			// with the controls still locked. Outside map view Maneuver Mode
			// does not exist (EnableManeuverMode returns at once).
			if (MapView.MapIsEnabled && !toggle.ManeuverModeActive)
			{
				toggle.OnNavBallToggle();
			}
		}
	}
}
