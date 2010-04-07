using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Security;
using System.Xml.Linq;
using System.Globalization;

namespace Brigand
{
	public sealed class Security : BotModule
	{
		private List<User> _users = new List<User>();
		private Dictionary<string, User> _userIndex = new Dictionary<string,User>();

		public User this[string name]
		{
			get
			{
				if (_userIndex.ContainsKey(name))
				{
					return _userIndex[name];
				}
				else
				{
					return null;
				}
			}
		}

		public User Recognize(IIrcPeer peer)
		{
			if (string.IsNullOrEmpty(peer.NickUserHost))
			{
				return null;
			}

			foreach (User user in _users)
			{
				if (Regex.IsMatch(peer.NickUserHost, "^" + user.Mask + "$"))
				{
					return user;
				}
			}
			return null;
		}

		public void Demand(IIrcPeer peer, params string[] permissions)
		{
			if (permissions == null || permissions.Length == 0)
			{
				throw new ArgumentException("At least one permission must be specified.");
			}

			if (permissions.Length == 1 && permissions[0] == "?")
			{
				return;
			}

			User user = this.Recognize(peer);
			if (user == null)
			{
				throw new SecurityException(string.Format(CultureInfo.InvariantCulture,
					"Permission denied for {0} to {1}", string.Join(",", permissions), peer.NickUserHost));
			}

			Demand(user, permissions);
		}

		public static void Demand(User user, params string[] permissions)
		{
			if (user == null)
			{
				throw new ArgumentNullException("user");
			}
			if (!user.HasPermissions(permissions))
			{
				throw new SecurityException(string.Format(CultureInfo.InvariantCulture,
					"Permission denied for {0} to {1}", string.Join(",", permissions), user.Mask));
			}
		}

		public void AddUser(string name, string mask, params string[] permissions)
		{
			if (_userIndex.ContainsKey(name))
			{
				throw new Exception(string.Format("User {0} already exists.", name));
			}

			User newUser = new User(name, mask, permissions);
			newUser.Permissions.UnionWith(permissions);
			_users.Add(newUser);
			_userIndex.Add(newUser.Name, newUser);
		}

		public bool RemoveUser(string name)
		{
			if (_userIndex.ContainsKey(name))
			{
				_users.Remove(_userIndex[name]);
				_userIndex.Remove(name);
				return true;
			}
			return false;
		}

		protected override void LoadConfig(System.Xml.Linq.XElement moduleEl)
		{
			base.LoadConfig(moduleEl);

			foreach (var userEl in moduleEl.Elements("user"))
			{
				var user = new User();
				BotModule.LoadProperties(user, userEl);
				if (_userIndex.ContainsKey(user.Name))
				{
					throw new BotConfigException(string.Format(
						"There are multiple users with the name {0}.", user.Name));
				}
				_users.Add(user);
				_userIndex.Add(user.Name, user);
			}
		}

		protected override void SaveConfig(System.Xml.Linq.XElement moduleEl)
		{
			base.SaveConfig(moduleEl);

			foreach (var user in _users)
			{
				var userEl = new XElement("user");
				BotModule.SaveProperties(user, userEl);
				moduleEl.Add(userEl);
			}
		}
	}

	public class User
	{
		internal User()
		{
		}

		public User(string name, string mask, params string[] permissions)
		{
			this.Name = name;
			this.Mask = mask;
			this.Permissions = new HashSet<string>(permissions);
		}

		[ModuleProperty("name")]
		public string Name { get; private set; }

		[ModuleProperty("mask")]
		public string Mask { get; set; }

		[ModuleProperty("permissions")]
		[TypeConverter(typeof(HashSetOfStringTypeConverter))]
		public HashSet<string> Permissions { get; private set; }

		public bool HasPermissions(params string[] permissions)
		{
			if (this.Permissions.Contains("*") ||
				(permissions.Length == 1 && permissions[0] == "*"))
			{
				return true;
			}
			if (permissions.Length == 0 ||
				(permissions.Length == 1 && string.IsNullOrEmpty(permissions[0])))
			{
				return false;
			}
			HashSet<string> perms = new HashSet<string>(permissions);
			return perms.IsSubsetOf(this.Permissions);
		}
	}
}
