using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;

using Brigand;

namespace Brigand
{
	class Program
	{
		private static void Main(string[] args)
		{
			ConsoleTraceListener traceListener = new ConsoleTraceListener();
			Trace.Listeners.Add(traceListener);

			string configPath = null;
			if (args.Length > 0)
			{
				configPath = args[0];
			}

			using (var bot = new Bot(configPath))
			{
				bot.Run();
			}
		}
	}
}
