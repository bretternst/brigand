using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Brigand;

namespace Brigand
{
	public enum IrcTextFormat
	{
		Raw,
		Split
	}

	public sealed class Irc : BotModule, IDisposable
	{
		private IrcConnection _conn;
		private bool _quitting;
		private string _textSplitChars = " ";

		private void ProcessMessage(IrcMessage msg)
		{
			bool isSelf = (msg.From != null && msg.From.Nickname == this.Nickname);
			IrcTarget target;

			switch (msg.Command)
			{
				// Handle ping by responding with pong
				case "PING":
					_conn.QueueMessage(new IrcMessage(null, "PONG"));
					break;

				// Nick changes could be for us, or for another user
				case "NICK":
					if (isSelf)
						this.Nickname = msg.Parameters[0];
					if (NickChanged != null)
						NickChanged(this, new IrcNickEventArgs(msg, isSelf, msg.Parameters[0]));
					break;

				case "JOIN":
					if (Joined != null)
						Joined(this, new IrcChannelEventArgs(msg, isSelf, msg.Parameters[0]));
					break;

				case "PART":
					if (Parted != null)
						Parted(this, new IrcChannelEventArgs(msg, isSelf, msg.Parameters[0]));
					break;

				case "MODE":
					if (ModeChanged != null)
					{
						target = IrcTarget.Parse(msg.Parameters[0]);
						string[] s = new string[msg.Parameters.Count-2];
						for(int i = 0; i < s.Length; i++)
							s[i] = msg.Parameters[i+2];
						ModeChanged(this, new IrcModeEventArgs(msg, isSelf, target.Nickname == this.Nickname, target,
							msg.Parameters[1], s));
					}
					break;

				case "TOPIC":
					if (Topic != null)
						Topic(this, new IrcTopicEventArgs(msg, isSelf, msg.Parameters[0], msg.Parameters[1]));
					break;

				case "INVITE":
					if (Invited != null)
						Invited(this, new IrcChannelEventArgs(msg, false, msg.Parameters[1]));
					break;

				case "KICK":
					target = IrcTarget.Parse(msg.Parameters[1]);
					if (Kicked != null)
						Kicked(this, new IrcKickEventArgs(msg, isSelf, target.Nickname==this.Nickname, msg.Parameters[0], target));
					break;

				case "PRIVMSG":
					target = IrcTarget.Parse(msg.Parameters[0]);
					if (PrivateMessage != null && target != null && msg.From != null)
						PrivateMessage(this, new IrcChatEventArgs(msg, isSelf, target.Nickname==this.Nickname, target, msg.Parameters[1]));
					break;

				case "NOTICE":
					target = IrcTarget.Parse(msg.Parameters[0]);
					if (Notice != null && target != null && msg.From != null)
						Notice(this, new IrcChatEventArgs(msg, isSelf, target.Nickname==this.Nickname, target, msg.Parameters[1]));
					break;
			}
		}

		private void OnConnected()
		{
			if (Connected != null)
				Connected(this, IrcEventArgs.Empty);
		}

		private void OnDisconnected()
		{
			if (Disconnected != null)
				Disconnected(this, IrcEventArgs.Empty);
		}

		private void OnQuit()
		{
			if (this.Quit != null)
				Quit(this, IrcEventArgs.Empty);
		}

		private void OnMessage(IrcMessageEventArgs e)
		{
			if (Message != null)
				Message(this, e);

			ProcessMessage(e.Message);
		}

		private void conn_Connected(object sender, IrcEventArgs e)
		{
			this.WriteTraceMessage("Connected to IRC Server");
			OnConnected();
		}

		private void conn_Disconnected(object sender, IrcEventArgs e)
		{
			this.WriteTraceMessage("Disconnected from IRC Server");

			OnDisconnected();
			if (this.AutoReconnect && !_quitting)
			{
				_conn.Open();
			}
			else
			{
				OnQuit();
			}
		}

		private void conn_MessageSent(object sender, IrcMessageEventArgs e)
		{
			this.WriteTraceMessage("SEND: " + e.Message.ToString());
		}

		private void conn_MessageReceived(object sender, IrcMessageEventArgs e)
		{
			this.WriteTraceMessage("RECV: " + e.Message.ToString());
			OnMessage(e);
		}

		private void conn_Error(object sender, System.IO.ErrorEventArgs e)
		{
			this.WriteTraceMessage(e.GetException().ToString());
		}

		protected override void OnInit()
		{
			base.OnInit();

			if (String.IsNullOrEmpty(this.Server))
				throw new InvalidOperationException("Server is a required property.");
			if (this.Port == 0)
				this.Port = 6667;
			if (String.IsNullOrEmpty(this.UserName))
				this.UserName = "irc";
			if (String.IsNullOrEmpty(this.FullName))
				this.FullName = "irc";
			if (String.IsNullOrEmpty(this.Nickname))
				throw new InvalidOperationException("Nickname is a required property.");
			if (String.IsNullOrEmpty(this.Localhost))
				this.Localhost = Environment.MachineName;

			_conn = new IrcConnection(this.Server, this.Port, this.Nickname, this.UserName, this.FullName, this.Localhost);
			_conn.Connected += conn_Connected;
			_conn.Disconnected += conn_Disconnected;
			_conn.MessageReceived += conn_MessageReceived;
			_conn.MessageSent += conn_MessageSent;
			_conn.Error += conn_Error;
		}

		protected override void OnStop()
		{
			_conn.Disconnect(this.SignOutMessage);
			base.OnStop();
		}

		internal void Open()
		{
			_conn.Open();
		}

		[ModuleProperty("server")]
		public string Server { get; set; }

		[ModuleProperty("port")]
		public int Port { get; set; }

		[ModuleProperty("nickName")]
		public string Nickname { get; set; }

		[ModuleProperty("userName")]
		public string UserName { get; set; }

		[ModuleProperty("fullName")]
		public string FullName { get; set; }

		[ModuleProperty("localHost")]
		public string Localhost { get; set; }

		[ModuleProperty("signOutMessage")]
		public string SignOutMessage { get; set; }

		[ModuleProperty("autoReconnect")]
		public bool AutoReconnect { get; set; }

		[ModuleProperty("maxTextLength")]
		public int MaxTextLength { get; set; }

		public string TextSplitChars { get { return _textSplitChars; } set { _textSplitChars = value; } }

		public bool IsConnected { get { return _conn.IsConnected; } }

		public event EventHandler<IrcEventArgs> Connected;

		public event EventHandler<IrcEventArgs> Disconnected;

		public event EventHandler<IrcMessageEventArgs> Message;

		public event EventHandler<IrcNickEventArgs> NickChanged;

		public event EventHandler<IrcChannelEventArgs> Joined;

		public event EventHandler<IrcChannelEventArgs> Parted;

		public event EventHandler<IrcTopicEventArgs> Topic;

		public event EventHandler<IrcChannelEventArgs> Invited;

		public event EventHandler<IrcKickEventArgs> Kicked;

		public event EventHandler<IrcChatEventArgs> PrivateMessage;

		public event EventHandler<IrcChatEventArgs> Notice;

		public event EventHandler<IrcModeEventArgs> ModeChanged;

		public event EventHandler<IrcEventArgs> Quit;

		public void Join(string channel)
		{
			_conn.QueueMessage(new IrcMessage(null, "JOIN", channel));
		}

		public void Nick(string nickname)
		{
			_conn.QueueMessage(new IrcMessage(null, "NICK", nickname));
		}

		public void Part(string channel)
		{
			_conn.QueueMessage(new IrcMessage(null, "PART", channel));
		}

		public void SetTopic(string channel, string topic)
		{
			_conn.QueueMessage(new IrcMessage(null, "TOPIC", channel, topic));
		}

		public void Invite(string channel, string nickname)
		{
			_conn.QueueMessage(new IrcMessage(null, "INVITE", nickname, channel));
		}

		public void Mode(IrcTarget target, string modes, string[] modeParameters)
		{
			string[] parameters = new string[modeParameters.Length + 2];
			parameters[0] = target.ToString();
			parameters[1] = modes;
			for (int i = 0; i < modeParameters.Length; i++)
				parameters[i + 2] = modeParameters[i];
			_conn.QueueMessage(new IrcMessage(target.IsChannel ? target.Channel : target.Nickname, "MODE", parameters));
		}

		public void Kick(string channel, string nickname, string reason)
		{
			_conn.QueueMessage(new IrcMessage(null, "KICK", channel, nickname, reason));
		}

		public void Say(IrcTarget target, string text)
		{
			Say(target.ToString(), text, IrcTextFormat.Raw);
		}

		public void Say(IrcTarget target, string text, IrcTextFormat format)
		{
			Say(target.ToString(), text, format);
		}

		public void Say(string to, string text)
		{
			Say(to, text, IrcTextFormat.Raw);
		}

		public void Say(string to, string text, IrcTextFormat format)
		{
			if (format == IrcTextFormat.Split && MaxTextLength > 0 && text.Length > MaxTextLength)
			{
				int i = MaxTextLength - 1;
				while (i >= 0 && _textSplitChars.IndexOf(text[i]) < 0)
					i--;
				if (i < 0) i = MaxTextLength - 1;
				Say(to, text.Substring(0, i + 1), IrcTextFormat.Raw);
				Say(to, text.Substring(i + 1), IrcTextFormat.Split);
			}
			else
			{
				_conn.QueueMessage(new IrcMessage(null, "PRIVMSG", to, text));
			}
		}

		public void SayNotice(IrcTarget target, string text)
		{
			_conn.QueueMessage(new IrcMessage(null, "NOTICE", target.ToString(), text));
		}

		public void Quote(string rawQuote)
		{
			_conn.QueueMessage(rawQuote);
		}

		public void SignOut(string quitMessage)
		{
			_quitting = true;
			_conn.Disconnect(quitMessage);
		}

		public void SignOut()
		{
			this.SignOut(this.SignOutMessage);
		}

		public void Dispose()
		{
			if (_conn != null)
			{
				_conn.Dispose();
			}
		}
	}
}
