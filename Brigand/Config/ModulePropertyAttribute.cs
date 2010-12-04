using System;

namespace Brigand
{
	[AttributeUsage(AttributeTargets.Property)]
	public class ModulePropertyAttribute : Attribute
	{
		public string ConfigName { get; private set; }

		public ModulePropertyAttribute(string configName)
		{
			this.ConfigName = configName;
		}
	}
}
