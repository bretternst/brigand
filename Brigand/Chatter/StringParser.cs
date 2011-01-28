using System.Collections.Generic;
using System.Text;

namespace Brigand.MarkovModel
{
	public static class StringParser
	{
		public static IEnumerable<Symbol> Parse(string input)
		{
			if(input == null) yield break;

			var buf = new StringBuilder();
			for (int i = 0; i < input.Length; i++)
			{
				char ch = input[i];

				if (!WordSymbol.IsWordChar(ch) && buf.Length > 0)
				{
					if (ch == ':' && (buf.ToString() == "http" || buf.ToString() == "https"))
					{
						for (; i < input.Length && !char.IsWhiteSpace(input[i]); i++)
						{
							buf.Append(input[i]);
						}
						string url = buf.ToString();
						buf.Length = 0;
						yield return new UrlSymbol(url);
						continue;
					}

					string word = buf.ToString().ToUpper();
					buf.Length = 0;
					yield return new WordSymbol(word);
				}
				
				if (WordSymbol.IsWordChar(ch))
				{
					buf.Append(ch);
					continue;
				}
				
				if (PunctuationSymbol.IsPunctuationChar(ch))
				{
					yield return new PunctuationSymbol(ch);
				}
			}

			if (buf.Length > 0)
				yield return new WordSymbol(buf.ToString().ToUpper());

			yield return new TerminateSymbol();
		}

		public static string Format(IEnumerable<Symbol> symbols)
		{
			StringBuilder sb = new StringBuilder();

			bool beginSentence = true;
			foreach (Symbol sym in symbols)
			{
				if (sym is TerminateSymbol)
				{
					break;
				}
				if (sb.Length > 0 && !(sym is PunctuationSymbol))
				{
					sb.Append(' ');
				}
				if (sym is UrlSymbol)
				{
					sb.Append(sym.ToString());
					beginSentence = false;
				}
				else if (beginSentence)
				{
					string word = sym.ToString().ToLower();
					word = word.Substring(0, 1).ToUpper() + word.Substring(1);
					sb.Append(word);
					beginSentence = false;
				}
				else
				{
					sb.Append(sym.ToString().ToLower());
				}
				if (sym is PunctuationSymbol && ((PunctuationSymbol)sym).IsTerminating)
				{
					beginSentence = true;
				}
			}
			return sb.ToString();
		}
	}
}
