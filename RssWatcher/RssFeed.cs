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
		private List<FeedItem> items;
		private DateTime updated;

		[ModuleProperty("name")]
		public string Name { get; private set; }

		[ModuleProperty("url")]
		public string Url { get; set; }

		public DateTime UpdatedDate { get { return updated; } }

		public ICollection<FeedItem> Items { get { return items; } }

		public RssFeed()
		{
			items = new List<FeedItem>();
		}

		public void Query()
		{
			items.Clear();
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
						items.Add(new FeedItem(itemEl));
					}
				}
				catch (NullReferenceException)
				{
					throw new Exception("Malformed feed XML.");
				}
			}
		}

		public void SetCurrentDate()
		{
			updated = DateTime.Now;
		}

		public IEnumerable<FeedItem> CatchUp()
		{
			Query();
			var readTo = updated;
			if (items.Count > 0)
			{
				updated = (from item in items select item.PublishDate).Max();
			}
			return (from item in items where item.PublishDate > readTo select item);
		}
	}
}
