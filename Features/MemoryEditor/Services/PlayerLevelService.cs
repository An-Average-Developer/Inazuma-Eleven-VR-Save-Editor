using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Services
{
    /// <summary>
    /// Service for setting player level (max 99)
    /// Active when Team Dock menu is open
    /// </summary>
    public class PlayerLevelService
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
        private IntPtr _moduleBase = IntPtr.Zero;
        private Process? _targetProcess;
        private bool _isEnabled = false;

        private IntPtr _codeCave = IntPtr.Zero;
        private IntPtr _hookAddress = IntPtr.Zero;
        private IntPtr _cfPlayerLevelAddress = IntPtr.Zero;
        private byte[] _originalBytes = Array.Empty<byte>();

        public bool IsEnabled => _isEnabled;

        public bool AttachToProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName("nie");
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

                return true;
            }
            catch (Exception)
            {
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
            if (_isEnabled)
            {
                DisablePlayerLevel();
            }

            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
            _targetProcess = null;
        }

        public bool EnablePlayerLevel(int level)
        {
            if (_processHandle == IntPtr.Zero || _moduleBase == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            if (level < 1 || level > 99)
            {
                throw new ArgumentException("Level must be between 1 and 99", nameof(level));
            }

            try
            {
                // AOB scan for pattern: FF * 48 * * * 88 5D * 49 8B CF
                // This is at nie.exe+BBB414
                byte[] aobPattern = new byte[] { 0xFF, 0x00, 0x48, 0x00, 0x00, 0x00, 0x88, 0x5D, 0x00, 0x49, 0x8B, 0xCF };
                byte[] aobMask = new byte[] { 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1, 1 };
                _hookAddress = AOBScan(aobPattern, aobMask);

                if (_hookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Player Level AOB pattern.\n\n" +
                        "Make sure:\n" +
                        "1. The game is running\n" +
                        "2. You are in the Team Dock menu\n" +
                        "3. The game version is correct");
                }

                // The injection point is at offset +6 from the found pattern (88 5D 20)
                _hookAddress = new IntPtr(_hookAddress.ToInt64() + 6);

                // Read original bytes (6 bytes: 88 5D 20 49 8B CF)
                _originalBytes = new byte[6];
                if (!ReadProcessMemory(_processHandle, _hookAddress, _originalBytes, 6, out _))
                {
                    throw new Exception("Failed to read original bytes");
                }

                // Allocate code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _codeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_codeCave != IntPtr.Zero)
                    {
                        long distance = _codeCave.ToInt64() - _hookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _codeCave, 0, MEM_RELEASE);
                            _codeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_codeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate code cave for Player Level");
                }

                // Build code cave
                // newmem:
                //   readMem(INJECTplayerlevel+6,6) - original 6 bytes
                //   push rax
                //   push rdx
                //   mov rax,[rcx+8]
                //   mov dx,[cfPlayerLevel]
                //   mov word ptr[rax+0C],dx
                //   pop rdx
                //   pop rax
                //   jmp return
                // cfPlayerLevel:
                //   dw level

                var codeCave = new System.Collections.Generic.List<byte>();

                // Original bytes (88 5D 20 49 8B CF)
                codeCave.AddRange(_originalBytes);

                // push rax
                codeCave.Add(0x50);

                // push rdx
                codeCave.Add(0x52);

                // mov rax,[rcx+8]
                codeCave.AddRange(new byte[] { 0x48, 0x8B, 0x41, 0x08 });

                // mov dx,[cfPlayerLevel] - we'll use RIP-relative addressing
                // The cfPlayerLevel will be at the end of our code cave
                // At this point: 6 (original) + 1 (push rax) + 1 (push rdx) + 4 (mov rax) = 12 bytes
                // This instruction is 7 bytes, so after it RIP will be at 12 + 7 = 19
                // cfPlayerLevel will be at: 19 (current end) + 4 (mov word) + 1 (pop rdx) + 1 (pop rax) + 5 (jmp) = 30
                // Offset = 30 - 19 = 11
                int cfPlayerLevelOffset = 11; // Offset from end of this instruction to cfPlayerLevel
                codeCave.AddRange(new byte[] { 0x66, 0x8B, 0x15 }); // mov dx,[rip+offset]
                codeCave.AddRange(BitConverter.GetBytes(cfPlayerLevelOffset));

                // mov word ptr[rax+0C],dx
                codeCave.AddRange(new byte[] { 0x66, 0x89, 0x50, 0x0C });

                // pop rdx
                codeCave.Add(0x5A);

                // pop rax
                codeCave.Add(0x58);

                // jmp return (back to original code after our hook)
                long returnAddress = _hookAddress.ToInt64() + 6;
                long jmpOffset = returnAddress - (_codeCave.ToInt64() + codeCave.Count + 5);
                codeCave.Add(0xE9); // jmp
                codeCave.AddRange(BitConverter.GetBytes((int)jmpOffset));

                // cfPlayerLevel: dw level
                _cfPlayerLevelAddress = new IntPtr(_codeCave.ToInt64() + codeCave.Count);
                codeCave.AddRange(BitConverter.GetBytes((short)level));

                // Write code cave
                if (!WriteProcessMemory(_processHandle, _codeCave, codeCave.ToArray(), codeCave.Count, out _))
                {
                    throw new Exception("Failed to write code cave for Player Level");
                }

                // Create jump to code cave from hook point
                long hookToCodeCave = _codeCave.ToInt64() - _hookAddress.ToInt64() - 5;
                byte[] jmpToCodeCave = new byte[6];
                jmpToCodeCave[0] = 0xE9; // jmp
                byte[] offsetBytes = BitConverter.GetBytes((int)hookToCodeCave);
                Array.Copy(offsetBytes, 0, jmpToCodeCave, 1, 4);
                jmpToCodeCave[5] = 0x90; // nop

                // Write jump at hook point
                uint oldProtect;
                VirtualProtectEx(_processHandle, _hookAddress, 6, PAGE_EXECUTE_READWRITE, out oldProtect);
                if (!WriteProcessMemory(_processHandle, _hookAddress, jmpToCodeCave, 6, out _))
                {
                    throw new Exception("Failed to write jump for Player Level");
                }
                VirtualProtectEx(_processHandle, _hookAddress, 6, oldProtect, out _);

                _isEnabled = true;
                return true;
            }
            catch (Exception)
            {
                // Clean up on failure
                if (_codeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _codeCave, 0, MEM_RELEASE);
                    _codeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool UpdatePlayerLevel(int level)
        {
            if (!_isEnabled || _cfPlayerLevelAddress == IntPtr.Zero)
            {
                return false;
            }

            if (level < 1 || level > 99)
            {
                throw new ArgumentException("Level must be between 1 and 99", nameof(level));
            }

            try
            {
                byte[] levelBytes = BitConverter.GetBytes((short)level);
                uint oldProtect;
                VirtualProtectEx(_processHandle, _cfPlayerLevelAddress, 2, PAGE_EXECUTE_READWRITE, out oldProtect);
                bool success = WriteProcessMemory(_processHandle, _cfPlayerLevelAddress, levelBytes, 2, out _);
                VirtualProtectEx(_processHandle, _cfPlayerLevelAddress, 2, oldProtect, out _);
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool DisablePlayerLevel()
        {
            if (_processHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Restore original bytes
                if (_hookAddress != IntPtr.Zero && _originalBytes.Length > 0)
                {
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _hookAddress, 6, PAGE_EXECUTE_READWRITE, out oldProtect);
                    WriteProcessMemory(_processHandle, _hookAddress, _originalBytes, 6, out _);
                    VirtualProtectEx(_processHandle, _hookAddress, 6, oldProtect, out _);
                }

                // Free code cave
                if (_codeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _codeCave, 0, MEM_RELEASE);
                    _codeCave = IntPtr.Zero;
                }

                _isEnabled = false;
                _cfPlayerLevelAddress = IntPtr.Zero;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IntPtr AOBScan(byte[] pattern, byte[] mask)
        {
            if (_processHandle == IntPtr.Zero || _moduleBase == IntPtr.Zero || _targetProcess == null)
            {
                return IntPtr.Zero;
            }

            try
            {
                long moduleSize = _targetProcess.MainModule?.ModuleMemorySize ?? 0;
                if (moduleSize == 0)
                {
                    return IntPtr.Zero;
                }

                int chunkSize = 4096 * 1024; // 4MB chunks
                byte[] buffer = new byte[chunkSize];

                for (long offset = 0; offset < moduleSize; offset += chunkSize - pattern.Length)
                {
                    IntPtr currentAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    int bytesToRead = (int)Math.Min(chunkSize, moduleSize - offset);

                    if (ReadProcessMemory(_processHandle, currentAddress, buffer, bytesToRead, out int bytesRead))
                    {
                        for (int i = 0; i < bytesRead - pattern.Length; i++)
                        {
                            bool found = true;
                            for (int j = 0; j < pattern.Length; j++)
                            {
                                if (mask[j] == 1 && buffer[i + j] != pattern[j])
                                {
                                    found = false;
                                    break;
                                }
                            }

                            if (found)
                            {
                                return new IntPtr(currentAddress.ToInt64() + i);
                            }
                        }
                    }
                }

                return IntPtr.Zero;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }
    }
}
