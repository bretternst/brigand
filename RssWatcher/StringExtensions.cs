using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Brigand
{
	public static class StringExtensions
	{
		public static string StripHtml(this string input)
		{
			return HttpUtility.HtmlDecode(Regex.Replace(input, "<.*?>", ""));
		}
	}
}
