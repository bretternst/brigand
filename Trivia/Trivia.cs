using System;
using System.Collections.Generic;
using System.Text;
using Floe.Net;

namespace Brigand
{
	public sealed class Trivia : BotModule
	{
		private enum GameState
		{
			Asking, Asked
		}

		private class Game
		{
			public Dictionary<string, int> Scores = new Dictionary<string, int>();
			public HashSet<string> Skips = new HashSet<string>();
			public HashSet<string> Aborts = new HashSet<string>();
			public int QuestionNum = 0;
			public int CurrentHint = 0;
			public GameState State = GameState.Asking;
			public Timer Timer;
			public ITriviaProvider Provider;
		}

		private const string TriviaName = "trivia";
		private const string SkipName = "skip";
		private const string AbortName = "abort";
		private const string TriviaServedMessage = "You've been served, and now it's on. Let's have some trivia!";
		private const string TriviaRequestMessage = "{0} wants to start a game of trivia in {1}.";
		private const string TriviaOneMore = "We only need one more! Uno mas!";
		private const string TriviaReqExpire = "Time's up, I guess nobody else is man enough to play trivia.";
		private const string TriviaStartMessage = "-----[ A trivia game is starting! The first question will be asked in {0} seconds... ]-----";
		private const string QuestionAsk = "-----[ Question # {0} ]-----";
		private const string QuestionNext = "The next question will be asked in {0} seconds...";
		private const string QuestionHint = "Here's a hint: {0}";
		private const string QuestionExpire = "Time's up! Nobody got the right answer: {0}";
		private const string QuestionCorrect = "Ding! Ding! Ding! {0} got the correct answer: {1}";
		private const string NoWinner = "There were no winners. Only losers. Namely, all of you.";
		private const string GameOver = "-----[ The trivia game is over. Get lost. ]-----";
		private const string GameWinner = "Way to go genius, we're SO impressed with your knowledge.";
		private const string GameTie = "Winners don't tie. Try harder next time.";
		private const string AnswerReveal = "The answer is {0}";
		private const string QuestionSkipped = "{0} out of {0} people agree: the question sucks. Skipping...";

		private Dictionary<string, HashSet<string>> _requests = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, Timer> _reqTimers = new Dictionary<string, Timer>();
		private Dictionary<string, Game> _games = new Dictionary<string, Game>();

		[ModuleProperty("providerType")]
		public string ProviderType { get; set; }

		[ModuleProperty("requestThreshold")]
		public int RequestThreshold { get; set; }

		[ModuleProperty("skipThreshold")]
		public int SkipThreshold { get; set; }

		[ModuleProperty("requestExpire")]
		public int RequestExpire { get; set; }

		[ModuleProperty("beforeQuestionDelay")]
		public int BeforeQuestionDelay { get; set; }

		[ModuleProperty("afterQuestionDelay")]
		public int AfterQuestionDelay { get; set; }

		[ModuleProperty("afterHintDelay")]
		public int AfterHintDelay { get; set; }

		[ModuleProperty("maxNumberOfHints")]
		public int MaxNumberOfHints { get; set; }

		[ModuleProperty("showAnswerReveal")]
		public bool ShowAnswerReveal { get; set; }

		[ModuleProperty("questionsPerGame")]
		public int QuestionsPerGame { get; set; }

		protected override void OnInit()
		{
			this.Aliases.CallAlias += new EventHandler<AliasEventArgs>(TriviaRequest);

			if (this.ProviderType == null)
			{
				throw new InvalidOperationException("The ProviderType property is required.");
			}
			if (this.RequestThreshold < 1)
			{
				throw new InvalidOperationException("The RequestThreshold property is required.");
			}
			if (this.RequestExpire < 1)
			{
				throw new InvalidOperationException("The RequestExpire property is required.");
			}
			if (this.QuestionsPerGame < 1)
			{
				throw new InvalidOperationException("The QuestionsPerGame property is reqiured.");
			}
		}

		private void TriviaRequest(object sender, AliasEventArgs e)
		{
			if (!e.To.IsChannel)
			{
				return;
			}

			var chan = e.To.Name;
			string user = e.From.Prefix;

			switch (e.Name)
			{
				case AbortName:
					{
						if (!_games.ContainsKey(chan) || _games[chan].Aborts.Contains(user))
						{
							return;
						}
						_games[chan].Aborts.Add(user);
						if (_games[chan].Aborts.Count >= this.RequestThreshold)
						{
							this.TriviaGameOver(e.To);
						}
					}
					break;
				case SkipName:
					{
						if (!_games.ContainsKey(chan) || _games[chan].Skips.Contains(user))
						{
							return;
						}
						_games[chan].Skips.Add(user);
						if (_games[chan].Skips.Count >= this.SkipThreshold)
						{
							this.TriviaNextQuestion(e.To, false);
						}
					}
					break;
				case TriviaName:
					{
						if (_games.ContainsKey(chan))
						{
							return;
						}

						if (!_requests.ContainsKey(chan))
						{
							_requests.Add(chan, new HashSet<string>());
						}
						if (!_requests[chan].Contains(user))
						{
							_requests[chan].Add(user);
						}
						else
						{
							return;
						}

						if (_requests[chan].Count >= this.RequestThreshold)
						{
							this.Irc.PrivateMessage(e.To, TriviaServedMessage);
							_requests[chan].Clear();
							if (_reqTimers.ContainsKey(chan))
							{
								if (_reqTimers[chan] != null)
								{
									_reqTimers[chan].Cancel();
								}
								_reqTimers.Remove(chan);
							}
							TriviaStart(e.To);
						}
						else
						{
							string s = string.Format(TriviaRequestMessage, e.From.Nickname, chan);
							if (this.RequestThreshold - _requests[chan].Count == 1)
							{
								s += " " + TriviaOneMore;
							}
							this.Irc.PrivateMessage(e.To, s);
							if (!_reqTimers.ContainsKey(chan))
							{
								_reqTimers.Add(chan,
									new Timer(this.Dispatcher, (EventHandler)((s2, o) =>
									{
										this.Irc.PrivateMessage(e.To, TriviaReqExpire);
										_requests[chan].Clear();
										_reqTimers.Remove(chan);
									}
								), this.RequestExpire * 1000, chan));
							}
						}
					}
					break;
			}
		}

		private void TriviaStart(IrcTarget to)
		{
			var chan = to.Name;
			_requests[chan].Clear();
			_games.Add(chan, new Game());
			_games[chan].Provider = CreateProvider();
			this.Irc.PrivateMessage(to, string.Format(TriviaStartMessage, this.BeforeQuestionDelay));
			this.Irc.PrivateMessaged += new EventHandler<IrcMessageEventArgs>(TriviaGuess);
			_games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.BeforeQuestionDelay*1000, to);
		}

		private void TriviaLoop(object sender, EventArgs e)
		{
			var to = (IrcTarget)((CallbackEventArgs)e).State;
			var chan = to.Name;

			switch (_games[chan].State)
			{
				case GameState.Asking:
					_games[chan].Provider.NextQuestion();
					this.Irc.PrivateMessage(to, string.Format(QuestionAsk, _games[chan].QuestionNum+1));
					this.Irc.PrivateMessage(to, _games[chan].Provider.QuestionText);
					_games[chan].State = GameState.Asked;
					_games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.AfterQuestionDelay*1000, to);
					break;
				case GameState.Asked:
					bool hasHints = _games[chan].Provider.HasHints;
					bool canReveal = this.CanReveal(_games[chan].Provider.PrimaryAnswer, _games[chan].CurrentHint);
					if (++_games[chan].CurrentHint <= this.MaxNumberOfHints && (hasHints || canReveal))
					{
						if (hasHints)
						{
							this.Irc.PrivateMessage(to, string.Format(QuestionHint, _games[chan].Provider.NextHint()));
						}
						if (canReveal)
						{
							this.Irc.PrivateMessage(to, string.Format(AnswerReveal,
								this.GetAnswerReveal(_games[chan].Provider.PrimaryAnswer, _games[chan].CurrentHint)));
						}
						_games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.AfterHintDelay * 1000, to);
					}
					else
					{
						this.Irc.PrivateMessage(to, string.Format(QuestionExpire, _games[chan].Provider.PrimaryAnswer));
						TriviaNextQuestion(to, true);
					}
					break;
				default:
					break;
			}
		}

		private void TriviaNextQuestion(IrcTarget to, bool increment)
		{
			var chan = to.Name;

			if (increment)
			{
				_games[chan].QuestionNum++;
			}
			else
			{
				this.Irc.PrivateMessage(to, string.Format(QuestionSkipped, this.SkipThreshold));
			}
			if (_games[chan].Timer != null)
			{
				_games[chan].Timer.Cancel();
				_games[chan].Timer = null;
			}
			_games[chan].State = GameState.Asking;
			_games[chan].CurrentHint = 0;
			_games[chan].Skips.Clear();
			if (_games[chan].QuestionNum < this.QuestionsPerGame)
			{
				this.Irc.PrivateMessage(to, string.Format(QuestionNext, this.BeforeQuestionDelay));
				_games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.BeforeQuestionDelay*1000, to);
			}
			else
			{
				TriviaGameOver(to);
			}
		}

		private void TriviaGuess(object sender, IrcMessageEventArgs e)
		{
			if (!e.To.IsChannel || !_games.ContainsKey(e.To.Name))
			{
				return;
			}

			string chan = e.To.Name;
			if (_games[chan].State != GameState.Asked)
			{
				return;
			}
			if (_games[chan].Provider.CheckAnswer(e.Text.Trim().ToLower()))
			{
				this.Irc.PrivateMessage(e.To, string.Format(QuestionCorrect, e.From.Nickname, _games[chan].Provider.PrimaryAnswer));
				if (!_games[chan].Scores.ContainsKey(e.From.Nickname))
				{
					_games[chan].Scores.Add(e.From.Nickname, 0);
				}
				_games[chan].Scores[e.From.Nickname]++;
				TriviaNextQuestion(e.To, true);
			}
		}

		private void TriviaGameOver(IrcTarget to)
		{
			var chan = to.Name;

			if (_games[chan].Timer != null)
			{
				_games[chan].Timer.Cancel();
				_games[chan].Timer = null;
			}

			int topScore = 0;
			var scores = _games[chan].Scores;
			var winners = new List<string>();

			if (scores.Count < 1)
			{
				this.Irc.PrivateMessage(to, NoWinner);
			}
			else
			{
				foreach (var k in scores.Keys)
				{
					if (scores[k] > topScore)
					{
						winners.Clear();
						winners.Add(k);
						topScore = scores[k];
					}
					else if (scores[k] == topScore)
					{
						winners.Add(k);
					}
				}
			}

			var sb = new StringBuilder();
			if (winners.Count > 1)
			{
				sb.Append("Game over. It's a tie between ");
				for (int i = 0; i < winners.Count; i++)
				{
					sb.Append(winners[i]);
					if (i < winners.Count - 2)
					{
						sb.Append(", ");
					}
					else if (i < winners.Count - 1)
					{
						sb.Append(" and ");
					}
				}
				sb.Append(" with ").Append(topScore).Append(" point");
				if (topScore > 1)
				{
					sb.Append("s");
				}
				sb.Append(" each.");
				this.Irc.PrivateMessage(to, sb.ToString());
				this.Irc.PrivateMessage(to, GameTie);
			}
			else if (winners.Count == 1)
			{
				sb.Append("Game over. The game's winner is ").Append(winners[0]).Append(" with ").Append(topScore).Append(" point");
				if (topScore > 1)
				{
					sb.Append("s");
				}
				sb.Append(". ");
				sb.Append(GameWinner);
				this.Irc.PrivateMessage(to, sb.ToString());
			}

			var runnersUp = new Dictionary<int, List<string>>();
			if (topScore > 0)
			{
				foreach (var k in scores.Keys)
				{
					if (scores[k] < topScore)
					{
						if (runnersUp.ContainsKey(scores[k]))
						{
							runnersUp[scores[k]].Add(k);
						}
						else
						{
							runnersUp.Add(scores[k], new List<string>());
							runnersUp[scores[k]].Add(k);
						}
					}
				}
			}

			sb.Length = 0;
			int place = 0;
			for (int i = topScore - 1; i >= 1 && place <= 2; i--)
			{
				if (!runnersUp.ContainsKey(i)) continue;
				switch (place)
				{
					case 0:
						sb.Append("First Loser");
						break;
					case 1:
						sb.Append("Second Loser");
						break;
					case 2:
						sb.Append("Third Loser");
						break;
					default:
						break;
				}
				if (runnersUp[i].Count > 1) sb.Append("s");
				sb.Append(" at ").Append(i).Append(" point");
				if (i > 1) sb.Append("s");
				sb.Append(": ");
				foreach (var s in runnersUp[i])
				{
					sb.Append(s).Append(", ");
				}
				sb.Length = sb.Length - 2;
				this.Irc.PrivateMessage(to, sb.ToString());
				sb.Length = 0;
				place++;
			}

			this.Irc.PrivateMessage(to, GameOver);
			if (_games[chan].Provider is IDisposable)
			{
				((IDisposable)_games[chan].Provider).Dispose();
			}
			_games.Remove(chan);
		}

		private ITriviaProvider CreateProvider()
		{
			Type t = Type.GetType(this.ProviderType, false);
			if (t == null)
			{
				throw new InvalidOperationException("The type " + this.ProviderType + " was not found.");
			}
			object o = Activator.CreateInstance(t);
			if (!(o is ITriviaProvider))
			{
				throw new InvalidOperationException("The type " + this.ProviderType + " does not implement ITriviaProvider.");
			}
			return o as ITriviaProvider;
		}

		private bool CanReveal(string answer, int hint)
		{
			if (!this.ShowAnswerReveal)
			{
				return false;
			}

			int numAlpha = 0;
			for (int i = 0; i < answer.Length; i++)
			{
				if (char.IsLetterOrDigit(answer[i])) numAlpha++;
			}
			return hint < numAlpha / 2;
		}

		private string GetAnswerReveal(string answer, int hint)
		{
			answer = answer.Trim();
			var sb = new StringBuilder();
			var words = answer.Split(' ');
			int[] reveal = new int[words.Length];
			int howMany = hint - 1;
			int wordIdx = 0;
			int noFind = 0;

			while (howMany > 0)
			{
				int curWord = wordIdx % words.Length;
				if (reveal[curWord] < words[curWord].Length)
				{
					reveal[curWord]++;
					howMany--;
					noFind = 0;
				}
				else
				{
					noFind++;
				}
				if (noFind > words.Length) break;
				wordIdx++;
			}

			for (int i = 0; i < words.Length; i++)
			{
				string c = words[i];
				for (int j = 0; j < words[i].Length; j++)
				{
					if (j < reveal[i])
					{
						sb.Append(c[j]);
					}
					else
					{
						sb.Append('_');
					}
				}
				sb.Append(' ');
			}
			sb.Length = sb.Length - 1;
			return sb.ToString();
		}
	}
}
