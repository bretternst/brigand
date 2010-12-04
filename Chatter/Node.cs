using System.IO;
using System.Runtime.InteropServices;

namespace Brigand
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
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
			if (branch < 1)
			{
				return 0;
			}
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
					{
						return middle + 1;
					}
					min = middle + 1;
				}
				else
				{
					if (min == middle)
					{
						return middle;
					}
					max = middle - 1;
				}
			}
		}

		public Node* Find(ushort token)
		{
			bool found;
			int i = this.Search(token, out found);
			if (found)
			{
				return children[i];
			}
			return null;
		}

		public Node* FindOrAdd(ushort token)
		{
			bool found;
			int i = Search(token, out found);
			if (found)
			{
				return children[i];
			}
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
			{
				for (int i = 0; i < branch; i++)
				{
					c += children[i]->Count(level + 1, targetLevel);
				}
			}
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
				{
					m += children[i]->CountBytes();
				}
			}
			return m;
		}
	}
}
