using System;
using System.Collections.Generic;
using System.Text;

namespace Brigand
{
	public sealed class Ctcp : BotModule
	{
		public void Action(IrcTarget target, string text)
		{
			this.Send(target, "ACTION", text);
		}

		public void Send(IrcTarget target, string command, string data)
		{
			this.Irc.Say(target, new CtcpMessage(command, data).ToString());
		}
	}

	public class CtcpMessage
	{
		private string _command;
		private string _data;

		public string Command { get { return _command; } }

		public string Data { get { return _data; } }

		public CtcpMessage(string command, string data)
		{
			this._command = command;
			this._data = data;
		}

		public override string ToString()
		{
			return "\x01" + _command + " " + _data + "\x01";
		}
	}
}
