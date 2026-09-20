// Ported from KRILL's KrillCapture.cs (github.com/Rjoande/KRILL, same
// author, MIT), including the Delete-to-clear behavior designed there but
// not yet merged at the time of this port (2026-09): pressing Delete during
// a capture clears the bind instead of being picked up as a capturable key.
//
// Downgraded to C# 5 syntax (no null-conditional operator, no
// expression-bodied members) to build with the legacy csc, same as
// ElkBind.cs.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	/// <summary>
	/// "Press it now" bind capture: the first non-modifier KeyCode (keyboard
	/// or joystick button) freshly pressed while capturing becomes the
	/// primary; every OTHER KeyCode already held at that same instant
	/// becomes a modifier. Driven by calling Tick() every frame while
	/// NeedsTick is true (ElkWindow is the only driver in ELK — unlike
	/// KRILL, there is no separate flight-scene input manager to also tick
	/// it, since the toolbar only exists in the Space Center scene).
	///
	/// Practical note for modifier combos: hold the modifier BEFORE pressing
	/// the primary. On the exact frame a key is first pressed its own
	/// GetKeyDown is true too, so if two keys were pressed on the very same
	/// frame either could be picked as primary; normal human input timing
	/// never hits this (see KrillCapture's original design notes).
	/// </summary>
	public static class ElkCapture
	{
		private static int lastTickFrame = -1;

		private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

		// A conventional modifier key (Shift in particular) fires its own
		// GetKeyDown the instant it's pressed, so without this exclusion a
		// capture would complete on the modifier alone before the player
		// ever reaches the intended primary. These keys can still end up in
		// a bind's modifier list (the scan below never excludes them there)
		// — they just can never BE the primary themselves.
		private static readonly HashSet<KeyCode> ModifierOnlyKeys = new HashSet<KeyCode>
		{
			KeyCode.LeftShift, KeyCode.RightShift,
			KeyCode.LeftControl, KeyCode.RightControl,
			KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr,
			KeyCode.LeftCommand, KeyCode.RightCommand,
			KeyCode.LeftApple, KeyCode.RightApple,
			KeyCode.LeftWindows, KeyCode.RightWindows,
		};

		// Mouse buttons: never capturable (an incidental UI click while the
		// window is open would otherwise bind to it). Delete: never
		// capturable either — it's the dedicated clear gesture (see Tick()),
		// so it can't also become a bind's primary or modifier.
		private static readonly HashSet<KeyCode> ExcludedKeys = new HashSet<KeyCode>
		{
			KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3,
			KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6,
			KeyCode.Delete,
		};

		// While capturing, ANY key press is meant to be consumed by the
		// bind, not leak into the game — most visibly Escape opening the
		// stock pause menu.
		private const string LockId = "ELK_capture";

		// The Space Center's own Escape handling (KSP.UI.Screens.UISpaceCenter
		// .Update, confirmed by decompiling Assembly-CSharp.dll) reacts to
		// Escape's KEY-UP, and its pause-menu path (QuitToMenu) checks
		// InputLockManager.IsLocked(ControlTypes.KSC_UI) on that SAME frame
		// — so the lock has to still be held at that instant, not removed
		// as soon as we ourselves observe the key-up. Removing it on the
		// very same frame races Unity's unspecified Update() order between
		// our capture driver and UISpaceCenter: if ours happens to run
		// first, the lock is already gone by the time UISpaceCenter checks
		// it, and the pause menu opens anyway (reproduced in-game). Fixed
		// by waiting one additional frame past the key-up before actually
		// releasing the lock (escapeKeyUpSeen below) — Input.GetKeyUp is
		// only ever true for the one frame the key is released, so on the
		// following frame UISpaceCenter's own check isn't even active
		// anymore, and removing the lock then is race-free by construction.
		// MaxUnlockWaitFrames is only a safety net for the rare case the
		// key-up is never seen at all (e.g. focus lost mid-hold), so the
		// lock can never get stuck forever.
		private const int MaxUnlockWaitFrames = 180;

		private static bool escapeKeyUpSeen;

		public static bool IsCapturing { get; private set; }

		/// <summary>True while a driver must keep calling Tick(): during an active capture, or while waiting for Escape's key-up after a cancel.</summary>
		public static bool NeedsTick
		{
			get { return IsCapturing || unlockWaitFramesLeft >= 0; }
		}

		private static Action<ElkBind> onCaptured;
		private static Action onCancelled;
		private static Action onCleared;
		private static int unlockWaitFramesLeft = -1;

		public static void Begin(Action<ElkBind> captured, Action cancelled, Action cleared)
		{
			IsCapturing = true;
			unlockWaitFramesLeft = -1;
			onCaptured = captured;
			onCancelled = cancelled;
			onCleared = cleared;
			InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS, LockId);
		}

		/// <summary>User-facing cancel (Escape): keeps the lock until one frame past Escape's key-up, see MaxUnlockWaitFrames and the class doc.</summary>
		public static void Cancel()
		{
			if (!IsCapturing)
				return;
			IsCapturing = false;
			unlockWaitFramesLeft = MaxUnlockWaitFrames;
			escapeKeyUpSeen = false;
			Action cancelled = onCancelled;
			onCaptured = null;
			onCancelled = null;
			onCleared = null;
			if (cancelled != null)
				cancelled();
		}

		/// <summary>Scene-teardown safety net: no Space Center Escape race to protect against once the scene itself is going away.</summary>
		public static void ForceCancel()
		{
			bool wasPending = IsCapturing || unlockWaitFramesLeft >= 0;
			IsCapturing = false;
			unlockWaitFramesLeft = -1;
			escapeKeyUpSeen = false;
			onCaptured = null;
			onCancelled = null;
			onCleared = null;
			if (wasPending)
				InputLockManager.RemoveControlLock(LockId);
		}

		public static void Tick()
		{
			if (Time.frameCount == lastTickFrame)
				return;
			lastTickFrame = Time.frameCount;

			if (unlockWaitFramesLeft >= 0)
			{
				unlockWaitFramesLeft--;

				if (escapeKeyUpSeen)
				{
					// A full frame has passed since we first observed
					// Escape's key-up: UISpaceCenter's own same-frame check
					// of that key-up (if it runs at all) has already had
					// its chance to see our lock still held, regardless of
					// Update() order. Safe to release now.
					unlockWaitFramesLeft = -1;
					escapeKeyUpSeen = false;
					InputLockManager.RemoveControlLock(LockId);
					return;
				}

				if (Input.GetKeyUp(KeyCode.Escape))
				{
					escapeKeyUpSeen = true;
				}
				else if (unlockWaitFramesLeft < 0)
				{
					InputLockManager.RemoveControlLock(LockId);
				}
				return;
			}
			if (!IsCapturing)
				return;

			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Cancel();
				return;
			}

			// Delete clears the bind instead of being scanned as a
			// capturable key. Not a PauseMenu key, so no key-up wait is
			// needed before releasing the lock.
			if (Input.GetKeyDown(KeyCode.Delete))
			{
				IsCapturing = false;
				InputLockManager.RemoveControlLock(LockId);
				Action cleared = onCleared;
				onCaptured = null;
				onCancelled = null;
				onCleared = null;
				if (cleared != null)
					cleared();
				return;
			}

			for (int i = 0; i < AllKeyCodes.Length; i++)
			{
				KeyCode candidate = AllKeyCodes[i];
				if (candidate == KeyCode.None || candidate == KeyCode.Escape
					|| ModifierOnlyKeys.Contains(candidate) || ExcludedKeys.Contains(candidate))
				{
					continue;
				}
				if (!Input.GetKeyDown(candidate))
					continue;

				ElkBind bind = new ElkBind();
				bind.primary = candidate;
				for (int j = 0; j < AllKeyCodes.Length; j++)
				{
					KeyCode modCandidate = AllKeyCodes[j];
					if (modCandidate == candidate || modCandidate == KeyCode.None || ExcludedKeys.Contains(modCandidate))
						continue;
					if (Input.GetKey(modCandidate))
					{
						bind.modifiers.Add(modCandidate);
					}
				}

				IsCapturing = false;
				InputLockManager.RemoveControlLock(LockId);
				Action<ElkBind> captured = onCaptured;
				onCaptured = null;
				onCancelled = null;
				onCleared = null;
				if (captured != null)
					captured(bind);
				return;
			}
		}
	}
}
