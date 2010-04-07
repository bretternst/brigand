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
			return (void*)Marshal.AllocHGlobal(size);
		}

		public static void Free(void* block)
		{
			Marshal.FreeHGlobal((IntPtr)block);
		}
	}
}
