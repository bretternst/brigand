using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

using Floe.Net;

namespace Brigand
{
	public sealed class Bot : IDisposable
	{
		private const string DefaultConfigPath = "bot.config";
		private const string ModulesPath = "Modules";

		private string _configPath;
		private List<BotModule> _modules = new List<BotModule>();
		private List<string> _startup = new List<string>();

		public Bot() :
			this(null)
		{
		}

		public Bot(string configPath)
		{
			_configPath = configPath;
			if (string.IsNullOrEmpty(_configPath))
			{
				_configPath = DefaultConfigPath;
			}

			this.LoadModuleAssemblies();
			this.LoadConfig();
		}

		[ModuleProperty("name")]
		public string Nickname { get; set; }

		[ModuleProperty("server")]
		public string Server { get; set; }

		[ModuleProperty("port")]
		public int Port { get; set; }

		[ModuleProperty("username")]
		public string Username { get; set; }

		[ModuleProperty("fullName")]
		public string FullName { get; set; }

		[ModuleProperty("localHost")]
		public string LocalHost { get; set; }

		[ModuleProperty("autoReconnect")]
		public bool AutoReconnect { get; set; }

		public Dispatcher Dispatcher { get; private set; }

		public IrcSession Irc { get; private set; }

		public Security Security { get; private set; }

		public Aliases Aliases { get; private set; }

		public Channels Channels { get; private set; }

		public void LoadConfig()
		{
			if (string.IsNullOrEmpty(_configPath))
			{
				throw new BotConfigException("No configuration path was provided.");
			}

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
			{
				throw new BotConfigException("Missing 'bot' element.");
			}

			BotModule.LoadProperties(this, root);

			foreach (var startupEl in root.Elements("startup"))
			{
				_startup.Add(startupEl.Value);
			}

			foreach(var moduleEl in root.Elements("module"))
			{
				this.LoadModule(moduleEl);
			}

			this.Irc = new IrcSession();
			BotModule.LoadProperties(this.Irc, root);

			this.Security = this.GetModule(typeof(Security)) as Security;
			if (this.Security == null)
			{
				throw new InvalidOperationException("There must be a configured Security module.");
			}
			this.Aliases = this.GetModule(typeof(Aliases)) as Aliases;
			if (this.Aliases == null)
			{
				throw new InvalidOperationException("There must be a configured Aliases module.");
			}
			this.Channels = this.GetModule(typeof(Channels)) as Channels;
			if (this.Channels == null)
			{
				throw new InvalidOperationException("There must be a configured Channels module.");
			}
		}

		public void SaveConfig()
		{
			var rootEl = new XElement("bot");
			BotModule.SaveProperties(this, rootEl);

			_startup.ForEach((startup) =>
				{
					var startupEl = new XElement("startup", startup);
					rootEl.Add(startupEl);
				});

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
			System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(this.Dispatcher));

			this.Irc.StateChanged += new EventHandler<EventArgs>(Irc_StateChanged);

			_modules.ForEach((module) =>
				{
					module.Bot = this;
					module.DoInit();
				});

			_modules.ForEach((module) => module.DoStart());

			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(Aliases_CallAlias);
			this.Irc.Open(this.Server, this.Port, false, this.Nickname, this.Username, this.FullName, true);
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
			foreach (string asmFile in Directory.GetFiles(ModulesPath, "*.dll", SearchOption.TopDirectoryOnly))
			{
				Assembly.LoadFrom(Path.GetFullPath(asmFile));
			}
		}

		private void LoadModule(XElement moduleEl)
		{
			var typeAt = moduleEl.Attribute("type");
			if (typeAt == null || string.IsNullOrEmpty(typeAt.Value))
			{
				throw new BotConfigException("Missing 'type' attribute.");
			}

			string typeName = typeAt.Value;
			var type = this.GetModuleType(typeName);
			if (type == null)
			{
				throw new BotConfigException(string.Format("Could not find type {0}.", typeName));
			}

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
			{
				throw new BotConfigException(string.Format("Type {0} does not derive from BotModule.", typeName));
			}

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

		private void Irc_StateChanged(object sender, EventArgs e)
		{
			if (this.Irc.State == IrcSessionState.Disconnected)
			{
				this.Stop();
			}
			else if (this.Irc.State == IrcSessionState.Connected)
			{
				foreach (var s in _startup)
				{
					this.Irc.Send(s);
				}
			}
		}

		private void Aliases_CallAlias(object sender, AliasEventArgs e)
		{
			if (e.Name.ToLower() == "quit")
			{
				try
				{
					Security.Demand(e.From, "bot");
				}
				catch (System.Security.SecurityException)
				{
					return;
				}
				this.Stop();
			}
		}

		public void Dispose()
		{
			this.SaveConfig();

			if (this.Irc.State == IrcSessionState.Connected)
				this.Irc.Quit("Shutting down");

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
