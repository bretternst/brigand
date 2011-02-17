using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Floe.Net;

namespace Brigand
{
	public class Shame : BotModule
	{
		private const string AliasName = "shame";
		private Timer _timer;
		private Dictionary<string, Version> _results;

		protected override void OnInit()
		{
			base.OnInit();

			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(Aliases_CallAlias);
		}

		private void Aliases_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name == AliasName && e.To.IsChannel)
			{
				if (_timer != null)
				{
					_timer.Dispose();
				}

				this.Irc.CtcpCommandReceived += new EventHandler<Floe.Net.CtcpEventArgs>(Irc_CtcpCommandReceived);
				this.Irc.SendCtcp(e.To, new CtcpCommand("VERSION"), false);
				_timer = new Timer(this.Dispatcher, CompileResults, 10000, e.To.Name);
			}
		}

		private void Irc_CtcpCommandReceived(object sender, CtcpEventArgs e)
		{
			if (e.Command.Command == "VERSION" && e.Command.Arguments.Length == 2 && e.Command.Arguments[0] == "Floe")
			{
				var v = new Version(e.Command.Arguments[1]);
				if (!_results.ContainsKey(e.From.Nickname))
				{
					_results.Add(e.From.Nickname, v);
				}
			}
		}

		private void CompileResults(object sender, EventArgs e)
		{
			var chan = ((CallbackEventArgs)e).State as string;
			var list = from v in _results
					   where v.Value.CompareTo(this.Irc.GetType().Assembly.GetName().Version) < 0
					   select string.Format("{0} ({1})", v.Key, v.Value.ToString());
			if (list.Any())
			{
				var fmt = string.Format("The following users have an old version of Floe: {0}",
					string.Join(", ", list));
				this.Irc.PrivateMessage(new IrcTarget(chan), fmt);
			}
		}
	}
}
