using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;

namespace Brigand
{
	public sealed class Bot : IDisposable
	{
		private const string CONFIG_PATH = "bot.config";
		private const string MODULES_PATH = "Modules";

		private string _configPath;
		private List<BotModule> _modules = new List<BotModule>();

		public Bot() :
			this(null)
		{
		}

		public Bot(string configPath)
		{
			_configPath = configPath;
			if (string.IsNullOrEmpty(_configPath))
			{
				_configPath = CONFIG_PATH;
			}

			this.LoadModuleAssemblies();
			this.LoadConfig();
		}

		public string Name { get; set; }

		public Dispatcher Dispatcher { get; private set; }

		public Irc Irc { get; private set; }

		public Security Security { get; private set; }

		public Aliases Aliases { get; private set; }

		public Channels Channels { get; private set; }

		public Script Script { get; private set; }

		public void LoadConfig()
		{
			if (string.IsNullOrEmpty(_configPath))
				throw new BotConfigException("No configuration path was provided.");

			XDocument doc;
			try
			{
				doc = XDocument.Load(_configPath);
			}
			catch (Exception ex)
			{
				throw new BotConfigException(ex);
			}

			var root = doc.Root;
			if (root.Name != "bot")
				throw new BotConfigException("Missing 'bot' element.");

			var nameAt = root.Attribute("name");
			if (nameAt == null || string.IsNullOrEmpty(nameAt.Value))
				throw new BotConfigException("Missing 'name' attribute.");
			this.Name = nameAt.Value;

			foreach(var moduleEl in root.Elements("module"))
			{
				this.LoadModule(moduleEl);
			}

			this.Irc = this.GetModule(typeof(Irc)) as Irc;
			if (this.Irc == null)
				throw new InvalidOperationException("There must be a configured Irc module.");
			this.Security = this.GetModule(typeof(Security)) as Security;
			if (this.Security == null)
				throw new InvalidOperationException("There must be a configured Security module.");
			this.Aliases = this.GetModule(typeof(Aliases)) as Aliases;
			if (this.Aliases == null)
				throw new InvalidOperationException("There must be a configured Aliases module.");
			this.Channels = this.GetModule(typeof(Channels)) as Channels;
			if (this.Channels == null)
				throw new InvalidOperationException("There must be a configured Channels module.");
			this.Script = this.GetModule(typeof(Script)) as Script;
		}

		public void SaveConfig()
		{
			var rootEl = new XElement("bot",
				new XAttribute("name", this.Name));

			_modules.ForEach((module) =>
				{
					var moduleEl = new XElement("module",
						new XAttribute("type", module.GetType().FullName));
					module.DoSaveConfig(moduleEl);
					rootEl.Add(moduleEl);
				});

			var doc = new XDocument(rootEl);

			try
			{
				doc.Save(_configPath, SaveOptions.None);
			}
			catch (Exception ex)
			{
				throw new BotConfigException(ex);
			}
		}

		public void Run()
		{
			this.Dispatcher = new Dispatcher();

			_modules.ForEach((module) =>
				{
					module.Bot = this;
					module.DoInit();
				});

			_modules.ForEach((module) => module.DoStart());

			this.Irc.Quit += new EventHandler<IrcEventArgs>(irc_Quit);
			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(Aliases_CallAlias);
			this.Irc.Open();

			this.Dispatcher.Run();
		}

		public void Stop()
		{
			this.Dispatcher.Stop();
		}

		public BotModule GetModule(Type moduleType)
		{
			return this.GetModule(moduleType, null);
		}

		public BotModule GetModule(string name)
		{
			return this.GetModule(null, name);
		}

		public BotModule GetModule(Type moduleType, string name)
		{
			if (moduleType == null && name == null)
				throw new ArgumentException("Either moduleType or name must be supplied.");

			foreach (BotModule module in _modules)
			{
				if ((moduleType == null || module.GetType() == moduleType) &&
					(name == null || module.Name == name))
				{
					return module;
				}
			}
			Trace.WriteLine("Module [" +
				moduleType != null ? "Type=" + moduleType.FullName + (name != null ? ", " : "") : "" +
				name != null ? "Name=" + name : "" +
				"] could not be found.");

			return null;
		}

		private void LoadModuleAssemblies()
		{
			foreach (string asmFile in Directory.GetFiles(MODULES_PATH, "*.dll", SearchOption.TopDirectoryOnly))
			{
				Assembly.LoadFrom(Path.GetFullPath(asmFile));
			}
		}

		private void LoadModule(XElement moduleEl)
		{
			var typeAt = moduleEl.Attribute("type");
			if (typeAt == null || string.IsNullOrEmpty(typeAt.Value))
				throw new BotConfigException("Missing 'type' attribute.");

			string typeName = typeAt.Value;
			var type = this.GetModuleType(typeName);
			if (type == null)
				throw new BotConfigException(string.Format("Could not find type {0}.", typeName));

			BotModule module;
			try
			{
				module = Activator.CreateInstance(type) as BotModule;
			}
			catch (Exception ex)
			{
				throw new BotConfigException(ex);
			}

			if (module == null)
				throw new BotConfigException(string.Format("Type {0} does not derive from BotModule.", typeName));

			module.DoLoadConfig(moduleEl);
			_modules.Add(module);
		}

		private Type GetModuleType(string typeName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(typeName);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		private void irc_Quit(object sender, IrcEventArgs e)
		{
			this.Stop();
		}

		private void Aliases_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name.ToLower() == "quit")
			{
				Security.Demand(e.From, "bot");
				this.Stop();
			}
		}

		public void Dispose()
		{
			this.SaveConfig();

			if (this.Irc.IsConnected)
				this.Irc.SignOut();

			_modules.Reverse();
			_modules.ForEach((module) => module.DoStop());
			_modules.ForEach((module) =>
				{
					var disposable = module as IDisposable;
					if (disposable != null)
					{
						disposable.Dispose();
					}
				});
		}
	}
}
