using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brigand.MarkovModel
{
	public enum ModelContext { Forward, Backward }

	public unsafe class Model
	{
		private const int SoftCutoff = 25;
		private const int HardCutoff = 100;
		private const int Order = 2;
		private static Random _rand = new Random();

		private SymbolDictionary _dict = new SymbolDictionary();
		private Node* _forward;
		private Node* _backward;

		public Model()
		{
			_forward = (Node*)Memory.Alloc(sizeof(Node));
			_backward = (Node*)Memory.Alloc(sizeof(Node));
		}

		~Model()
		{
			_forward->Free();
			Memory.Free(_forward);
			_backward->Free();
			Memory.Free(_backward);
		}

		private void Associate(ModelContext context, IEnumerable<ushort> tokens)
		{
			Node* node = context == ModelContext.Forward ? _forward : _backward;
			foreach (ushort tok in tokens)
			{
				node = node->FindOrAdd(tok);
			}
		}

		private Node* Find(ModelContext context, IEnumerable<ushort> tokens)
		{
			Node* node = context == ModelContext.Forward ? _forward : _backward;
			foreach (ushort tok in tokens)
			{
				node = node->Find(tok);
				if (node == null) break;
			}
			return node;
		}

		private void Learn(IList<ushort> tokens)
		{
			if (tokens.Count < Order + 1) return;

			for (int i = 0; i < tokens.Count; i++)
			{
				if (i + Order < tokens.Count)
				{
					IEnumerable<ushort> forward = tokens.Select(t => t).Skip(i).Take(Order + 1);
					this.Associate(ModelContext.Forward, forward);
				}
				if (i == Order - 1)
				{
					var reverseTerminator = new List<ushort>();
					reverseTerminator.AddRange(tokens.Select(t => t).Take(Order).Reverse());
					reverseTerminator.Add(_dict[new TerminateSymbol()]);
					this.Associate(ModelContext.Backward, reverseTerminator);
				}
				if (i >= Order && !(_dict[tokens[i]] is TerminateSymbol))
				{
					IEnumerable<ushort> backward = tokens.Select(t => t).Skip(i - Order).Take(Order + 1).Reverse();
					this.Associate(ModelContext.Backward, backward);
				}
			}
		}

		private bool AddWordForward(IList<ushort> reply)
		{
			int o = Order;
			if (reply.Count < o) o = reply.Count;

			IEnumerable<ushort> forward = reply.Skip(reply.Count - o).Take(o);
			Node* n = this.Find(ModelContext.Forward, forward);

			if (n == null || n->branch < 1)
			{
				return false;
			}
			ushort t = GetNextToken(n);
			if (_dict[t] is TerminateSymbol)
			{
				return false;
			}
			reply.Add(t);
			return !(reply.Count >= SoftCutoff && _dict[t] is PunctuationSymbol && ((PunctuationSymbol)_dict[t]).IsTerminating);
		}

		private bool AddWordBackward(IList<ushort> reply)
		{
			int o = Order;
			if (reply.Count < o) o = reply.Count;

			IEnumerable<ushort> backward = reply.Take(o).Reverse();
			Node* n = this.Find(ModelContext.Backward, backward);
			if (n == null || n->branch < 1)
			{
				return false;
			}
			ushort t = GetNextToken(n);
			if (_dict[t] is TerminateSymbol || (_dict[t] is PunctuationSymbol && ((PunctuationSymbol)_dict[t]).IsTerminating))
			{
				return false;
			}
			reply.Insert(0, t);
			return true;
		}

		private ushort GetNextToken(Node* n)
		{
			return n->children[_rand.Next(n->branch)]->token;
		}

		private void SeedReply(List<ushort> reply, IList<ushort> tokens)
		{
			int tryOrder = _rand.Next(Order) + 1;

			for (int o = tryOrder; o > 0; --o)
			{
				for (int i = 0; i < tokens.Count; i++)
				{
					if (i + o > tokens.Count)
					{
						break;
					}

					IEnumerable<ushort> forward = tokens.Skip(i).Take(o);
					Node* n = Find(ModelContext.Forward, forward);
					if (n != null)
					{
						reply.AddRange(forward);
						return;
					}
				}
			}
			if (reply.Count < 1)
			{
				if (_forward->branch > 0)
				{
					reply.Add(GetNextToken(_forward));
				}
			}
		}

		private IList<ushort> Reply(IList<ushort> symbols)
		{
			var reply = new List<ushort>();
			this.SeedReply(reply, symbols);
			if (reply.Count == 0)
			{
				return reply;
			}

			while (reply.Count < HardCutoff && this.AddWordBackward(reply)) ;
			while (reply.Count < HardCutoff && this.AddWordForward(reply)) ;
			reply.Add(_dict.AddSymbol(new TerminateSymbol()));
			return reply;
		}

		public void Learn(string input)
		{
			var symbols = StringParser.Parse(input).ToList();
			this.Learn(symbols.Select(s => _dict.AddSymbol(s)).ToList());
		}

		public string Query(string input)
		{
			var symbols = StringParser.Parse(input).ToList();
			var tokens = symbols.Select(s => _dict.AddSymbol(s)).ToList();
			var reply = this.Reply(tokens).Select(t => _dict[t]).ToList();
			this.Learn(tokens);

			return StringParser.Format(reply);
		}

		public void Save(string fileName)
		{
			using (FileStream fs = File.Create(fileName))
			{
				BinaryWriter bw = new BinaryWriter(fs);
				_dict.Save(bw);
				_forward->Write(bw,0);
				_backward->Write(bw,0);
				bw.Close();
				fs.Close();
			}
		}

		static public Model Load(string fileName)
		{
			Model m = new Model();

			using (FileStream fs = File.OpenRead(fileName))
			{
				BinaryReader br = new BinaryReader(fs);
				m._dict.Load(br);
				m._forward->Read(br);
				m._backward->Read(br);
				br.Close();
				fs.Close();
				return m;
			}
		}

		/// <summary>
		/// Gets the total number of unique symbols.
		/// </summary>
		public int SymbolCount
		{
			get
			{
				return _dict.Count;
			}
		}

		/// <summary>
		/// Gets the total number of tuples in the model.
		/// </summary>
		public int TotalTupleCount
		{
			get
			{
				return _forward->Count(Order);
			}
		}

		/// <summary>
		/// Gets the total number of nodes in the model.
		/// </summary>
		public int TotalNodeCount
		{
			get
			{
				return _forward->Count();
			}
		}

		/// <summary>
		/// Gets the total memory, in bytes, used by the model's tree.
		/// </summary>
		public int TotalBytesUsed
		{
			get
			{
				return _forward->CountBytes();
			}
		}
	}
}
