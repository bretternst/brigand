using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Floe.Net;

namespace Brigand
{
	public sealed class RssWatcher : BotModule
	{
		private const string LinkAliasName = "link";
		private const string LinkDescName = "details";
		private const int LineLength = 420;

		private List<FeedItem> _current;
		private Dictionary<string,RssFeed> _feeds;
		private Timer _timer;
		private int _pollTime;

		[ModuleProperty("pollTime")]
		public int PollTime { get { return _pollTime; } set { _pollTime = value; if (_timer != null) { _timer.Dispose(); _timer = new Timer(this.Dispatcher, Update, _pollTime * 1000, _pollTime * 1000, null); } } }

		public IDictionary<string, RssFeed> Feeds { get { return _feeds; } }

		public RssWatcher()
		{
			_feeds = new Dictionary<string, RssFeed>();
			_current = new List<FeedItem>();
		}

		public void LinkRequest(object sender, AliasEventArgs e)
		{
			if (!e.Handled && (e.Name == LinkAliasName || e.Name == LinkDescName) && e.To.Type == IrcTargetType.Channel)
			{
				int idx = 0;
				if (e.Arguments.Count > 0)
				{
					int.TryParse(e.Arguments[0], out idx);
				}
				if (idx < 0 || idx > _current.Count - 1)
				{
					this.Irc.PrivateMessage(e.ReplyTo, "No such item exists.");
				}
				else
				{
					if (e.Name == LinkAliasName)
					{
						this.Irc.PrivateMessage(e.ReplyTo, _current[idx].Link);
					}
					else
					{
						foreach (var s in _current[idx].Description.StripHtml().WordBreak(LineLength))
						{
							this.Irc.PrivateMessage(e.ReplyTo, s);
						}
					}
				}
			}
		}

		public void AddFeed(string name, string url)
		{
			if (_feeds.ContainsKey(name))
			{
				throw new Exception("A feed with the specified name already exists.");
			}
			var feed = new RssFeed { Url = url };
			feed.CatchUp();
			_feeds.Add(name, feed);
		}

		public void RemoveFeed(string name)
		{
			if (!_feeds.ContainsKey(name))
			{
				throw new Exception("No such feed exists.");
			}
			_feeds.Remove(name);
		}

		protected override void OnInit()
		{
			base.OnInit();

			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(LinkRequest);

			foreach (var feed in _feeds.Values)
			{
				feed.SetCurrentDate();
			}
			_timer = new Timer(this.Dispatcher, Update, PollTime * 1000, PollTime * 1000, null);
		}

		protected override void LoadConfig(XElement moduleEl)
		{
			base.LoadConfig(moduleEl);

			foreach(var feedEl in moduleEl.Elements("feed"))
			{
				var feed = new RssFeed();
				BotModule.LoadProperties(feed, feedEl);
				if (_feeds.ContainsKey(feed.Name))
				{
					throw new BotConfigException(string.Format(
						"An RSS feed with the name {0} already exists.", feed.Name));
				}
				_feeds.Add(feed.Name, feed);
			}
		}

		protected override void SaveConfig(XElement moduleEl)
		{
			base.SaveConfig(moduleEl);

			foreach (var feed in _feeds.Values)
			{
				var feedEl = new XElement("feed");
				BotModule.SaveProperties(feed, feedEl);
				moduleEl.Add(feedEl);
			}
		}

		private void Update(object sender, EventArgs e)
		{
			var tempList = new List<FeedItem>();
			foreach (var name in _feeds.Keys)
			{
				var feed = _feeds[name];
				try
				{
					foreach (var item in feed.CatchUp())
					{
						string output = string.Format("[{0}({1})] {2}", name, tempList.Count.ToString(), item.Title.StripHtml());
						foreach (var chan in Channels.CurrentChannels)
						{
							foreach (var s in output.WordBreak(LineLength))
							{
								Irc.PrivateMessage(new IrcTarget(chan), s);
							}
						}
						tempList.Add(item);
					}
				}
				catch (Exception ex)
				{
					this.WriteTraceMessage(string.Format("Error polling RSS feed \"{0}\":\n{1}", name, ex.ToString()));
				}
			}
			if (tempList.Count > 0) _current = tempList;
		}
	}
}
