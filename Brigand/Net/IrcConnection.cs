using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace Brigand
{
	internal sealed class IrcConnection : IDisposable
	{
		private static Regex _sendFilter = new Regex("[\u000a\u000d]",RegexOptions.Compiled);

		private Dispatcher _dispatcher;
		private string _server;
		private int _port;
		private string _nickname;
		private string _userName;
		private string _fullName;
		private string _localhost;
		private TcpClient _tcpClient;
		private NetworkStream _stream;
		private StreamReader _rstream;
		private StreamWriter _wstream;
		private bool _connected;
		private Queue<IrcMessage> _sendQueue;
		private System.Threading.Timer _sendTimer;
		private int _sendPace = 250;
		private DateTime _lastSendTime = DateTime.MinValue;
		private Thread _listenThread;

		public bool IsConnected { get { return _connected; } }

		public event EventHandler<IrcEventArgs> Connected;

		public event EventHandler<IrcEventArgs> Disconnected;

		public event EventHandler<IrcMessageEventArgs> MessageSent;

		public event EventHandler<IrcMessageEventArgs> MessageReceived;

		public event ErrorEventHandler Error;

		public IrcConnection(Dispatcher dispatcher, string server, int port, string nickname, string userName, string fullName, string localhost)
		{
			if (string.IsNullOrEmpty(server))
				throw new ArgumentNullException("server");
			if (port <= 0 || port > 65535)
				throw new ArgumentOutOfRangeException("port");
			if (string.IsNullOrEmpty(nickname))
				throw new ArgumentNullException("nickname");

			_dispatcher = dispatcher;
			_server = server;
			_port = port;
			_nickname = nickname;
			_userName = userName;
			_fullName = fullName;
			_localhost = localhost;

			Reset();
		}

		public void Open()
		{
			_tcpClient = new TcpClient();

			if (_connected)
				throw new InvalidOperationException("The instance of IrcConnection is already connected.");

			_tcpClient.Connect(_server, _port);

			_stream = _tcpClient.GetStream();
			_rstream = new StreamReader(_stream, Encoding.ASCII);
			_wstream = new StreamWriter(_stream, Encoding.ASCII);

			_listenThread = new Thread(new ThreadStart(this.MessageLoop));
			_listenThread.Start();
		}

		public void Disconnect(string quitMessage)
		{
			QueueMessage(new IrcMessage(null, "QUIT", quitMessage));
		}

		public void QueueMessage(string message)
		{
			QueueMessage(IrcMessage.Parse(message));
		}

		public void QueueMessage(IrcMessage message)
		{
			if (_sendQueue.Count > 0)
			{
				_sendQueue.Enqueue(message);
			}
			else
			{
				TimeSpan ts = DateTime.Now - _lastSendTime;
				if (ts.TotalMilliseconds < _sendPace)
				{
					_sendQueue.Enqueue(message);
					_sendTimer.Change(_sendPace - (int)ts.TotalMilliseconds, _sendPace);
				}
				else
				{
					SendMessage(message);
				}
			}
		}

		public void Dispose()
		{
			if (_rstream != null)
			{
				_rstream.Dispose();
			}
			if (_wstream != null)
			{
				_wstream.Dispose();
			}
			if (_tcpClient != null && _tcpClient.Connected)
			{
				_tcpClient.Close();
			}
			if (_sendTimer != null)
			{
				_sendTimer.Dispose();
			}
		}

		private void SendMessage(IrcMessage message)
		{
			string msg = _sendFilter.Replace(message.ToString(), "\uffff");
			_wstream.WriteLine(msg);
			_wstream.Flush();
			_lastSendTime = DateTime.Now;
			OnMessageSent(message);
		}

		private IrcMessage RecvMessage()
		{
			IrcMessage msg = null;
			string s = _rstream.ReadLine();
			if (s != null && s.Length > 0)
				msg = IrcMessage.Parse(s);
			else
				return null;
			return msg;
		}

		private void ProcessMessage(IrcMessage msg)
		{
			try
			{
				OnMessageReceived(msg);
			}
			catch (Exception ex)
			{
				string errMsg = "Unhandled exception thrown by module: " +
					Environment.NewLine +
					ex.ToString();
				System.Diagnostics.Trace.WriteLine(errMsg);
				System.Diagnostics.Debug.WriteLine(errMsg);
				throw;
			}

			if (msg.Command == "ERROR")
				_tcpClient.Close();
		}

		private void MessageLoop()
		{
			_sendTimer = new System.Threading.Timer(ar =>
			{
				if (_sendQueue.Count > 0)
				{
					IrcMessage msg = _sendQueue.Dequeue();
					try
					{
						SendMessage(msg);
					}
					catch (Exception)
					{
						CloseConnection();
						throw;
					}
				}
			}, null, _sendPace, _sendPace);

			QueueMessage(new IrcMessage(null, "USER", _userName, _localhost, "*", _fullName));
			QueueMessage(new IrcMessage(null, "NICK", _nickname));

			while (true)
			{
				try
				{
					IrcMessage msg = RecvMessage();

					if (msg != null)
						_dispatcher.BeginInvoke((Action)(() => ProcessMessage(msg)));
					else
						throw new IOException();
				}
				catch (ObjectDisposedException)
				{
					_dispatcher.BeginInvoke((Action)(() => CloseConnection()));
					break;
				}
				catch (IOException)
				{
					_dispatcher.BeginInvoke((Action)(() => CloseConnection()));
					break;
				}
			}
		}

		private void Reset()
		{
			if (_sendTimer != null)
				_sendTimer.Dispose();

			_rstream = null;
			_wstream = null;
			_stream = null;
			_tcpClient = null;
			_connected = false;
			_sendQueue = new Queue<IrcMessage>();
			_sendTimer = null;
		}

		private void CloseConnection()
		{
			Reset();
			OnDisconnected();
		}

		private void OnConnected()
		{
			if (Connected != null)
				Connected(this, IrcEventArgs.Empty);
		}

		private void OnDisconnected()
		{
			if (Disconnected != null)
				Disconnected(this, IrcEventArgs.Empty);
		}

		private void OnMessageSent(IrcMessage message)
		{
			try
			{
				if (MessageSent != null)
					MessageSent(this, new IrcMessageEventArgs(message));
			}
			catch (Exception ex)
			{
				OnError(ex);
				throw;
			}
		}

		private void OnMessageReceived(IrcMessage message)
		{
			if (message.Command == "376")
			{
				_connected = true;
				OnConnected();
			}

			if (MessageReceived != null)
				MessageReceived(this, new IrcMessageEventArgs(message));
		}

		private void OnError(Exception ex)
		{
			if (Error != null)
				Error(this, new ErrorEventArgs(ex));
		}
	}
}
