using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Floe.Net;
using Mono.CSharp;

namespace Brigand
{
	public class Script : BotModule
	{
        private StringBuilder errOut;

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

            errOut = new StringBuilder();
            Evaluator.MessageOutput = new StringWriter(errOut);
            Evaluator.Init(new string[] { });

			foreach (string script in this.LoadScripts)
			{
				try
				{
                    Evaluator.Evaluate(File.ReadAllText(script));
				}
				catch (Exception ex)
				{
					throw new BotConfigException(string.Format(
						"Error executing startup script {0}: {1}", script, ex.Message), ex);
				}
			}

			this.Irc.PrivateMessaged += new EventHandler<IrcMessageEventArgs>(Irc_PrivateMessage);
		}

		private void Execute(IrcTarget replyTo, string line)
		{
			object output = null;

            line = line.Trim();
            if (!line.EndsWith(";"))
                line = line + ";";

            try {
                output = Evaluator.Evaluate(line);
            }
            catch (Exception ex) {
                this.Irc.PrivateMessage(replyTo, string.Format("ERROR: {0}", ex.Message));
            }

            if (errOut.Length > 0) {
                this.Irc.PrivateMessage(replyTo, string.Format("ERROR: {0}", errOut.ToString()));
                errOut.Clear();
            }

			if (output != null)
			{
				this.Irc.PrivateMessage(replyTo, output.ToString());
			}
		}

		private void Irc_PrivateMessage(object sender, IrcMessageEventArgs e)
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

				this.Execute(e.To.IsChannel ? e.To : new IrcTarget(e.From), line.Substring(this.Prefix.Length));
			}
		}
	}
}
