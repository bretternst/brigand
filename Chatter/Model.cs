using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace Brigand.MarkovModel
{
	[StructLayout(LayoutKind.Sequential,Pack=1)]
	unsafe struct Node
	{
		public ushort token;
		public ushort branch;
		public Node** children;

		public void Free()
		{
			if (branch > 0)
			{
				for (int i = 0; i < branch; i++)
				{
					children[i]->Free();
					Memory.Free(children[i]);
				}
				Memory.Free(children);
			}
		}

		int Search(ushort token, out bool found)
		{
			found = false;
			if (branch < 1) return 0;
			int min = 0;
			int max = branch - 1;
			for (; ; )
			{
				int middle = (min + max) / 2;
				int compare = token - children[middle]->token;
				if (compare == 0)
				{
					found = true;
					return middle;
				}
				else if (compare > 0)
				{
					if (max == middle)
						return middle + 1;
					min = middle + 1;
				}
				else
				{
					if (min == middle)
						return middle;
					max = middle - 1;
				}
			}
		}

		public Node* Find(ushort token)
		{
			bool found;
			int i = this.Search(token, out found);
			if (found) return children[i];
			else return null;
		}

		public Node* FindOrAdd(ushort token)
		{
			bool found;
			int i = Search(token, out found);
			if (found) return children[i];
			else
			{
				Node* newNode = (Node*)Memory.Alloc(sizeof(Node));
				newNode->token = token;
				AddNode(newNode, i);
				return newNode;
			}
		}

		Node* AddNode(Node* newNode, int pos)
		{
			if (branch < 1)
			{
				children = (Node**)Memory.Alloc(sizeof(Node*));
			}
			else
			{
				Node** newChildren = (Node**)Memory.Alloc(sizeof(Node*) * (branch + 1));
				for (int i = 0; i < branch; i++)
				{
					newChildren[i] = children[i];
				}
				Memory.Free(children);
				children = newChildren;
			}

			for (int i = branch; i > pos; --i)
			{
				children[i] = children[i - 1];
			}
			children[pos] = newNode;
			branch++;
			return newNode;
		}

		public void Write(BinaryWriter bw, int level)
		{
			bw.Write(token);
			bw.Write(branch);

			for (int i = 0; i < branch; i++)
			{
				children[i]->Write(bw, level + 1);
			}
		}

		public void Read(BinaryReader br)
		{
			token = br.ReadUInt16();
			branch = br.ReadUInt16();
			if (branch > 0)
			{
				children = (Node**)Memory.Alloc(sizeof(Node*) * branch);
				for (int i = 0; i < branch; i++)
				{
					Node* newNode = (Node*)Memory.Alloc(sizeof(Node));
					children[i] = newNode;
					newNode->Read(br);
				}
			}
		}

		private int Count(int level, int targetLevel)
		{
			int c = targetLevel == -1 || targetLevel == level ? 1 : 0;
			if (branch > 0)
				for (int i = 0; i < branch; i++)
					c += children[i]->Count(level+1, targetLevel);
			return c;
		}

		public int Count(int targetLevel)
		{
			return this.Count(1, targetLevel);
		}

		public int Count()
		{
			return this.Count(-1);
		}

		public int CountBytes()
		{
			int m = sizeof(Node);
			if (branch > 0)
			{
				m += sizeof(Node*) * branch;
				for (int i = 0; i < branch; i++)
					m += children[i]->CountBytes();
			}
			return m;
		}
	}

	public enum ModelContext { Forward, Backward }

	public unsafe class Model
	{
		private const string IDENTICAL_REPLY = "I understand: ";

		private const int SOFT_CUTOFF = 25;
		private const int HARD_CUTOFF = 100;
		private const int ORDER = 2;
		private SymbolDictionary dict = new SymbolDictionary();
		private Node* forward;
		private Node* backward;
		private static Random rand = new Random();

		public Model()
		{
			forward = (Node*)Memory.Alloc(sizeof(Node));
			backward = (Node*)Memory.Alloc(sizeof(Node));
		}

		~Model()
		{
			forward->Free();
			Memory.Free(forward);
			backward->Free();
			Memory.Free(backward);
		}

		private void Associate(ModelContext context, IEnumerable<ushort> tokens)
		{
			Node* node = context == ModelContext.Forward ? forward : backward;
			foreach (ushort tok in tokens)
			{
				node = node->FindOrAdd(tok);
			}
		}

		private Node* Find(ModelContext context, IEnumerable<ushort> tokens)
		{
			Node* node = context == ModelContext.Forward ? forward : backward;
			foreach (ushort tok in tokens)
			{
				node = node->Find(tok);
				if (node == null) break;
			}
			return node;
		}

		private void Learn(IList<ushort> tokens)
		{
			if (tokens.Count < ORDER + 1) return;

			for (int i = 0; i < tokens.Count; i++)
			{
				if (i + ORDER < tokens.Count)
				{
					IEnumerable<ushort> forward = tokens.Select(t => t).Skip(i).Take(ORDER + 1);
					this.Associate(ModelContext.Forward, forward);
				}
				if (i == ORDER - 1)
				{
					var reverseTerminator = new List<ushort>();
					reverseTerminator.AddRange(tokens.Select(t => t).Take(ORDER).Reverse());
					reverseTerminator.Add(dict[new TerminateSymbol()]);
					this.Associate(ModelContext.Backward, reverseTerminator);
				}
				if (i >= ORDER && !(dict[tokens[i]] is TerminateSymbol))
				{
					IEnumerable<ushort> backward = tokens.Select(t => t).Skip(i - ORDER).Take(ORDER + 1).Reverse();
					this.Associate(ModelContext.Backward, backward);
				}
			}
		}

		private bool AddWordForward(IList<ushort> reply)
		{
			int o = ORDER;
			if (reply.Count < o) o = reply.Count;

			IEnumerable<ushort> forward = reply.Skip(reply.Count - o).Take(o);
			Node* n = this.Find(ModelContext.Forward, forward);

			if (n == null || n->branch < 1) return false;
			ushort t = GetNextToken(n);
			if (dict[t] is TerminateSymbol) return false;
			reply.Add(t);
			return !(reply.Count >= SOFT_CUTOFF && dict[t] is PunctuationSymbol && ((PunctuationSymbol)dict[t]).IsTerminating);
		}

		private bool AddWordBackward(IList<ushort> reply)
		{
			int o = ORDER;
			if (reply.Count < o) o = reply.Count;

			IEnumerable<ushort> backward = reply.Take(o).Reverse();
			Node* n = this.Find(ModelContext.Backward, backward);
			if (n == null || n->branch < 1) return false;
			ushort t = GetNextToken(n);
			if (dict[t] is TerminateSymbol || (dict[t] is PunctuationSymbol && ((PunctuationSymbol)dict[t]).IsTerminating))
				return false;
			reply.Insert(0, t);
			return true;
		}

		private ushort GetNextToken(Node* n)
		{
			return n->children[rand.Next(n->branch)]->token;
		}

		private void SeedReply(List<ushort> reply, IList<ushort> tokens)
		{
			int tryOrder = rand.Next(ORDER) + 1;

			for (int o = tryOrder; o > 0; --o)
			{
				for (int i = 0; i < tokens.Count; i++)
				{
					if (i + o > tokens.Count) break;

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
                if (forward->branch > 0)
                    reply.Add(GetNextToken(forward));
			}
		}

		private IList<ushort> Reply(IList<ushort> symbols)
		{
			var reply = new List<ushort>();
			this.SeedReply(reply, symbols);
			if (reply.Count == 0)
				return reply;

			while (reply.Count < HARD_CUTOFF && this.AddWordBackward(reply)) ;
			while (reply.Count < HARD_CUTOFF && this.AddWordForward(reply)) ;
			reply.Add(dict.AddSymbol(new TerminateSymbol()));
			return reply;
		}

		public void Learn(string input)
		{
			var symbols = StringParser.Parse(input).ToList();
			this.Learn(symbols.Select(s => dict.AddSymbol(s)).ToList());
		}

		public string Query(string input)
		{
			var symbols = StringParser.Parse(input).ToList();
			var tokens = symbols.Select(s => dict.AddSymbol(s)).ToList();
			var reply = this.Reply(tokens).Select(t => dict[t]).ToList();
			this.Learn(tokens);

			bool hasReply = true;
			if (reply.Count == symbols.Count)
			{
				hasReply = false;
				for (int i = 0; i < reply.Count; i++)
				{
					if (!reply[i].Equals(symbols[i]))
					{
						hasReply = true;
						break;
					}
				}
			}

			if (hasReply)
				return StringParser.Format(reply);
			else
				return IDENTICAL_REPLY + StringParser.Format(reply);
		}

		public void Save(string fileName)
		{
			using (FileStream fs = File.Create(fileName))
			{
				BinaryWriter bw = new BinaryWriter(fs);
				dict.Save(bw);
				forward->Write(bw,0);
				backward->Write(bw,0);
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
				m.dict.Load(br);
				m.forward->Read(br);
				m.backward->Read(br);
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
				return dict.Count;
			}
		}

		/// <summary>
		/// Gets the total number of tuples in the model.
		/// </summary>
		public int TotalTupleCount
		{
			get
			{
				return forward->Count(ORDER);
			}
		}

		/// <summary>
		/// Gets the total number of nodes in the model.
		/// </summary>
		public int TotalNodeCount
		{
			get
			{
				return forward->Count();
			}
		}

		/// <summary>
		/// Gets the total memory, in bytes, used by the model's tree.
		/// </summary>
		public int TotalBytesUsed
		{
			get
			{
				return forward->CountBytes();
			}
		}
	}
}
