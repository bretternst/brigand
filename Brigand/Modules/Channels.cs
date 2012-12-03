using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Floe.Net;

namespace Brigand
{
	public sealed class Channels : BotModule
	{
		private List<string> _channelsToJoin = new List<string>();

		public IEnumerable<string> CurrentChannels { get { return _channelsToJoin; } }

		protected override void OnInit()
		{
			base.OnInit();
			this.Irc.StateChanged += new EventHandler<EventArgs>(Irc_StateChanged);
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

		private void Irc_StateChanged(object sender, EventArgs e)
		{
			if (this.Irc.State == IrcSessionState.Connected)
			{
				_channelsToJoin.ForEach((chan) => this.Irc.Join(chan));
			}
		}
	}
}
