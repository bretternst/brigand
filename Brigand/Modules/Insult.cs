using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Floe.Net;

namespace Brigand
{
	public class Insult : BotModule
	{
		private static char[] Vowels = { 'a', 'e', 'i', 'o', 'u' };
		private const string AliasName = "insult";

		private Random rand = new Random();
		private string[][] words;
		private string[] reflect;
		private StringBuilder builder;

		protected override void OnInit()
		{
			base.OnInit();

			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(Aliases_CallAlias);
		}

		protected override void LoadConfig(XElement moduleEl)
		{
			base.LoadConfig(moduleEl);

			words = (from wordsEl in moduleEl.Elements("words")
			         select wordsEl.Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
			reflect = (from reflectEl in moduleEl.Elements("reflect")
			           select reflectEl.Value.Trim()).ToArray();
		}

		protected override void SaveConfig(XElement moduleEl)
		{
			base.SaveConfig(moduleEl);

			moduleEl.Add(
				from w in words
				select new XElement("words", string.Join(" ", w))
			);
			moduleEl.Add(
				from r in reflect
				select new XElement("reflect", r)
			);
		}

		private void Aliases_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name == AliasName && e.To.IsChannel && e.Arguments.Count > 0)
			{
				var nick = e.Arguments[0];
				string msg = null;
				if (string.Compare(nick, e.From.Nickname, StringComparison.OrdinalIgnoreCase) == 0) {
					msg = "Don't be so hard on yourself.";
				} else {
					if (reflect.Contains(nick, StringComparer.OrdinalIgnoreCase))
						nick = e.From.Nickname;

					var insult = this.GenerateInsult();
					if (insult.Length > 0)
						msg = string.Format("{0} is a{1} {2}", nick, Vowels.Contains(char.ToLower(insult[0])) ? "n" : "", insult);
				}
				this.Irc.PrivateMessage(e.ReplyTo, msg);
			}
		}

		private string GenerateInsult()
        {
			if (builder == null)
				builder = new StringBuilder();
			builder.Length = 0;

			for (var i = 0; i < words.Length; i++) {
				if (i > 0)
					builder.Append(' ');
				builder.Append(words[i][rand.Next(words[i].Length)]);
			}
			return builder.ToString();
		}
	}
}
