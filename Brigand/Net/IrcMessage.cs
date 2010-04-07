using System;
using System.Collections.Generic;
using System.Text;

namespace Brigand
{
	public class IrcMessage
	{
		private string prefix;
		private IrcPrefix from;
		private string command;
		private string[] parameters;
		private bool captured;

		public string Prefix { get { return prefix; } }

		public IrcPrefix From { get { return from; } }

		public string Command { get { return command; } }

		public IList<string> Parameters { get { return parameters; } }

		public bool Captured { get { return captured; } set { captured = value; } }

		public IrcMessage(string prefix, string command, params string[] parameters)
		{
			this.prefix = prefix;
			from = IrcPrefix.Parse(prefix);
			this.command = command.ToUpperInvariant();
			this.parameters = parameters;

			for (int i = 0; i < parameters.Length - 1; i++)
				if (parameters[i].IndexOf(' ') >= 0)
					throw new ArgumentException("Badly formed IRC message: Only the last message parameter may contain nulls.",
						"parameters");
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (prefix != null)
				sb.Append(':').Append(prefix).Append(' ');
			sb.Append(command);
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i] == null)
					continue;

				sb.Append(' ');
				if (i == parameters.Length - 1)
					sb.Append(':');
				sb.Append(parameters[i]);
			}

			return sb.ToString();
		}

		public static IrcMessage Parse(string data)
		{
			StringBuilder sb = new StringBuilder();
			List<string> para = new List<string>();
			int size = data.Length > 512 ? 512 : data.Length;
			Char[] c = data.ToCharArray(0, size);
			int pos = 0;
			string prefix = null;
			string command = null;

			if (c[0] == ':')
			{
				for (pos = 1; pos < c.Length; pos++)
				{
					if (c[pos] == ' ')
						break;

					sb.Append(c[pos]);
				}
				prefix = sb.ToString();
				sb.Length = 0;
				pos++;
			}

			for (; pos < c.Length; pos++)
			{
				if (c[pos] == ' ')
					break;
				sb.Append(c[pos]);
			}
			command = sb.ToString();
			sb.Length = 0;
			pos++;

			bool trailing = false;
			while (pos < c.Length)
			{
				if (c[pos] == ':')
				{
					trailing = true;
					pos++;
				}

				for (; pos < c.Length; pos++)
				{
					if (c[pos] == ' ' && !trailing)
						break;
					sb.Append(c[pos]);
				}
				para.Add(sb.ToString());
				sb.Length = 0;
				pos++;
			}

			return new IrcMessage(prefix, command, para.ToArray());
		}
	}
}
