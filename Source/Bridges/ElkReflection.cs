// Small reflection helpers shared by the per-mod bridges. Every lookup
// returns null (with one warning line) instead of throwing, so a bridge
// can resolve its members one by one and degrade per member when a mod
// release renames something.
//
// AssemblyLoader.loadedAssemblies confirmed against KSP 1.12.5
// Assembly-CSharp.dll, decompiled: LoadedAssembly.dllName is the DLL file
// name without extension and never changes, while LoadedAssembly.name
// starts equal to it but is OVERWRITTEN with the [KSPAssembly] attribute's
// name when the DLL declares one (NavUtilitiesUpdated.dll becomes
// "NavInstrumentsContinued"). Matching on name alone silently missed
// NavUtilities in-game, so dllName is the key and name only a fallback.

using System;
using System.Reflection;
using UnityEngine;

namespace ELK
{
	public static class ElkReflection
	{
		/// <summary>The loaded assembly whose DLL file is named dllName (no extension), or, failing that, whose KSPAssembly name is dllName; null if neither.</summary>
		public static Assembly FindAssembly(string dllName)
		{
			try
			{
				foreach (AssemblyLoader.LoadedAssembly la in AssemblyLoader.loadedAssemblies)
				{
					if (la != null && (la.dllName == dllName || la.name == dllName))
						return la.assembly;
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] assembly scan failed for " + dllName + ": " + e.Message);
			}
			return null;
		}

		public static Type FindType(Assembly assembly, string fullName, string modLabel)
		{
			if (assembly == null)
				return null;
			try
			{
				Type t = assembly.GetType(fullName, false);
				if (t == null)
					Debug.LogWarning("[ELK] " + modLabel + ": type " + fullName + " not found");
				return t;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + modLabel + ": type " + fullName + " lookup failed: " + e.Message);
				return null;
			}
		}

		public static FieldInfo FindField(Type type, string name, BindingFlags flags, string modLabel)
		{
			if (type == null)
				return null;
			try
			{
				FieldInfo f = type.GetField(name, flags);
				if (f == null)
					Debug.LogWarning("[ELK] " + modLabel + ": field " + type.Name + "." + name + " not found");
				return f;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + modLabel + ": field " + type.Name + "." + name + " lookup failed: " + e.Message);
				return null;
			}
		}

		public static PropertyInfo FindProperty(Type type, string name, BindingFlags flags, string modLabel)
		{
			if (type == null)
				return null;
			try
			{
				PropertyInfo p = type.GetProperty(name, flags);
				if (p == null)
					Debug.LogWarning("[ELK] " + modLabel + ": property " + type.Name + "." + name + " not found");
				return p;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + modLabel + ": property " + type.Name + "." + name + " lookup failed: " + e.Message);
				return null;
			}
		}

		public static Type FindNestedType(Type type, string name, string modLabel)
		{
			if (type == null)
				return null;
			try
			{
				Type t = type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
				if (t == null)
					Debug.LogWarning("[ELK] " + modLabel + ": nested type " + type.Name + "." + name + " not found");
				return t;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + modLabel + ": nested type " + type.Name + "." + name + " lookup failed: " + e.Message);
				return null;
			}
		}

		public static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type[] parameterTypes, string modLabel)
		{
			if (type == null)
				return null;
			try
			{
				MethodInfo m = type.GetMethod(name, flags, null, parameterTypes, null);
				if (m == null)
					Debug.LogWarning("[ELK] " + modLabel + ": method " + type.Name + "." + name + " not found");
				return m;
			}
			catch (Exception e)
			{
				Debug.LogWarning("[ELK] " + modLabel + ": method " + type.Name + "." + name + " lookup failed: " + e.Message);
				return null;
			}
		}
	}
}
