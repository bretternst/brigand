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
		const int HEAP_ZERO_MEMORY = 0x0040;

		[DllImport("kernel32")]
		static extern IntPtr GlobalAlloc(int flags, int size);
		[DllImport("kernel32")]
		static extern IntPtr GlobalReAlloc(void* block, int size, int flags);
		[DllImport("kernel32")]
		static extern bool GlobalFree(void* block);

		[DllImport("kernel32")]
		static extern uint GetLastError();

		public static void* Alloc(int size)
		{
			IntPtr mem = GlobalAlloc(HEAP_ZERO_MEMORY, size);
			if (mem == IntPtr.Zero)
			{
				Console.WriteLine("failed allocation");
			}
			return (void*)mem;
		}

		public static void* ReAlloc(void* block, int size)
		{
			IntPtr mem = GlobalReAlloc(block, size, HEAP_ZERO_MEMORY);
			if (mem == IntPtr.Zero)
			{
				Console.WriteLine("FAILED REALLOCATION " + GetLastError());
				Console.WriteLine("size requested " + size.ToString());
			}
			else
			{
				Console.WriteLine("SUCCESS REALLOCATION");
			}
			return (void*)mem;
		}

		public static bool Free(void* block)
		{
			return GlobalFree(block);
		}
	}
}
