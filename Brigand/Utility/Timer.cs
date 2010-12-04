using System;

namespace Brigand
{
	#region CallbackEventArgs class

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
	
	#endregion

	public sealed class Timer : IDisposable
	{
		private Dispatcher _dispatcher;
		private EventHandler _handler;
		private object _state;
		private System.Threading.Timer _timer;

		public Timer(Dispatcher disp, EventHandler handler, int delay, int interval, object state)
		{
			_dispatcher = disp;
			_handler = handler;
			_state = state;

			_timer = new System.Threading.Timer((o) =>
			{
				var timer = o as Timer;
				timer._dispatcher.BeginInvoke((Action)(() =>
				{
					try
					{
						timer._handler(null, new CallbackEventArgs(timer._state));
					}
					catch (SystemException ex)
					{
						System.Diagnostics.Trace.WriteLine(ex.ToString());
					}
				}));
			}, this, delay, interval);
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
}
