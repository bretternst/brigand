using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.ComponentModel;

namespace Brigand
{
	public sealed class Channels : BotModule
	{
		private List<string> _channelsToJoin = new List<string>();

		public IEnumerable<string> CurrentChannels { get { return _channelsToJoin; } }

		protected override void OnInit()
		{
			base.OnInit();
			this.Irc.Connected += new EventHandler<IrcEventArgs>(Irc_Connected);
		}

		protected override void LoadConfig(XElement moduleEl)
		{
			base.LoadConfig(moduleEl);

			_channelsToJoin = (from chanEl in moduleEl.Elements("channel")
							   where !string.IsNullOrEmpty(chanEl.Value)
							   select chanEl.Value).ToList();
		}

		protected override void SaveConfig(XElement moduleEl)
		{
			base.SaveConfig(moduleEl);

			moduleEl.Add(
				from chan in _channelsToJoin
				select new XElement("channel", chan));
		}

		private void Irc_Connected(object sender, IrcEventArgs e)
		{
			_channelsToJoin.ForEach((chan) => this.Irc.Join(chan));
		}
	}
}
