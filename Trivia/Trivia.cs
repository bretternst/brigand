using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Security;
using System.Globalization;
using System.Threading;

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
			public int QuestionNum = 0;
			public int CurrentHint = 0;
			public GameState State = GameState.Asking;
			public Timer Timer;
			public ITriviaProvider Provider;
		}

		private const string ALIAS_NAME = "trivia";
		private const string TRIVIA_START_STR = "You've been served, and now it's on. Let's have some trivia!";
		private const string TRIVIA_REQ_STR = "{0} wants to start a game of trivia in {1}.";
		private const string TRIVIA_ONE_MORE_STR = "We only need one more! Uno mas!";
		private const string TRIVIA_REQ_EXPIRE = "Time's up, I guess nobody else is man enough to play trivia.";
		private const string TRIVIA_START = "-----[ A trivia game is starting! The first question will be asked in {0} seconds... ]-----";
		private const string QUESTION_ASK = "-----[ Question # {0} ]-----";
		private const string QUESTION_NEXT = "The next question will be asked in {0} seconds...";
		private const string QUESTION_HINT = "Here's a hint: {0}";
		private const string QUESTION_EXPIRE = "Time's up! Nobody got the right answer: {0}";
		private const string QUESTION_CORRECT = "Ding! Ding! Ding! {0} got the correct answer: {1}";
		private const string NO_WINNER = "There were no winners. Only losers. Namely, all of you.";
		private const string GAME_OVER = "-----[ The trivia game is over. Get lost. ]-----";
		private const string GAME_WINNER = "Way to go genius, we're SO impressed with your knowledge.";
		private const string GAME_TIE = "Winners don't tie. Try harder next time.";
		private const string ANSWER_REVEAL = "The answer is {0}";

		private Dictionary<string, HashSet<string>> requests = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, Timer> reqTimers = new Dictionary<string, Timer>();
		private Dictionary<string, Game> games = new Dictionary<string, Game>();

		[ModuleProperty("providerType")]
		public string ProviderType { get; set; }

		[ModuleProperty("requestThreshold")]
		public int RequestThreshold { get; set; }

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
				throw new InvalidOperationException("The ProviderType property is required.");
			if (this.RequestThreshold < 1)
				throw new InvalidOperationException("The RequestThreshold property is required.");
			if (this.RequestExpire < 1)
				throw new InvalidOperationException("The RequestExpire property is required.");
			if (this.QuestionsPerGame < 1)
				throw new InvalidOperationException("The QuestionsPerGame property is reqiured.");
		}

		private void TriviaRequest(object sender, AliasEventArgs e)
		{
			if (!e.Handled && e.Name == ALIAS_NAME && e.Target.IsChannel)
			{
				string chan = e.Target.Channel;
				string user = e.From.NickUserHost;
				if (games.ContainsKey(chan)) return;

				if (!requests.ContainsKey(chan))
					requests.Add(chan, new HashSet<string>());
				if (!requests[chan].Contains(user))
					requests[chan].Add(user);
				else
					return;

				if (requests[chan].Count >= this.RequestThreshold)
				{
					this.Irc.Say(chan, TRIVIA_START_STR);
					requests[chan].Clear();
					if (reqTimers[chan] != null) reqTimers[chan].Cancel();
					reqTimers.Remove(chan);
					TriviaStart(chan);
				}
				else
				{
					string s = string.Format(TRIVIA_REQ_STR, e.From.Nickname, chan);
					if (this.RequestThreshold - requests[chan].Count == 1)
						s += " " + TRIVIA_ONE_MORE_STR;
					this.Irc.Say(chan, s);
					if (!reqTimers.ContainsKey(chan))
					{
						reqTimers.Add(chan,
							new Timer(this.Dispatcher, (EventHandler)((s2,o) =>
								{
									this.Irc.Say(chan, TRIVIA_REQ_EXPIRE);
									requests[chan].Clear();
									reqTimers.Remove(chan);
								}
						), this.RequestExpire*1000, chan));
					}
				}
			}
		}

		private void TriviaStart(string chan)
		{
			requests[chan].Clear();
			games.Add(chan, new Game());
			games[chan].Provider = CreateProvider();
			this.Irc.Say(chan, string.Format(TRIVIA_START, this.BeforeQuestionDelay));
			this.Irc.PrivateMessage += new EventHandler<IrcChatEventArgs>(TriviaGuess);
			games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.BeforeQuestionDelay*1000, chan);
		}

		private void TriviaLoop(object sender, EventArgs e)
		{
			string chan = (string)((CallbackEventArgs)e).State;

			switch (games[chan].State)
			{
				case GameState.Asking:
					games[chan].Provider.NextQuestion();
					this.Irc.Say(chan, string.Format(QUESTION_ASK, games[chan].QuestionNum+1));
					this.Irc.Say(chan, games[chan].Provider.QuestionText);
					games[chan].State = GameState.Asked;
					games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.AfterQuestionDelay*1000, chan);
					break;
				case GameState.Asked:
					bool hasHints = games[chan].Provider.HasHints;
					bool canReveal = this.CanReveal(games[chan].Provider.PrimaryAnswer, games[chan].CurrentHint);
					if (++games[chan].CurrentHint <= this.MaxNumberOfHints && (hasHints || canReveal))
					{
						if(hasHints)
							this.Irc.Say(chan, string.Format(QUESTION_HINT, games[chan].Provider.NextHint()));
						if (canReveal)
							this.Irc.Say(chan, string.Format(ANSWER_REVEAL,
								this.GetAnswerReveal(games[chan].Provider.PrimaryAnswer, games[chan].CurrentHint)));
						games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.AfterHintDelay * 1000, chan);
					}
					else
					{
						this.Irc.Say(chan, string.Format(QUESTION_EXPIRE, games[chan].Provider.PrimaryAnswer));
						TriviaNextQuestion(chan);
					}
					break;
				default:
					break;
			}
		}

		private void TriviaNextQuestion(string chan)
		{
			games[chan].QuestionNum++;
			if (games[chan].Timer != null)
			{
				games[chan].Timer.Cancel();
				games[chan].Timer = null;
			}
			games[chan].State = GameState.Asking;
			games[chan].CurrentHint = 0;
			if (games[chan].QuestionNum < this.QuestionsPerGame)
			{
				this.Irc.Say(chan, string.Format(QUESTION_NEXT, this.BeforeQuestionDelay));
				games[chan].Timer = new Timer(this.Dispatcher, TriviaLoop, this.BeforeQuestionDelay*1000, chan);
			}
			else
			{
				TriviaGameOver(chan);
			}
		}

		private void TriviaGuess(object sender, IrcChatEventArgs e)
		{
			if (e.IsSelf || e.From.IsServer || !e.Target.IsChannel || !games.ContainsKey(e.Target.Channel))
				return;

			string chan = e.Target.Channel;
			if (games[chan].State != GameState.Asked) return;
			if (games[chan].Provider.CheckAnswer(e.Text.Trim().ToLower()))
			{
				this.Irc.Say(chan, string.Format(QUESTION_CORRECT, e.From.Nickname, games[chan].Provider.PrimaryAnswer));
				if (!games[chan].Scores.ContainsKey(e.From.Nickname))
					games[chan].Scores.Add(e.From.Nickname, 0);
				games[chan].Scores[e.From.Nickname]++;
				TriviaNextQuestion(chan);
			}
		}

		private void TriviaGameOver(string chan)
		{
			if (games[chan].Timer != null)
			{
				games[chan].Timer.Cancel();
				games[chan].Timer = null;
			}

			int topScore = 0;
			var scores = games[chan].Scores;
			var winners = new List<string>();

			if (scores.Count < 1)
			{
				this.Irc.Say(chan, NO_WINNER);
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
						sb.Append(", ");
					else if (i < winners.Count - 1)
						sb.Append(" and ");
				}
				sb.Append(" with ").Append(topScore).Append(" point");
				if (topScore > 1) sb.Append("s");
				sb.Append(" each.");
				this.Irc.Say(chan, sb.ToString());
				this.Irc.Say(chan, GAME_TIE);
			}
			else if (winners.Count == 1)
			{
				sb.Append("Game over. The game's winner is ").Append(winners[0]).Append(" with ").Append(topScore).Append(" point");
				if (topScore > 1) sb.Append("s");
				sb.Append(". ");
				sb.Append(GAME_WINNER);
				this.Irc.Say(chan, sb.ToString());
			}

			var runnersUp = new Dictionary<int, List<string>>();
			if (topScore > 0)
			{
				foreach (var k in scores.Keys)
				{
					if (scores[k] < topScore)
					{
						if (runnersUp.ContainsKey(scores[k]))
							runnersUp[scores[k]].Add(k);
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
			for (int i = topScore - 1; i >= 1; i--)
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
					sb.Append(s).Append(", ");
				sb.Length = sb.Length - 2;
				this.Irc.Say(chan, sb.ToString());
				sb.Length = 0;
				place++;
				if (place > 2) break;
			}

			this.Irc.Say(chan, GAME_OVER);
			if (games[chan].Provider is IDisposable)
				((IDisposable)games[chan].Provider).Dispose();
			games.Remove(chan);
		}

		private ITriviaProvider CreateProvider()
		{
			Type t = Type.GetType(this.ProviderType, false);
			if (t == null)
				throw new InvalidOperationException("The type " + this.ProviderType + " was not found.");
			object o = Activator.CreateInstance(t);
			if (!(o is ITriviaProvider))
				throw new InvalidOperationException("The type " + this.ProviderType + " does not implement ITriviaProvider.");
			return o as ITriviaProvider;
		}

		private bool CanReveal(string answer, int hint)
		{
			if (!this.ShowAnswerReveal)
				return false;

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
