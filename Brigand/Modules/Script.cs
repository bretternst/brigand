using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
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

			this.Irc.PrivateMessage += new EventHandler<IrcChatEventArgs>(Irc_PrivateMessage);
		}

		protected override void OnStop()
		{
			_engine.Dispose();

			base.OnStop();
		}

		private void Irc_PrivateMessage(object sender, IrcChatEventArgs e)
		{
			if (e.IsSelf)
			{
				return;
			}

			string line = e.Text.Trim();
			if(line.StartsWith(this.Prefix) && line.Length > this.Prefix.Length)
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

				this.Execute(e.ReplyTo, line.Substring(this.Prefix.Length));
			}
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
					this.Irc.Say(replyTo, string.Format("ERROR: {0}", ex.Message));
				}
			}
			catch (Exception ex)
			{
				this.Irc.Say(replyTo, string.Format("ERROR: {0}", ex.Message));
			}

			if (output != null)
			{
				this.Irc.Say(replyTo, output.ToString());
			}
		}
	}
}
