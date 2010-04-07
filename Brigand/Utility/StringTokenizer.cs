using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Brigand
{
	internal class StringTokenizer
	{
		static Regex _tokenizer = new Regex(@"(""([^""\\]|\\.)*""|[^\s]+)", RegexOptions.Compiled);

		static public IEnumerable<string> Tokenize(string s)
		{
			foreach (Match m in _tokenizer.Matches(s))
				yield return m.Value.StartsWith("\"") && m.Value.EndsWith("\"") ?
					MakeStringRaw(m.Value) : m.Value;
		}

		static public string MakeStringRaw(string s)
		{
			int length = s.Length;
			StringBuilder raw = new StringBuilder(length);

			for (int i = 1; i < length - 1; i++)
			{
				char c = s[i];

				// Process escape sequences
				if (c == '\\' && i < length - 2)
				{
					char c1 = s[i + 1];
					switch (c1)
					{
						case '\"':
						case '\\':
						case '\'':
							raw.Append(c1); i++;
							break;
						case 'b':
							raw.Append('\b'); i++;
							break;
						case 'f':
							raw.Append('\f'); i++;
							break;
						case 'n':
							raw.Append('\n'); i++;
							break;
						case 'O':
							raw.Append('\u0000'); i++;
							break;
						case 'r':
							raw.Append('\r'); i++;
							break;
						case 't':
							raw.Append('\t'); i++;
							break;
						case 'v':
							raw.Append('\v'); i++;
							break;
						case 'u':
							{
								bool success = false;

								if (i < length - 5)
								{
									char[] hex = new char[4];
									for (int j = 0; j < 4; j++) hex[j] = s[i + j + 2];
									int uValue;
									bool isHex = int.TryParse(new string(hex),
										System.Globalization.NumberStyles.AllowHexSpecifier,
										null, out uValue);
									if (isHex)
									{
										success = true;
										raw.Append((char)uValue);
									}
								}
								if (success)
								{
									i += 5;
								}
								else
								{
									raw.Append("\\");
								}
							}
							break;
						case 'x':
							{
								bool success = false;

								if (i < length - 3)
								{
									char[] hex = new char[2];
									hex[0] = s[i + 2];
									hex[1] = s[i + 3];
									int xValue;
									bool isHex = int.TryParse(new string(hex),
										System.Globalization.NumberStyles.AllowHexSpecifier,
										null, out xValue);
									if (isHex)
									{
										success = true;
										raw.Append((char)xValue);
									}
								}
								if (success)
								{
									i += 3;
								}
								else
								{
									raw.Append("\\"); i++;
								}
							}
							break;
						case '0':
						case '1':
						case '2':
						case '3':
							{
								bool success = false;

								if (i < length - 3)
								{
									char[] oct = new char[3];
									oct[0] = s[i + 1];
									oct[1] = s[i + 2];
									oct[2] = s[i + 3];
									if (Char.IsNumber(oct[0]) && Char.IsNumber(oct[1]) &&
										Char.IsNumber(oct[2]))
									{
										int oValue = Convert.ToInt32(new string(oct), 8);
										if (oValue <= 255)
										{
											raw.Append((char)oValue);
											success = true;
										}
									}
								}
								if (success)
								{
									i += 3;
								}
								else
								{
									raw.Append("\\");
								}
							}
							break;
						default:
							raw.Append("\\");
							break;
					}
				}
				else
					raw.Append(s[i]);
			}

			return raw.ToString();
		}
	}
}
