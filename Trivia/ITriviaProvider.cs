using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brigand
{
	public interface ITriviaProvider
	{
		int QuestionId { get; }

		string QuestionText { get; }

		string PrimaryAnswer { get; }

		bool HasHints { get; }

		void NextQuestion();

		string NextHint();

		bool CheckAnswer(string candidate);
	}
}
