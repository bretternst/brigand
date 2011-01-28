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
		private const int MaxItems = 10;

		private LinkedList<FeedItem> _allItems;
		private Dictionary<string,RssFeed> _feeds;
		private Timer _timer;
		private int _pollTime;

		[ModuleProperty("pollTime")]
		public int PollTime { get { return _pollTime; } set { _pollTime = value; if (_timer != null) { _timer.Dispose(); _timer = new Timer(this.Dispatcher, Update, _pollTime * 1000, _pollTime * 1000, null); } } }

		public IDictionary<string, RssFeed> Feeds { get { return _feeds; } }

		public RssWatcher()
		{
			_feeds = new Dictionary<string, RssFeed>();
			_allItems = new LinkedList<FeedItem>();
		}

		public void LinkRequest(object sender, AliasEventArgs e)
		{
			if (e.Name == LinkAliasName || e.Name == LinkDescName)
			{
				int idx = 0;
				if (e.Arguments.Count > 0)
				{
					int.TryParse(e.Arguments[0], out idx);
				}
				if (idx < 0 || idx > _allItems.Count - 1)
				{
					this.Irc.PrivateMessage(e.ReplyTo, "No such item exists.");
				}
				else
				{
					var item = _allItems.Last;
					while (--idx >= 0)
					{
						item = item.Previous;
					}

					if (e.Name == LinkAliasName)
					{
						this.Irc.PrivateMessage(e.ReplyTo, item.Value.Link);
					}
					else
					{
						foreach (var s in item.Value.Description.StripHtml().WordBreak(LineLength))
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
				try
				{
					feed.CatchUp();
					_feeds.Add(feed.Name, feed);
				}
				catch (Exception ex)
				{
					throw new BotConfigException(string.Format(
						"An RSS feed with the name {0} could not be loaded: {1}", feed.Name, ex.Message));
				}
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
			foreach (var name in _feeds.Keys)
			{
				var feed = _feeds[name];
				try
				{
					foreach (var item in feed.CatchUp())
					{
						string output = string.Format("[{0}] {1}", name, item.Title.StripHtml());
						foreach (var chan in Channels.CurrentChannels)
						{
							foreach (var s in output.WordBreak(LineLength))
							{
								Irc.PrivateMessage(new IrcTarget(chan), s);
							}
						}

						while (_allItems.Count >= MaxItems)
						{
							_allItems.RemoveFirst();
						}
						_allItems.AddLast(item);
					}
				}
				catch (Exception ex)
				{
					this.WriteTraceMessage(string.Format("Error polling RSS feed \"{0}\":\n{1}", name, ex.ToString()));
				}
			}
		}
	}
}
