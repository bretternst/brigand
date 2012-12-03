using System;
using System.Collections.Generic;
using System.Threading;

namespace Brigand
{
	public class Dispatcher
	{
		private Queue<Action> _actions;
		private AutoResetEvent _waitHandle;
		private volatile bool _running;

		public Dispatcher()
		{
			_actions = new Queue<Action>();
			_waitHandle = new AutoResetEvent(false);
		}

		public void BeginInvoke(Action action)
		{
			lock (_actions)
			{
				_actions.Enqueue(action);
			}
			_waitHandle.Set();
		}

		public void Run()
		{
			_running = true;

			while (_running)
			{
				lock (_actions)
				{
					while (_actions.Count > 0)
					{
						var action = _actions.Dequeue();
						action();
					}
				}
				_waitHandle.WaitOne();
			}
		}

		public void Stop()
		{
			_running = false;
			_waitHandle.Set();
		}
	}
}
