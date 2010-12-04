using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Brigand
{
	public class HashSetOfStringTypeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
			{
				return true;
			}
			else
			{
				return base.CanConvertFrom(context, sourceType);
			}
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(string))
			{
				return true;
			}
			else
			{
				return base.CanConvertTo(context, destinationType);
			}
		}

		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			string s = value as string;
			if (s != null)
			{
				return new HashSet<string>(s.Split(','));
			}
			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			var val = value as HashSet<string>;
			if (val != null && destinationType == typeof(string))
			{
				return string.Join(",", val.ToArray());
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
}
