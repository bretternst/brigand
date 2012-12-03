using System;
using System.IO;

namespace Brigand.MarkovModel
{
	enum SymbolType : byte
	{
		Punctuation = 0,
		Word = 1,
		Terminate = 2,
		Url = 3
	}

	public abstract class Symbol
	{
		public void Save(BinaryWriter bw)
		{
			if (this is PunctuationSymbol)
			{
				bw.Write((byte)PunctuationSymbol.SymType);
				bw.Write((byte)((PunctuationSymbol)this).Type);
			}
			else if (this is WordSymbol)
			{
				bw.Write((byte)WordSymbol.SymType);
				bw.Write(((WordSymbol)this).Word);
			}
			else if (this is TerminateSymbol)
			{
				bw.Write((byte)TerminateSymbol.SymType);
			}
			else if (this is UrlSymbol)
			{
				bw.Write((byte)UrlSymbol.SymType);
				bw.Write(((UrlSymbol)this).Url);
			}
		}

		public static Symbol Load(BinaryReader br)
		{
			SymbolType type = (SymbolType)br.ReadByte();
			Symbol sym;
			switch (type)
			{
				case PunctuationSymbol.SymType:
					sym = new PunctuationSymbol((PunctuationType)br.ReadByte());
					break;
				case WordSymbol.SymType:
					sym = new WordSymbol(br.ReadString());
					break;
				case TerminateSymbol.SymType:
					sym = new TerminateSymbol();
					break;
				case UrlSymbol.SymType:
					sym = new UrlSymbol(br.ReadString());
					break;
				default:
					throw new InvalidOperationException();
			}
			return sym;
		}
	}

	internal enum PunctuationType : byte
	{
		Period = 0,
		Comma = 1,
		SemiColon = 2,
		Colon = 3,
		ExclamationMark = 4,
		QuestionMark = 5
	}

	internal class PunctuationSymbol : Symbol
	{
		public const SymbolType SymType = SymbolType.Punctuation;

		private const string PunctuationMarks = ".,;:!?";

		public PunctuationType Type { get; set; }

		public PunctuationSymbol(PunctuationType type) : base()
		{
			Type = type;
		}

		public PunctuationSymbol(char ch) : base()
		{
			Type = (PunctuationType)PunctuationMarks.IndexOf(ch);
		}

		public static bool IsPunctuationChar(char ch)
		{
			return PunctuationMarks.IndexOf(ch) >= 0;		
		}

		public bool IsTerminating
		{
			get
			{
				return Type == PunctuationType.ExclamationMark || Type == PunctuationType.Period ||
					Type == PunctuationType.QuestionMark;
			}
		}

		public override string ToString()
		{
			return new string(PunctuationMarks[(int)Type],1);
		}

		public override int GetHashCode()
		{
			return Type.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as PunctuationSymbol;
			return other != null && other.Type == this.Type;
		}
	}

	internal class WordSymbol : Symbol
	{
		public const SymbolType SymType = SymbolType.Word;

		public string Word { get; set; }

		public WordSymbol(string word) : base()
		{
			Word = word;
		}

		private const string WordChars = "-_$#@'+=*&/";
		public static bool IsWordChar(char ch)
		{
			return char.IsLetterOrDigit(ch) || WordChars.IndexOf(ch) >= 0;
		}

		public override string ToString()
		{
			return Word;
		}

		public override int GetHashCode()
		{
			return Word.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as WordSymbol;
			return other != null && other.Word == this.Word;
		}
	}

	class UrlSymbol : Symbol
	{
		public const SymbolType SymType = SymbolType.Url;

		public string Url { get; set; }

		public UrlSymbol(string url)
			: base()
		{
			Url = url;
		}

		public override string ToString()
		{
			return Url;
		}

		public override int GetHashCode()
		{
			return Url.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as UrlSymbol;
			return other != null && other.Url == this.Url;
		}
	}
	
	class TerminateSymbol : Symbol
	{
		public const SymbolType SymType = SymbolType.Terminate;

		public override int GetHashCode()
		{
			return 0;
		}

		public override bool Equals(object obj)
		{
			return obj is TerminateSymbol;
		}
	}
}
