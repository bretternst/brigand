using System;
using System.Collections.Generic;
using System.Net;
using System.Web;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Brigand
{
	public class UrbanDictionary : ITriviaProvider
	{
		private const string UrlRandom = "http://api.urbandictionary.com/v0/random";
		private const string UrlDef = "http://api.urbandictionary.com/v0/define?term={0}";
		private const int MaxDefSize = 256;

		private List<string> _defs;
		private int _currentHint;

		public int QuestionId { get; private set; }

		public string QuestionText { get; private set; }

		public string PrimaryAnswer { get; private set; }

		public bool HasHints
		{
			get
			{
				if (_currentHint >= _defs.Count) return false;

				return true;
			}
		}

		public UrbanDictionary()
		{
			_defs = new List<string>();
			QuestionText = null;
			PrimaryAnswer = null;
		}

		public void NextQuestion()
		{
			var req = HttpWebRequest.Create(UrlRandom);
			var res = req.GetResponse();
			var reader = new StreamReader(res.GetResponseStream());
			var obj = JObject.ReadFrom(new JsonTextReader(reader));

			var word = (string)((JObject)((JArray)obj["list"]).First).Property("word").Value;
			req = HttpWebRequest.Create(string.Format(UrlDef, HttpUtility.UrlEncode(word)));
			res = req.GetResponse();
			reader = new StreamReader(res.GetResponseStream());
			obj = JObject.ReadFrom(new JsonTextReader(reader));

			_defs.Clear();
			this.PrimaryAnswer = "";
			_currentHint = 0;

			foreach(var i in (JArray)obj["list"]) {
				_defs.Add((string)((JObject)i).Property("definition").Value);
			}

			QuestionId = PrimaryAnswer.GetHashCode();

			// the questionText should be the first definition, so remove that from the definitions array
			QuestionText = _defs[0];
			_defs.RemoveAt(0);
		}

		public string NextHint()
		{
			// you're a bad man and should have checked this first
			if (!HasHints)
			{
				return null;
			}

			_currentHint++;

			// return the previous comment
			return _defs[_currentHint - 1];
		}

		public bool CheckAnswer(string candidate)
		{
			if (candidate.ToLower() == PrimaryAnswer.ToLower())
			{
				return true;
			}

			return false;
		}
	}
}
