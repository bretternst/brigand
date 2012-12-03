using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Brigand
{
	public class DispatcherSynchronizationContext : SynchronizationContext
	{
		private Dispatcher _dispatcher;

		public DispatcherSynchronizationContext(Dispatcher dispatcher)
		{
			_dispatcher = dispatcher;
		}

		public override void Post(SendOrPostCallback d, object state)
		{
			_dispatcher.BeginInvoke(() => d.Invoke(state));
		}

		public override void Send(SendOrPostCallback d, object state)
		{
			throw new NotImplementedException();
		}
	}
}
