using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Floe.Net;
using IronPython.Hosting;

namespace Brigand
{
	public class Script : BotModule
	{
		private PythonEngine _engine;

		[ModuleProperty("prefix")]
		public string Prefix { get; set; }

		[ModuleProperty("maxOutputLines")]
		public int MaxOutputLines { get; set; }

		[ModuleProperty("executePermission")]
		public string ExecutePermission { get; set; }

		public IList<string> LoadScripts { get; private set; }

		protected override void LoadConfig(XElement moduleEl)
		{
			base.LoadConfig(moduleEl);

			this.LoadScripts = (from loadEl in moduleEl.Elements("load")
								select loadEl.Value).ToList();

			if (this.MaxOutputLines < 1)
			{
				throw new BotConfigException("The maxOutputLines value must be >= 1.");
			}
			if (string.IsNullOrEmpty(this.Prefix))
			{
				throw new BotConfigException("No prefix value was supplied.");
			}
			if (string.IsNullOrEmpty(this.ExecutePermission))
			{
				throw new BotConfigException("No executePermission value was supplied.");
			}
		}

		protected override void SaveConfig(XElement moduleEl)
		{
			base.SaveConfig(moduleEl);

			moduleEl.Add(from load in this.LoadScripts
						 select new XElement("load", load));
		}

		protected override void OnStart()
		{
			base.OnStart();

			_engine = new PythonEngine();
			_engine.Globals.Add("bot", this.Bot);

			foreach (string script in this.LoadScripts)
			{
				try
				{
					_engine.ExecuteFile(script);
				}
				catch (Exception ex)
				{
					throw new BotConfigException(string.Format(
						"Error executing startup script {0}: {1}", script, ex.Message), ex);
				}
			}

			this.Irc.PrivateMessaged += new EventHandler<IrcDialogEventArgs>(Irc_PrivateMessage);
			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(Aliases_CallAlias);
		}

		protected override void OnStop()
		{
			_engine.Dispose();

			base.OnStop();
		}

		private void Execute(IrcTarget replyTo, string line)
		{
			object output = null;

			try
			{
				output = _engine.Evaluate(line);
			}
			catch (IronPython.Runtime.Exceptions.PythonSyntaxErrorException)
			{
				try
				{
					_engine.Execute(line);
				}
				catch (Exception ex)
				{
					this.Irc.PrivateMessage(replyTo, string.Format("ERROR: {0}", ex.Message));
				}
			}
			catch (Exception ex)
			{
				this.Irc.PrivateMessage(replyTo, string.Format("ERROR: {0}", ex.Message));
			}

			if (output != null)
			{
				this.Irc.PrivateMessage(replyTo, output.ToString());
			}
		}

		private void Load(string location)
		{
			string scriptCode = "";

			location = location.Trim();
			if (location.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
				location.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
			{
				var client = new System.Net.WebClient();
				string content = client.DownloadString(location);

				try
				{
					content = content.Trim();
					if (content.StartsWith("<!DOCTYPE"))
					{
						int firstCloseBracket = content.IndexOf('>');
						if (firstCloseBracket >= 0 && content.Length > firstCloseBracket + 1)
						{
							content = content.Substring(firstCloseBracket + 1);
						}
					}
					var doc = XDocument.Parse(content);
					var preEl = doc.Descendants(doc.Root.Name.Namespace + "pre").FirstOrDefault();
					if (preEl == null)
					{
						throw new Exception("Could not locate PRE element in the specified document.");
					}
					scriptCode = preEl.Value;
				}
				catch(System.Xml.XmlException)
				{
					scriptCode = content;
				}
			}
			else
			{
				scriptCode = File.ReadAllText(location);
			}

			_engine.Execute(scriptCode);
		}

		private void Irc_PrivateMessage(object sender, IrcDialogEventArgs e)
		{
			if (string.Compare(e.From.Nickname, this.Irc.Nickname, true) == 0)
			{
				return;
			}

			string line = e.Text.Trim();
			if (line.StartsWith(this.Prefix) && line.Length > this.Prefix.Length)
			{
				try
				{
					this.Security.Demand(e.From, this.ExecutePermission);
				}
				catch (System.Security.SecurityException)
				{
					// Just ignore unauthorized commands.
					return;
				}

				this.Execute(e.To.Type == IrcTargetType.Channel ? e.To : new IrcTarget(e.From), line.Substring(this.Prefix.Length));
			}
		}

		private void Aliases_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name == "load")
			{
				try
				{
					this.Security.Demand(e.From, this.ExecutePermission);
				}
				catch (System.Security.SecurityException)
				{
					return;
				}

				if (e.Arguments.Count == 1)
				{
					try
					{
						this.Load(e.Arguments[0]);
					}
					catch (Exception ex)
					{
						this.Irc.PrivateMessage(e.ReplyTo, string.Format("ERROR: {0}", ex.Message));
					}
				}
				else
				{
					this.Irc.PrivateMessage(e.ReplyTo, "Usage: !load <url|path>");
				}
			}
		}
	}
}
