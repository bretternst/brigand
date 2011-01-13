using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace Brigand
{
	public sealed class FeedItem
	{
		public string Title { get; private set; }
		public string Link { get; private set; }
		public string Description { get; private set; }
		public DateTime PublishDate { get; private set; }

		public FeedItem(XElement el)
		{
			var titleEl = el.Element("title");
			var pubDateEl = el.Element("pubDate");
			var linkEl = el.Element("link");
			var descEl = el.Element("description");
			if (titleEl == null || pubDateEl == null) throw new Exception("Malformed feed item.");
			Title = titleEl.Value;
			PublishDate = DateTime.Parse(pubDateEl.Value);
			Link = linkEl == null ? "" : linkEl.Value;
			Description = descEl == null ? "" : descEl.Value;
		}
	}

	public sealed class RssFeed
	{
		private List<FeedItem> _items;

		[ModuleProperty("name")]
		public string Name { get; private set; }

		[ModuleProperty("url")]
		public string Url { get; set; }

		public DateTime UpdatedDate { get; private set; }

		public ICollection<FeedItem> Items { get { return _items; } }

		public RssFeed()
		{
			_items = new List<FeedItem>();
		}

		public void Query()
		{
			_items.Clear();
			using (var request = new WebClient())
			{
				request.Encoding = System.Text.Encoding.UTF8;
				string response = request.DownloadString(Url);

				try
				{
					var doc = XDocument.Parse(response);
					if (doc.Root.Name.LocalName.ToLower() == "rdf")
					{
						doc = new XDocument(new XElement("rss", new XElement("channel",
							from item in doc.Root.Elements().Where((el) => el.Name.LocalName == "item")
							select new XElement("item",
								new XElement("pubDate", item.Elements().Where((el) => el.Name.LocalName == "date").First().Value),
								new XElement("title", item.Elements().Where((el) => el.Name.LocalName == "title").First().Value),
								new XElement("description", item.Elements().Where((el) => el.Name.LocalName == "description").First().Value),
								new XElement("link", item.Elements().Where((el) => el.Name.LocalName == "link").First().Value)))));
					}

					foreach (var itemEl in doc.Root.Element("channel").Elements("item"))
					{
						_items.Add(new FeedItem(itemEl));
					}
				}
				catch (NullReferenceException)
				{
					throw new Exception("Malformed feed XML.");
				}
			}
		}

		public IEnumerable<FeedItem> CatchUp()
		{
			this.Query();
			var readTo = this.UpdatedDate;
			if (_items.Count > 0)
			{
				this.UpdatedDate = (from item in _items select item.PublishDate).Max();
			}
			else
			{
				this.UpdatedDate = DateTime.Now;
			}
			return (from item in _items where item.PublishDate > readTo select item);
		}
	}
}
