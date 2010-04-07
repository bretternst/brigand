using System;
using System.Collections.Generic;
using System.Text;

namespace Brigand
{
	/// <summary>
	/// An interface that identifies another entity on the IRC network.
	/// </summary>
	public interface IIrcPeer
	{
		string NickUserHost { get; }
	}

	/// <summary>
	/// This class identifies a user that has sent a message.
	/// </summary>
	public class IrcPrefix : IIrcPeer
	{
		#region Private

		private string _nickname;
		private string _userName;
		private string _hostName;
		private string _nickUserHost;

		#endregion Private

		#region Public

		public string Nickname { get { return _nickname; } }
		public string UserName { get { return _userName; } }
		public string HostName { get { return _hostName; } }
		public bool IsServer { get { return _nickname == null; } }
		public string NickUserHost { get { return _nickUserHost; } }

		public IrcPrefix(string hostName, string userName, string nickname, string nickUserHost)
		{
			this._hostName = hostName;
			this._userName = userName;
			this._nickname = nickname;
			this._nickUserHost = nickUserHost;
		}

		public override string ToString()
		{
			if (IsServer)
				return _hostName;
			else
				return _nickname + "!" + _userName + "@" + _hostName;
		}

		static public IrcPrefix Parse(string profile)
		{
			string hostName = null, userName = null, nickname = null, nickUserHost = null;
			if (profile == null)
				return null;

			if (profile.IndexOf('@') > 0 && profile.IndexOf('!') > 0)
			{
				string[] s = profile.Split('@');
				hostName = s[1];
				string[] s2 = s[0].Split('!');
				userName = s2[1];
				nickname = s2[0];
				nickUserHost = profile;
			}
			else
				hostName = profile;

			return new IrcPrefix(hostName, userName, nickname, nickUserHost);
		}

		#endregion
	}

	/// <summary>
	/// This class identifies a user or channel to which a message has been sent.
	/// </summary>
	public class IrcTarget : IIrcPeer
	{
		#region Private

		private string _nickname;
		private string _channel;
		private string _userName;
		private string _hostName;
		private string _nickUserHost;

		#endregion

		#region Public

		/// <summary>
		/// Gets the nickname of the user, if the target represents a user.
		/// </summary>
		public string Nickname { get { return _nickname; } }

		/// <summary>
		/// Gets the name of the channel, if the target represents a channel.
		/// </summary>
		public string Channel { get { return _channel; } }

		/// <summary>
		/// Gets the username of the user, if the target represents a user.
		/// </summary>
		public string UserName { get { return _userName; } }

		/// <summary>
		/// Gets the hostname of a user, if the target represents a user.
		/// </summary>
		public string HostName { get { return _hostName; } }

		/// <summary>
		/// Gets a value indicating whether the target is a channel (as opposed to a user).
		/// </summary>
		public bool IsChannel { get { return _channel != null; } }

		/// <summary>
		/// Gets the full nick/user/host combination in the format nick!user@host.
		/// </summary>
		public string NickUserHost { get { return _nickUserHost; } }

		/// <summary>
		/// Initializes a target object to refer to a user.
		/// </summary>
		/// <param name="nickname">The user's nickname.</param>
		/// <param name="channel">The channel name, or null. </param>
		/// <param name="userName">The user's username, or null.</param>
		/// <param name="hostName">The user's hostname, or null.</param>
		/// <param name="nickUserHost">The user's nick/user/host string, or null.</param>
		public IrcTarget(string nickname, string userName, string hostName, string nickUserHost)
		{
			_nickname = nickname;
			_userName = userName;
			_hostName = hostName;
			_nickUserHost = nickUserHost;
		}

		/// <summary>
		/// Initializes a target to refer to a channel.
		/// </summary>
		/// <param name="channel"></param>
		public IrcTarget(string channel)
		{
			_channel = channel;
		}

		/// <summary>
		/// Initializes this object from an IrcPrefix object.
		/// </summary>
		/// <param name="prefix">The prefix object to use for initialization.</param>
		public IrcTarget(IrcPrefix prefix)
		{
			_nickname = prefix.Nickname;
			_hostName = prefix.HostName;
			_userName = prefix.UserName;
			_nickUserHost = prefix.NickUserHost;
		}

		/// <summary>
		/// Converts this object to a nick/user/host notation.
		/// </summary>
		/// <returns>A string of the format nick!user@host.</returns>
		public override string ToString()
		{
			if (IsChannel)
				return _channel;
			else if (_nickname == null && UserName == null)
				return _hostName;
			else if (_nickname != null && _userName != null && _hostName != null)
				return _nickname + "!" + _userName + "@" + _hostName;
			else
				return _nickname;
		}

		/// <summary>
		/// Parses a nick/user/host notation string.
		/// </summary>
		/// <param name="target">The raw string representing the nick/user/host.</param>
		/// <returns>A new IrcTarget object.</returns>
		static public IrcTarget Parse(string target)
		{
			if (target.StartsWith("#", StringComparison.Ordinal) || target.StartsWith("&", StringComparison.Ordinal))
				return new IrcTarget(target);
			else if (target.IndexOf("*", StringComparison.Ordinal) > 0)
				return new IrcTarget(null, null, target, null);
			else if (target.IndexOf('@') > 0 && target.IndexOf('!') > 0)
			{
				string[] s = target.Split('@');
				string hostName = s[1];
				string[] s2 = s[0].Split('!');
				string userName = s2[1];
				string nickname = s2[0];
				return new IrcTarget(nickname, userName, hostName, target);
			}
			else
				return new IrcTarget(target, null, null, null);
		}

		#endregion
	}
}
