// "Press now" key capture, ported from KRILL's KrillCapture (MIT); Delete
// clears the bind. C# 5 syntax only (legacy csc).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ELK
{
	/// <summary>"Press now" capture: first non-modifier KeyCode freshly pressed = primary,
	/// every other key held at that instant = modifier. Hold the modifier BEFORE
	/// the primary. Ticked each frame by ElkToolbarApp while NeedsTick.</summary>
	public static class ElkCapture
	{
		private static int lastTickFrame = -1;

		private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

		// Modifier keys fire GetKeyDown too, so they can never be the primary
		// (they can still be in the modifier list).
		private static readonly HashSet<KeyCode> ModifierOnlyKeys = new HashSet<KeyCode>
		{
			KeyCode.LeftShift, KeyCode.RightShift,
			KeyCode.LeftControl, KeyCode.RightControl,
			KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr,
			KeyCode.LeftCommand, KeyCode.RightCommand,
			KeyCode.LeftApple, KeyCode.RightApple,
			KeyCode.LeftWindows, KeyCode.RightWindows,
		};

		// Mouse buttons and Delete (the clear gesture) are never capturable.
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

		// UISpaceCenter reacts to Escape's key-UP and checks the KSC_UI lock on
		// that same frame, so the lock is released one frame after the key-up
		// (escapeKeyUpSeen); MaxUnlockWaitFrames is a safety net.
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

		// Unity's KeyCode lays joystick buttons out as JoystickButton0..19
		// (any joystick) followed by Joystick1Button0..19 .. Joystick8Button0..19.
		private const int ButtonsPerJoystick = 20;

		private static bool IsGenericJoystickButton(KeyCode code)
		{
			return code >= KeyCode.JoystickButton0 && code <= KeyCode.JoystickButton19;
		}

		private static int ButtonIndex(KeyCode code)
		{
			if (IsGenericJoystickButton(code))
				return (int)code - (int)KeyCode.JoystickButton0;
			return ((int)code - (int)KeyCode.Joystick1Button0) % ButtonsPerJoystick;
		}

		/// <summary>The JoystickKButtonN currently held for button index n, or None.</summary>
		private static KeyCode SpecificJoystickButtonHeld(int index)
		{
			for (int code = (int)KeyCode.Joystick1Button0 + index; code <= (int)KeyCode.Joystick8Button19; code += ButtonsPerJoystick)
			{
				if (Input.GetKey((KeyCode)code))
					return (KeyCode)code;
			}
			return KeyCode.None;
		}

		/// <summary>A generic JoystickButtonN primary becomes the JoystickKButtonN actually pressed, so the bind names one joystick and one button.</summary>
		private static KeyCode PreferSpecificJoystick(KeyCode primary)
		{
			if (!IsGenericJoystickButton(primary))
				return primary;
			KeyCode specific = SpecificJoystickButtonHeld(ButtonIndex(primary));
			return specific == KeyCode.None ? primary : specific;
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
					// One full frame after Escape's key-up: UISpaceCenter has already seen the
					// lock, safe to release.
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

				// What is stored is the LOGICAL joystick index (device known
				// by name, see ElkJoysticks); the physical one is what the
				// modifier scan below must compare against.
				KeyCode physicalPrimary = PreferSpecificJoystick(candidate);
				ElkBind bind = new ElkBind();
				bind.primary = ElkJoysticks.Logical(physicalPrimary);
				// HashSet: Enum.GetValues lists alias values twice (RightApple
				// and RightCommand share one value), so without it a held
				// alias key was recorded as a duplicated modifier.
				HashSet<KeyCode> mods = new HashSet<KeyCode>();
				for (int j = 0; j < AllKeyCodes.Length; j++)
				{
					KeyCode modCandidate = AllKeyCodes[j];
					if (modCandidate == physicalPrimary || modCandidate == KeyCode.None || ExcludedKeys.Contains(modCandidate))
						continue;
					if (!Input.GetKey(modCandidate))
						continue;
					// Unity reports joystick buttons as JoystickButtonN and JoystickKButtonN;
					// only the specific one is kept.
					if (IsGenericJoystickButton(modCandidate) && SpecificJoystickButtonHeld(ButtonIndex(modCandidate)) != KeyCode.None)
						continue;
					mods.Add(ElkJoysticks.Logical(modCandidate));
				}
				bind.modifiers.AddRange(mods);

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
