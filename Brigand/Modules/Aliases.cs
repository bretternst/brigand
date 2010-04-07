using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brigand
{
	public sealed class Aliases : BotModule
	{
		[ModuleProperty("prefix")]
		public string Prefix { get; set; }

		public event EventHandler<AliasEventArgs> CallAlias;

		protected override void OnInit()
		{
			if (string.IsNullOrEmpty(this.Prefix))
				throw new InvalidOperationException("The AliasPrefix property is required.");

			base.OnInit();
		}

		protected override void OnStart()
		{
			base.OnStart();

			this.Irc.PrivateMessage += new EventHandler<IrcChatEventArgs>(Irc_PrivateMessage);
		}

		private void OnCallAlias(IrcChatEventArgs e, string name, string[] arguments)
		{
			var evt = this.CallAlias;
			if (evt != null)
				evt(this, new AliasEventArgs(e, name, arguments));
		}

		private void Irc_PrivateMessage(object sender, IrcChatEventArgs e)
		{
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
	}

	public class AliasEventArgs : IrcChatEventArgs
	{
		private string _name;
		private string[] _arguments;

		public string Name { get { return _name; } }

		public IList<string> Arguments { get { return _arguments; } }

		public bool Handled { get; set; }

		internal AliasEventArgs(IrcChatEventArgs e, string name, string[] arguments) :
			base(e)
		{
			_name = name;
			_arguments = arguments;
		}
	}
}
