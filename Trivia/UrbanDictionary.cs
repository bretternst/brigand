using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Brigand
{
	public class UrbanDictionary : ITriviaProvider
	{
		#region Constants
		// the HTML fragments to look for
		private const string PHRASE_TAG_START = "<td class='word'>";
		private const string PHRASE_TAG_END = "</td>";
		private const string DEF_TAG_START = "<div class=\"definition\">";
		private const string DEF_TAG_END = "</div>";

		// the URL of the page to get
		private const string URL = "http://www.urbandictionary.com/random.php";
		// because some people like to write novels...
		private const int MAX_DEF_SIZE = 256;

		#endregion

		#region Private Members
		private List<string> defs;
		private int currentHint;
		#endregion

		#region Properties
		public int QuestionId { get; private set; }
		public string QuestionText { get; private set; }
		public string PrimaryAnswer { get; private set; }
		public bool HasHints
		{
			get
			{
				if (currentHint >= defs.Count) return false;

				return true;
			}
		}
		#endregion

		public UrbanDictionary()
		{
			defs = new List<string>();
			QuestionText = null;
			PrimaryAnswer = null;
		}

		public void NextQuestion()
		{
			string response, def;
			int tagIndex;

			// get the entry
			using (System.Net.WebClient webAccess = new System.Net.WebClient())
			{
				webAccess.Encoding = System.Text.Encoding.UTF8;
				response = webAccess.DownloadString(URL);
			}

			// initialize things for this new question
			defs.Clear();
			PrimaryAnswer = "";
			currentHint = 0;


			tagIndex = 0;
			// loop until we don't find any more phrase tags
			while (true)
			{
				// find the next phrase tag
				tagIndex = response.IndexOf(PHRASE_TAG_START, tagIndex) + PHRASE_TAG_START.Length;
				if (tagIndex - PHRASE_TAG_START.Length == -1) break;

				// only take the phrase the first time we see it
				if (PrimaryAnswer == "")
				{
					PrimaryAnswer = HTMLReplace(
						response.Substring(tagIndex, response.IndexOf(PHRASE_TAG_END, tagIndex) - tagIndex).Trim()
					);
				}

				// pull out the definition for this iteration
				tagIndex = response.IndexOf(DEF_TAG_START, tagIndex) + DEF_TAG_START.Length;
				def = HTMLReplace(
						response.Substring(tagIndex, response.IndexOf(DEF_TAG_END, tagIndex) - tagIndex).Trim().Replace("\n", "").Replace("\r", "")
				);

				foreach (string word in PrimaryAnswer.Split(' '))
				{
					if (word.Length >= 3)
					{
						def = Regex.Replace(def, word, new String('*', word.Length), RegexOptions.IgnoreCase);
					}
				}

				// only add definitions that aren't way too long
				if (def.Length <= MAX_DEF_SIZE) defs.Add(def);

			}

			// apparently this question sucked, so let's get another
			if (defs.Count == 0)
			{
				NextQuestion();
				return;
			}

			QuestionId = PrimaryAnswer.GetHashCode();

			// the questionText should be the first definition, so remove that from the definitions array
			QuestionText = defs[0];
			defs.RemoveAt(0);

		}

		public string NextHint()
		{
			// you're a bad man and should have checked this first
			if (!HasHints)
			{
				return null;
			}

			currentHint++;

			// return the previous comment
			return defs[currentHint - 1];
		}

		public bool CheckAnswer(string candidate)
		{
			if (candidate.ToLower() == PrimaryAnswer.ToLower())
			{
				return true;
			}

			return false;
		}

		private static string HTMLReplace(string toFix)
		{
			// collapse all <br> tags in any form down to a single one, then replaces it with a separator
			string foo = Regex.Replace(Regex.Replace(toFix, "(<br[^>]*>)+", "<br>"), "<br>", " | ");
			// remove HTML tags
			foo = Regex.Replace(foo, "<[^>]*>", "");
			// a cheap way to undo HTML escapes... also removes the last separator if there is one
			return foo.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&quot;", "\"").Replace("&nbsp;", " ").Replace("&amp;", "&");
		}
	}
}
