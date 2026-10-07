// Reflection helpers for the bridges: lookups return null with one warning.
// Assemblies match on dllName (stable); LoadedAssembly.name is overwritten by
// [KSPAssembly] and is only a fallback.

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
