using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ErectRoom
{
    public static class DllInjector
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        const int PROCESS_ALL_ACCESS = 0x001F0FFF;
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 0x04;

        static IntPtr GetLoadLibraryInTarget(int processId)
        {
            // Find kernel32 base in the target process via its module list
            var targetProcess = Process.GetProcessById(processId);
            foreach (ProcessModule mod in targetProcess.Modules)
            {
                if (mod.ModuleName.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the offset of LoadLibraryW in our own kernel32
                    IntPtr ourKernel32 = GetModuleHandle("kernel32.dll");
                    IntPtr ourLoadLib = GetProcAddress(ourKernel32, "LoadLibraryW");
                    long offset = ourLoadLib.ToInt64() - ourKernel32.ToInt64();

                    // Apply the same offset to the target's kernel32 base
                    return new IntPtr(mod.BaseAddress.ToInt64() + offset);
                }
            }
            throw new Exception("Could not find kernel32.dll in target process modules");
        }

        public static void Inject(int processId, string dllPath)
        {
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("DLL not found", dllPath);

            Console.WriteLine($"[Injector] Target PID: {processId}");
            Console.WriteLine($"[Injector] DLL path: {dllPath}");

            IntPtr pLoadLib;
            try
            {
                pLoadLib = GetLoadLibraryInTarget(processId);
                Console.WriteLine($"[Injector] LoadLibraryW in target: {pLoadLib:X}");
            }
            catch (Exception ex)
            {
                // Fallback to our own process address — may work if ASLR is off
                Console.WriteLine($"[Injector] Module enum failed ({ex.Message}), falling back to local address");
                pLoadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            }

            var hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed");

            Console.WriteLine($"[Injector] OpenProcess handle: {hProcess:X}");

            try
            {
                var dllPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");

                var pRemoteMem = VirtualAllocEx(hProcess, IntPtr.Zero,
                    (uint)dllPathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                if (pRemoteMem == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed");

                Console.WriteLine($"[Injector] Remote memory allocated at: {pRemoteMem:X}");

                bool wrote = WriteProcessMemory(hProcess, pRemoteMem, dllPathBytes,
                    (uint)dllPathBytes.Length, out UIntPtr bytesWritten);

                Console.WriteLine($"[Injector] WriteProcessMemory: {wrote}, bytes written: {bytesWritten}");

                if (!wrote)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteProcessMemory failed");

                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0,
                    pLoadLib, pRemoteMem, 0, out uint threadId);

                if (hThread == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateRemoteThread failed");

                Console.WriteLine($"[Injector] Remote thread created, ID: {threadId}");
                CloseHandle(hThread);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }
}