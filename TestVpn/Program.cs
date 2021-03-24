using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Test
{
    class Program
    {
        [DllImport("wintun.dll", SetLastError = true)]
        extern static IntPtr WintunCreateAdapter(IntPtr a, IntPtr b, IntPtr pp, IntPtr p);
    
        [DllImport("wintun.dll", SetLastError = true)]
        extern static IntPtr WintunStartSession(IntPtr p, int n);

        [DllImport("wintun.dll", SetLastError = true)]
        extern static IntPtr WintunOpenAdapter(IntPtr a, IntPtr b);

        [DllImport("wintun.dll", SetLastError = true)]
        extern static IntPtr WintunReceivePacket(IntPtr han, out int size);

        [DllImport("wintun.dll", SetLastError = true)]
        extern static IntPtr WintunAllocateSendPacket(IntPtr han, int size);

        [DllImport("wintun.dll", SetLastError = true)]
        extern static void WintunSendPacket(IntPtr han, IntPtr buffer);

        static string POOL_NAME = "WireGuard";

        static string NAME_NAME = "Test";

        static IntPtr WintunCreateAdapter()
        {
            return WintunCreateAdapter(Marshal.StringToHGlobalUni(POOL_NAME),
                Marshal.StringToHGlobalUni(NAME_NAME), IntPtr.Zero, IntPtr.Zero);


        }

        static IntPtr WintunOpenAdapter()
        {
            return WintunOpenAdapter(Marshal.StringToHGlobalUni(POOL_NAME),
                Marshal.StringToHGlobalUni(NAME_NAME));


        }


        static void Main(string[] args)
        {
            IntPtr sess = WintunStartSession(WintunCreateAdapter(), 0x400000);


            while (true)
            {
                IntPtr p = WintunReceivePacket(sess, out int size);

                if (p == IntPtr.Zero)
                {

                }
                else
                {
                    Console.WriteLine(size);
                }
            }



        }
    }
}
