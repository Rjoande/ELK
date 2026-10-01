// Per-vessel memory of the active key set, persisted inside the vessel's
// own node of the save (KSP attaches one VesselModule of every declared
// type to every vessel, no part needed, and calls OnLoad/OnSave around the
// VESSEL node - verified on the KSP 1.12.5 Assembly-CSharp.dll, decompiled).
// Default Activation (Always) on purpose: a module only active in flight
// might not get OnSave in other scenes and would lose the value. The cost
// is one tiny node per vessel, debris included.
//
// Nothing else lives here: ElkSets reads and writes activeSet.

namespace ELK
{
	public class ElkVesselModule : VesselModule
	{
		private const string ValueName = "activeSet";

		/// <summary>The set name this vessel was last switched to, or "" for "use the global default".</summary>
		public string activeSet = "";

		protected override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			string value = node.GetValue(ValueName);
			activeSet = (value == null) ? "" : value.Trim();
		}

		protected override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if (!string.IsNullOrEmpty(activeSet))
			{
				node.AddValue(ValueName, activeSet);
			}
		}
	}
}
