using System.Diagnostics;

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
