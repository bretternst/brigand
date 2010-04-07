using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;

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
