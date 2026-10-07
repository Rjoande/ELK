// Per-vessel memory of the active key set, saved in the vessel's own node
// (Activation Always so OnSave runs in every scene). ElkSets reads/writes it.

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
