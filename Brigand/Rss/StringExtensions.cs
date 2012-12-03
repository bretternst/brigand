using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;

namespace Brigand
{
	public static class StringExtensions
	{
		private static readonly char[] TextSplitChars = new[] { ' ', '-', '.', '?', ';', ',', '!' };

		public static string StripHtml(this string input)
		{
			return HttpUtility.HtmlDecode(Regex.Replace(input, "<.*?>", ""));
		}

		public static IEnumerable<string> WordBreak(this string input, int lineLength)
		{
			while (input.Length > lineLength)
			{
				int i = lineLength - 1;
				while (i >= 0 && Array.IndexOf(TextSplitChars, input[i]) < 0)
				{
					--i;
				}
				if (i == 0)
				{
					i = lineLength - 1;
				}
				yield return input.Substring(0, i + 1);
				input = input.Substring(i + 1);
			}
			
			if (input.Length > 0)
			{
				yield return input;
			}
		}
	}
}
