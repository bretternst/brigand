using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Floe.Net;

namespace Brigand
{
	public abstract class BotModule
	{
		private Bot _bot;

		[ModuleProperty("name")]
		public string Name { get; protected set; }

		public Bot Bot { get { return _bot; } internal set { _bot = value; } }
		public Dispatcher Dispatcher { get { return _bot.Dispatcher; } }
		public IrcSession Irc { get { return this.Bot.Irc; } }
		public Security Security { get { return this.Bot.Security; } }
		public Channels Channels { get { return this.Bot.Channels; } }
		public Aliases Aliases { get { return this.Bot.Aliases; } }

		public static void LoadProperties(object obj, XElement configEl)
		{
			foreach (var property in obj.GetType().GetProperties(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				var attr = property.GetCustomAttributes(
					typeof(ModulePropertyAttribute), true).OfType<ModulePropertyAttribute>().FirstOrDefault();
				if (attr != null)
				{
					var configAt = configEl.Attribute(attr.ConfigName);
					if (configAt != null)
					{
						try
						{
							var converter = BotModule.GetTypeConverter(property);
							property.SetValue(obj, converter.ConvertFromString(configAt.Value), null);
						}
						catch (Exception ex)
						{
							throw new BotConfigException(string.Format(
								"Could not load configuration property {0}", attr.ConfigName), ex);
						}
					}
				}
			}
		}

		public static void SaveProperties(object obj, XElement configEl)
		{
			foreach (var property in obj.GetType().GetProperties(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				var attr = property.GetCustomAttributes(
					typeof(ModulePropertyAttribute), true).OfType<ModulePropertyAttribute>().FirstOrDefault();
				if (attr != null)
				{
					try
					{
						object val = property.GetValue(obj, null);
						if (val != null)
						{
							var converter = BotModule.GetTypeConverter(property);
							configEl.Add(new XAttribute(attr.ConfigName, converter.ConvertToString(val)));
						}
					}
					catch (Exception ex)
					{
						throw new BotConfigException(string.Format(
							"Could not save configuration property {0}", attr.ConfigName), ex);
					}
				}
			}
		}

		protected void WriteTraceMessage(string message)
		{
			string msg = string.Format(System.Globalization.CultureInfo.InvariantCulture,
				"[{0}] {1}", String.IsNullOrEmpty(this.Name) ? this.GetType().Name : this.Name, message);
			Trace.WriteLine(msg);
			Debug.WriteLine(msg);
		}

		protected virtual void OnInit()
		{
			if (_bot == null)
				throw new InvalidOperationException();
			this.WriteTraceMessage("Init");
		}

		protected virtual void OnStart()
		{
			if (_bot == null)
				throw new InvalidOperationException();

			this.WriteTraceMessage("Start");
		}

		protected virtual void LoadConfig(XElement moduleEl)
		{
			BotModule.LoadProperties(this, moduleEl);
		}

		protected virtual void SaveConfig(XElement moduleEl)
		{
			BotModule.SaveProperties(this, moduleEl);
		}

		protected virtual void OnStop()
		{
			this.WriteTraceMessage("Stop");
		}

		private static TypeConverter GetTypeConverter(PropertyInfo property)
		{
			var attr = property.GetCustomAttributes(
				typeof(TypeConverterAttribute), true).OfType<TypeConverterAttribute>().FirstOrDefault();
			if (attr != null)
			{
				var type = Type.GetType(attr.ConverterTypeName, false);
				if (type == null)
				{
					throw new BotConfigException(string.Format("Cannot find type {0}.", attr.ConverterTypeName));
				}
				return Activator.CreateInstance(type) as TypeConverter;
			}
			else
			{
				return TypeDescriptor.GetConverter(property.PropertyType);
			}
		}

		internal void DoInit()
		{
			this.OnInit();
		}

		internal void DoStart()
		{
			this.OnStart();
		}

		internal void DoStop()
		{
			this.OnStop();
		}

		internal void DoLoadConfig(XElement moduleEl)
		{
			this.LoadConfig(moduleEl);
		}

		internal void DoSaveConfig(XElement moduleEl)
		{
			this.SaveConfig(moduleEl);
		}
	}
}
