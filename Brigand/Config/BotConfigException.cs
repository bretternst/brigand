using System;

namespace Brigand
{
	public class BotConfigException : Exception
	{
		private const string BaseMessage = "A configuration error occurred: {0}";

		public BotConfigException(string message) :
			base(string.Format(BaseMessage,message))
		{
		}

		public BotConfigException(Exception innerException) :
			base(string.Format(BaseMessage,innerException.Message), innerException)
		{
		}

		public BotConfigException(string message, Exception innerException) :
			base(string.Format(BaseMessage, message), innerException)
		{
		}
	}
}
