using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brigand
{
	public class BotConfigException : Exception
	{
		private const string BASE_MESSAGE = "A configuration error occurred: {0}";

		public BotConfigException(string message) :
			base(string.Format(BASE_MESSAGE,message))
		{
		}

		public BotConfigException(Exception innerException) :
			base(string.Format(BASE_MESSAGE,innerException.Message), innerException)
		{
		}

		public BotConfigException(string message, Exception innerException) :
			base(string.Format(BASE_MESSAGE, message), innerException)
		{
		}
	}
}
