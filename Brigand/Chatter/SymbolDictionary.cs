using System.Collections.Generic;
using System.IO;

namespace Brigand.MarkovModel
{
	class SymbolDictionary
	{
		private List<Symbol> _store = new List<Symbol>();
		private Dictionary<Symbol, ushort> _lookup = new Dictionary<Symbol,ushort>();

		public ushort AddSymbol(Symbol s)
		{
			if (_lookup.ContainsKey(s))
			{
				return _lookup[s];
			}

			_store.Add(s);
			ushort idx = (ushort)(_store.Count - 1);
			_lookup.Add(s, idx);
			return idx;
		}

		public Symbol this[ushort token]
		{
			get
			{
				return _store[token];
			}
		}

		public ushort this[Symbol sym]
		{
			get
			{
				return _lookup[sym];
			}
		}

		public int Count
		{
			get
			{
				return _store.Count;
			}
		}

		public void Save(BinaryWriter bw)
		{
			bw.Write((ushort)_store.Count);
			for (int i = 0; i < _store.Count; i++)
			{
				_store[i].Save(bw);
			}
		}

		public void Load(BinaryReader br)
		{
			int count = (int)br.ReadUInt16();
			for (ushort i = 0; i < count; i++)
			{
				_store.Add(Symbol.Load(br));
				if (!_lookup.ContainsKey(_store[i]))
				{
					_lookup.Add(_store[i], i);
				}
			}
		}
	}
}
