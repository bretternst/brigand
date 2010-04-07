using System;
using System.Collections.Generic;
using System.Text;

namespace Brigand
{
	public class IrcEventArgs : EventArgs
	{
		internal static readonly new IrcEventArgs Empty = new IrcEventArgs();

		internal IrcEventArgs()
		{
		}
	}

	public class IrcMessageEventArgs : IrcEventArgs
	{
		private IrcMessage _message;

		public IrcMessage Message { get { return _message; } }

		internal IrcMessageEventArgs(IrcMessage message)
		{
			_message = message;
		}
	}

	public class IrcClientEventArgs : IrcMessageEventArgs
	{
		private bool _isSelf;

		public bool IsSelf { get { return _isSelf; } }

		public IrcPrefix From { get { return this.Message.From; } }

		internal IrcClientEventArgs(IrcMessage message, bool isSelf)
			: base(message)
		{
			_isSelf = isSelf;
		}
	}

	public class IrcNickEventArgs : IrcClientEventArgs
	{
		private string _newNick;

		public string NewNick { get { return _newNick; } }

		internal IrcNickEventArgs(IrcMessage message, bool isSelf, string newNick)
			: base(message, isSelf)
		{
			_newNick = newNick;
		}
	}

	public class IrcChannelEventArgs : IrcClientEventArgs
	{
		private string _channel;

		public string Channel { get { return _channel; } }

		internal IrcChannelEventArgs(IrcMessage message, bool isSelf, string channel)
			: base(message, isSelf)
		{
			_channel = channel;
		}
	}

	public class IrcTopicEventArgs : IrcChannelEventArgs
	{
		private string _topic;

		public string Topic { get { return _topic; } }

		internal IrcTopicEventArgs(IrcMessage message, bool isSelf, string channel, string topic)
			: base(message, isSelf, channel)
		{
			_topic = topic;
		}
	}

	public class IrcKickEventArgs : IrcChannelEventArgs
	{
		private IrcTarget _target;
		private bool _isToSelf;

		public IrcTarget Target { get { return _target; } }

		public bool IsToSelf { get { return _isToSelf; } }

		internal IrcKickEventArgs(IrcMessage message, bool isSelf, bool isToSelf, string channel, IrcTarget target)
			: base(message, isSelf, channel)
		{
			_isToSelf = isToSelf;
			_target = target;
		}
	}

	public class IrcChatEventArgs : IrcClientEventArgs
	{
		private IrcTarget _target;
		private string _text;
		private bool _isPrivate;

		public IrcTarget Target { get { return _target; } }

		public string Text { get { return _text; } }

		public bool IsPrivate { get { return _isPrivate; } }

		public IrcTarget ReplyTo
		{
			get
			{
				if (this.Target.IsChannel)
					return this.Target;
				else
					return new IrcTarget(this.From.Nickname, null, null, null);
			}
		}

		internal IrcChatEventArgs(IrcMessage message, bool isSelf, bool isPrivate, IrcTarget target, string text)
			: base(message, isSelf)
		{
			_target = target;
			_text = text;
			_isPrivate = isPrivate;
		}

		internal IrcChatEventArgs(IrcChatEventArgs e) : base(e.Message, e.IsSelf)
		{
			_target = e.Target;
			_text = e.Text;
		}
	}

	public class IrcModeEventArgs : IrcClientEventArgs
	{
		private IrcTarget _target;
		private string _modes;
		private string[] _modeParams;
		private bool _isToSelf;

		public IrcTarget Target { get { return _target; } }

		public string Modes { get { return _modes; } }

		public IList<string> ModeParameters { get { return _modeParams; } }

		public bool IsToSelf { get { return _isToSelf; } }

		internal IrcModeEventArgs(IrcMessage message, bool isSelf, bool isToSelf, IrcTarget target, string modes, string[] modeParams)
			: base(message, isSelf)
		{
			_isToSelf = isToSelf;
			_target = target;
			_modes = modes;
			_modeParams = modeParams;
		}
	}
}
