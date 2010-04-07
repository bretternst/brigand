using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brigand
{
	public sealed class Timer : IDisposable
	{
		private System.Threading.Timer _timer;

		public Timer(Dispatcher disp, EventHandler handler, int delay, int interval, object state)
		{
			_timer = new System.Threading.Timer((o) =>
			{
				disp.Invoke((Action)(() =>
				{
					try
					{
						handler(null, new CallbackEventArgs(state));
					}
					catch (SystemException ex)
					{
						System.Diagnostics.Trace.WriteLine(ex.ToString());
					}
				}));
			}, null, delay, interval);
		}

		public Timer(Dispatcher disp, EventHandler handler, int delay, object state)
			: this(disp, handler, delay, System.Threading.Timeout.Infinite, state)
		{
		}

		public void Cancel()
		{
			if (_timer != null)
			{
				_timer.Dispose();
				_timer = null;
			}
		}

		public void Dispose()
		{
			if (_timer != null)
			{
				_timer.Dispose();
			}
		}
	}

	public class CallbackEventArgs : EventArgs
	{
		private object _state;

		public object State { get { return _state; } }

		public CallbackEventArgs(object state)
			: base()
		{
			_state = state;
		}

		public CallbackEventArgs()
			: base()
		{
		}
	}
}
