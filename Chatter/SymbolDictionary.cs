using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Brigand.MarkovModel
{
	class SymbolDictionary
	{
		private List<Symbol> store = new List<Symbol>();
		private Dictionary<Symbol, ushort> lookup = new Dictionary<Symbol,ushort>();

		public ushort AddSymbol(Symbol s)
		{
			if (lookup.ContainsKey(s))
			{
				return lookup[s];
			}

			store.Add(s);
			ushort idx = (ushort)(store.Count - 1);
			lookup.Add(s, idx);
			return idx;
		}

		public Symbol this[ushort token]
		{
			get
			{
				return store[token];
			}
		}

		public ushort this[Symbol sym]
		{
			get
			{
				return lookup[sym];
			}
		}

		public int Count
		{
			get
			{
				return store.Count;
			}
		}

		public void Save(BinaryWriter bw)
		{
			bw.Write((ushort)store.Count);
			for (int i = 0; i < store.Count; i++)
			{
				store[i].Save(bw);
			}
		}

		public void Load(BinaryReader br)
		{
			int count = (int)br.ReadUInt16();
			for (ushort i = 0; i < count; i++)
			{
				store.Add(Symbol.Load(br));
				if(!lookup.ContainsKey(store[i]))
					lookup.Add(store[i], i);
			}
		}
	}
}
