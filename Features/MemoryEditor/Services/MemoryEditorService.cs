using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Services
{
    public class MemoryEditorService
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private IntPtr _processHandle = IntPtr.Zero;
        private bool _isAttached;
        private string _processName = "nie";
        private IntPtr _moduleBase = IntPtr.Zero;
        private Process? _targetProcess;
        private IntPtr _storeItemMultiplierCodeCave1 = IntPtr.Zero; // For nie.exe+21EF25
        private IntPtr _storeItemMultiplierCodeCave2 = IntPtr.Zero; // For nie.exe+21DEE5
        private IntPtr _storeItemMultiplierCodeCave3 = IntPtr.Zero; // For nie.exe+21E225

        public bool IsAttached => _isAttached;

        public bool AttachToProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName(_processName);
                if (!processes.Any())
                {
                    return false;
                }

                _targetProcess = processes[0];

                _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, _targetProcess.Id);

                if (_processHandle == IntPtr.Zero)
                {
                    return false;
                }

                _moduleBase = _targetProcess.MainModule?.BaseAddress ?? IntPtr.Zero;

                if (_moduleBase == IntPtr.Zero)
                {
                    CloseHandle(_processHandle);
                    _processHandle = IntPtr.Zero;
                    return false;
                }

                _isAttached = true;
                return true;
            }
            catch (Exception)
            {
                _isAttached = false;
                if (_processHandle != IntPtr.Zero)
                {
                    CloseHandle(_processHandle);
                    _processHandle = IntPtr.Zero;
                }
                return false;
            }
        }

        public void DetachFromProcess()
        {
            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
            _isAttached = false;
            _targetProcess = null;
        }

        public int ReadValue(long baseOffset, int[] offsets)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr address = ResolvePointerChain(baseOffset, offsets);

                byte[] buffer = new byte[4];
                if (ReadProcessMemory(_processHandle, address, buffer, buffer.Length, out _))
                {
                    return BitConverter.ToInt32(buffer, 0);
                }

                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public bool WriteValue(long baseOffset, int[] offsets, int value)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr address = ResolvePointerChain(baseOffset, offsets);

                byte[] buffer = BitConverter.GetBytes(value);
                return WriteProcessMemory(_processHandle, address, buffer, buffer.Length, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public float ReadFloatValue(long baseOffset, int[] offsets)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr address = ResolvePointerChain(baseOffset, offsets);

                byte[] buffer = new byte[4];
                if (ReadProcessMemory(_processHandle, address, buffer, buffer.Length, out _))
                {
                    return BitConverter.ToSingle(buffer, 0);
                }

                return 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        public bool WriteFloatValue(long baseOffset, int[] offsets, float value)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr address = ResolvePointerChain(baseOffset, offsets);

                byte[] buffer = BitConverter.GetBytes(value);
                return WriteProcessMemory(_processHandle, address, buffer, buffer.Length, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IntPtr ResolvePointerChain(long baseOffset, int[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
            {
                return IntPtr.Add(_moduleBase, (int)baseOffset);
            }

            IntPtr address = IntPtr.Add(_moduleBase, (int)baseOffset);

            byte[] buffer = new byte[IntPtr.Size];
            if (!ReadProcessMemory(_processHandle, address, buffer, buffer.Length, out _))
            {
                return IntPtr.Zero;
            }

            address = IntPtr.Size == 8
                ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                : new IntPtr(BitConverter.ToInt32(buffer, 0));

            for (int i = 0; i < offsets.Length - 1; i++)
            {
                address = IntPtr.Add(address, offsets[i]);

                if (!ReadProcessMemory(_processHandle, address, buffer, buffer.Length, out _))
                {
                    return IntPtr.Zero;
                }

                address = IntPtr.Size == 8
                    ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                    : new IntPtr(BitConverter.ToInt32(buffer, 0));
            }

            address = IntPtr.Add(address, offsets[offsets.Length - 1]);

            return address;
        }

        public bool IsProcessRunning()
        {
            var processes = Process.GetProcessesByName(_processName);
            return processes.Any();
        }

        public string GetProcessStatus()
        {
            if (!IsProcessRunning())
            {
                return "Process not running";
            }

            if (_isAttached)
            {
                return "Attached";
            }

            return "Process running (not attached)";
        }

        public bool WriteBytes(long address, byte[] bytes)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr targetAddress = new IntPtr(_moduleBase.ToInt64() + address);

                // Change memory protection to writable
                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, targetAddress, (uint)bytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect))
                    return false;

                // Write the bytes
                bool success = WriteProcessMemory(_processHandle, targetAddress, bytes, bytes.Length, out _);

                // Restore original protection
                VirtualProtectEx(_processHandle, targetAddress, (uint)bytes.Length, oldProtect, out _);

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool InjectAtAddress(long hookOffset, int bytesToSkip, ref IntPtr codeCave)
        {
            try
            {
                IntPtr hookAddress = new IntPtr(_moduleBase.ToInt64() + hookOffset);

                // Try multiple allocation attempts at different offsets to find memory within ±2GB range
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    codeCave = VirtualAllocEx(_processHandle, preferredAddress, 2048, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (codeCave != IntPtr.Zero)
                    {
                        // Check if within range
                        long distance = codeCave.ToInt64() - hookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break; // Found a good location
                        }
                        else
                        {
                            // Too far, free it and try next offset
                            VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                            codeCave = IntPtr.Zero;
                        }
                    }
                }

                // If all preferred locations failed, try letting the system choose (less likely to work but worth a try)
                if (codeCave == IntPtr.Zero)
                {
                    codeCave = VirtualAllocEx(_processHandle, IntPtr.Zero, 2048, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (codeCave != IntPtr.Zero)
                    {
                        long distance = codeCave.ToInt64() - hookAddress.ToInt64();
                        if (Math.Abs(distance) > 0x7FFFFFFF)
                        {
                            VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                            codeCave = IntPtr.Zero;
                        }
                    }
                }

                if (codeCave == IntPtr.Zero)
                {
                    throw new Exception($"Failed to allocate memory within ±2GB range of hook at nie.exe+{hookOffset:X}");
                }

                // Injected code: sub ecx,[rsi+10]; imul ecx,ecx,999; add ecx,[rsi+10]; mov [rsi+10],ecx; jmp back
                byte[] injectedCode = new byte[20];
                injectedCode[0] = 0x2B; injectedCode[1] = 0x4E; injectedCode[2] = 0x10;
                injectedCode[3] = 0x69; injectedCode[4] = 0xC9; injectedCode[5] = 0x99;
                injectedCode[6] = 0x09; injectedCode[7] = 0x00; injectedCode[8] = 0x00;
                injectedCode[9] = 0x03; injectedCode[10] = 0x4E; injectedCode[11] = 0x10;
                injectedCode[12] = 0x89; injectedCode[13] = 0x4E; injectedCode[14] = 0x10;

                // Calculate jump back
                IntPtr returnAddress = new IntPtr(hookAddress.ToInt64() + bytesToSkip);
                long jmpOffset = returnAddress.ToInt64() - (codeCave.ToInt64() + 20);

                injectedCode[15] = 0xE9;
                byte[] offsetBytes = BitConverter.GetBytes((int)jmpOffset);
                Array.Copy(offsetBytes, 0, injectedCode, 16, 4);

                // Write injected code
                if (!WriteProcessMemory(_processHandle, codeCave, injectedCode, injectedCode.Length, out _))
                {
                    int error = Marshal.GetLastWin32Error();
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                    throw new Exception($"WriteProcessMemory failed writing injected code at 0x{codeCave.ToInt64():X}. Error: {error}");
                }

                // Create hook
                long jmpToCodeCave = codeCave.ToInt64() - (hookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[5];
                hookBytes[0] = 0xE9;
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);

                bool hookSuccess = WriteBytes(hookOffset, hookBytes);
                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                    throw new Exception($"WriteBytes failed writing hook at nie.exe+{hookOffset:X}");
                }

                return true;
            }
            catch (Exception)
            {
                if (codeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool InjectStoreItemMultiplier()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Inject at all three item purchase locations (InjectAtAddress will throw detailed exceptions on failure)
                InjectAtAddress(0x21EF25, 7, ref _storeItemMultiplierCodeCave1); // First - Hissatsus and Kenshins (return to 21EF2A)
                InjectAtAddress(0x21DEE5, 5, ref _storeItemMultiplierCodeCave2); // Second - Items unless boots and kizuna items (return to 21DEEA)
                InjectAtAddress(0x21E225, 5, ref _storeItemMultiplierCodeCave3); // Third - Boots and kizuna items (return to 21E22A)

                return true;
            }
            catch (Exception ex)
            {
                RemoveStoreItemMultiplier();
                throw new Exception($"Store item multiplier injection failed: {ex.Message}", ex);
            }
        }

        public bool RemoveStoreItemMultiplier()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Restore original bytes at all three locations (5 bytes each)
                byte[] originalBytes1 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+21EF25: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes2 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+21DEE5: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes3 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+21E225: mov [rsi+10],ecx; mov eax,ebx

                bool success1 = WriteBytes(0x21EF25, originalBytes1);
                bool success2 = WriteBytes(0x21DEE5, originalBytes2);
                bool success3 = WriteBytes(0x21E225, originalBytes3);

                // Free allocated memory
                if (_storeItemMultiplierCodeCave1 != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _storeItemMultiplierCodeCave1, 0, MEM_RELEASE);
                    _storeItemMultiplierCodeCave1 = IntPtr.Zero;
                }

                if (_storeItemMultiplierCodeCave2 != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _storeItemMultiplierCodeCave2, 0, MEM_RELEASE);
                    _storeItemMultiplierCodeCave2 = IntPtr.Zero;
                }

                if (_storeItemMultiplierCodeCave3 != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _storeItemMultiplierCodeCave3, 0, MEM_RELEASE);
                    _storeItemMultiplierCodeCave3 = IntPtr.Zero;
                }

                return success1 && success2 && success3;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
