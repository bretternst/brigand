using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Brigand.MarkovModel;

namespace Brigand
{
	public class Chatter : BotModule
	{
		private Model model;

		const string ATTN_CHARS = ",:";
		const string NO_REPLY = null;
		const string TRAIN_ALIAS = "train";
		const string STATS_ALIAS = "brainstats";
		const string TRAIN_COMPLETE = "OM NOM NOM!";
		const string TRAIN_FAILED = "Could not train: {0}";

		[ModuleProperty("brainFile")]
		public string BrainFile { get; set; }

		public void Train(string fileName)
		{
			StreamReader sr = new StreamReader(fileName);
			while (!sr.EndOfStream)
			{
				string s = sr.ReadLine();
				model.Learn(s);
			}
			sr.Close();
		}

		public void Save()
		{
			model.Save(this.BrainFile);
		}

		public void Clear()
		{
			model = null;
			model = new Model();
		}

		protected override void OnInit()
		{
			this.Irc.PrivateMessage += new EventHandler<IrcChatEventArgs>(PrivateMessage);
			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(AliasModule_CallAlias);

			if (this.BrainFile == null)
				throw new InvalidOperationException("The BrainFile property is required.");
		}

		protected override void OnStart()
		{
			if (File.Exists(this.BrainFile))
			{
				model = Model.Load(this.BrainFile);
			}
			else
			{
				model = new Model();
			}
		}

		protected override void OnStop()
		{
			this.Save();
		}

		private void AliasModule_CallAlias(object sender, AliasEventArgs e)
		{
			if (!e.Handled)
			{
				if (e.Name == TRAIN_ALIAS && e.Arguments.Count == 1)
				{
					try
					{
						this.Train(e.Arguments[0]);
						Irc.Say(e.ReplyTo, TRAIN_COMPLETE);
					}
					catch (IOException ex)
					{
						Irc.Say(e.ReplyTo, string.Format(TRAIN_FAILED, ex.Message));
					}
				}
				else if (e.Name == STATS_ALIAS)
				{
					Irc.Say(e.ReplyTo, "Total unique symbols: " + model.SymbolCount);
					Irc.Say(e.ReplyTo, "Total unique tuples: " + model.TotalTupleCount);
					Irc.Say(e.ReplyTo, "Total nodes: " + model.TotalNodeCount);
					Irc.Say(e.ReplyTo, "Memory usage: " + model.TotalBytesUsed);
				}
			}
		}

		private void PrivateMessage(object sender, IrcChatEventArgs e)
		{
			if (e.Target.IsChannel && !e.IsSelf)
			{
				string raw = e.Text.Trim();
				if (raw.Length > this.Irc.Nickname.Length &&
					raw.StartsWith(this.Irc.Nickname, StringComparison.CurrentCultureIgnoreCase) &&
					ATTN_CHARS.IndexOf(raw[this.Irc.Nickname.Length]) >= 0)
				{
					string text = raw.Substring(this.Irc.Nickname.Length + 1).Trim();
					string reply = model.Query(text);
					if (!string.IsNullOrEmpty(reply))
					{
						this.Irc.Say(e.Target.Channel, reply);
					}
					else
					{
						if (!string.IsNullOrEmpty(NO_REPLY))
							this.Irc.Say(e.Target.Channel, NO_REPLY);
					}
				}
				else
				{
					model.Learn(raw);
				}
			}
		}
	}
}
