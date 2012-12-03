using System;
using System.Runtime.InteropServices;

namespace Brigand
{
	unsafe class Memory
	{
		public static void* Alloc(int size)
		{
			IntPtr p = Marshal.AllocHGlobal(size);
			for (int i = 0; i < size; i++)
			{
				Marshal.WriteByte(p, i, 0);
			}
			return (void*)p;
		}

		public static void Free(void* block)
		{
			Marshal.FreeHGlobal((IntPtr)block);
		}
	}
}
