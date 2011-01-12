using System;
using System.Collections.Generic;
using System.Linq;

using Floe.Net;

namespace Brigand
{
	#region AliasEventArgs class

	public class AliasEventArgs : IrcMessageEventArgs
	{
		private string _name;
		private string[] _arguments;
		private IrcTarget _replyTo;

		public string Name { get { return _name; } }

		public IrcTarget ReplyTo { get { return _replyTo; } }

		public IList<string> Arguments { get { return _arguments; } }

		internal AliasEventArgs(IrcMessageEventArgs e, string name, string[] arguments)
			: base(e.Message)
		{
			_name = name;
			_arguments = arguments;
			_replyTo = e.To.Type == IrcTargetType.Channel ? e.To : new IrcTarget(e.From);
		}
	}

	#endregion

	public sealed class Aliases : BotModule
	{
		[ModuleProperty("prefix")]
		public string Prefix { get; set; }

		public event EventHandler<AliasEventArgs> CallAlias;

		protected override void OnInit()
		{
			if (string.IsNullOrEmpty(this.Prefix))
			{
				throw new InvalidOperationException("The AliasPrefix property is required.");
			}

			base.OnInit();
		}

		protected override void OnStart()
		{
			base.OnStart();

			this.Irc.PrivateMessaged += new EventHandler<IrcMessageEventArgs>(Irc_PrivateMessaged);
		}

		private void Irc_PrivateMessaged(object sender, IrcMessageEventArgs e)
		{
			if (string.Compare(e.From.Nickname, this.Irc.Nickname, true) == 0)
			{
				return;
			}

			string line = e.Text.Trim();

			if (line.StartsWith(this.Prefix, StringComparison.Ordinal) && line.Length > this.Prefix.Length)
			{
				string aliasName = line.Substring(this.Prefix.Length).Split(' ')[0];
				string paramString = "";
				int firstSpace = line.IndexOf(' ');
				if (firstSpace >= 0 && line.Length > firstSpace + 1)
				{
					paramString = line.Substring(firstSpace + 1);
				}
				this.OnCallAlias(e, aliasName, StringTokenizer.Tokenize(paramString).ToArray());
			}
		}

		private void OnCallAlias(IrcMessageEventArgs e, string name, string[] arguments)
		{
			var evt = this.CallAlias;
			if (evt != null)
			{
				evt(this, new AliasEventArgs(e, name, arguments));
			}
		}
	}
}
