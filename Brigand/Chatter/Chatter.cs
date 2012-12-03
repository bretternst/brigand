using System;
using System.IO;
using Brigand.MarkovModel;

using Floe.Net;

namespace Brigand
{
	public class Chatter : BotModule
	{
		private Model _model;

		private const string AttnChars = ",:";
		private const string NoReply = null;
		private const string TrainAlias = "train";
		private const string StatsAlias = "brainstats";
		private const string TrainComplete = "OM NOM NOM!";
		private const string TrainFailed = "Could not train: {0}";

		[ModuleProperty("brainFile")]
		public string BrainFile { get; set; }

		public void Train(string fileName)
		{
			StreamReader sr = new StreamReader(fileName);
			while (!sr.EndOfStream)
			{
				string s = sr.ReadLine();
				_model.Learn(s);
			}
			sr.Close();
		}

		public void Save()
		{
			_model.Save(this.BrainFile);
		}

		public void Clear()
		{
			_model = null;
			_model = new Model();
		}

		protected override void OnInit()
		{
			this.Irc.PrivateMessaged += new EventHandler<IrcMessageEventArgs>(PrivateMessage);
			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(AliasModule_CallAlias);

			if (this.BrainFile == null)
				throw new InvalidOperationException("The BrainFile property is required.");
		}

		protected override void OnStart()
		{
			if (File.Exists(this.BrainFile))
			{
				_model = Model.Load(this.BrainFile);
			}
			else
			{
				_model = new Model();
			}
		}

		protected override void OnStop()
		{
			this.Save();
		}

		private void AliasModule_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name == TrainAlias && e.Arguments.Count == 1)
			{
				try
				{
					this.Train(e.Arguments[0]);
					Irc.PrivateMessage(e.ReplyTo, TrainComplete);
				}
				catch (IOException ex)
				{
					Irc.PrivateMessage(e.ReplyTo, string.Format(TrainFailed, ex.Message));
				}
			}
			else if (e.Name == StatsAlias)
			{
				Irc.PrivateMessage(e.ReplyTo, "Total unique symbols: " + _model.SymbolCount);
				Irc.PrivateMessage(e.ReplyTo, "Total unique tuples: " + _model.TotalTupleCount);
				Irc.PrivateMessage(e.ReplyTo, "Total nodes: " + _model.TotalNodeCount);
				Irc.PrivateMessage(e.ReplyTo, "Memory usage: " + _model.TotalBytesUsed);
			}
		}

		private void PrivateMessage(object sender, IrcMessageEventArgs e)
		{
			if (e.To.IsChannel)
			{
				string raw = e.Text.Trim();
				if (raw.Length > this.Irc.Nickname.Length &&
					raw.StartsWith(this.Irc.Nickname, StringComparison.CurrentCultureIgnoreCase) &&
					AttnChars.IndexOf(raw[this.Irc.Nickname.Length]) >= 0)
				{
					string text = raw.Substring(this.Irc.Nickname.Length + 1).Trim();
					string reply = _model.Query(text);
					if (!string.IsNullOrEmpty(reply))
					{
						this.Irc.PrivateMessage(e.To, reply);
					}
					else
					{
						if (!string.IsNullOrEmpty(NoReply))
							this.Irc.PrivateMessage(e.To, NoReply);
					}
				}
				else
				{
					_model.Learn(raw);
				}
			}
		}
	}
}
