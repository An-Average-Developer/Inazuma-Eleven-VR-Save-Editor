using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Services
{
    /// <summary>
    /// Service for enabling unlimited spirits in team (removes team dock limit for hero spirits)
    /// </summary>
    public class UnlimitedSpiritsService
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

        // Code cave and hook addresses
        private IntPtr _teamDockHero1CodeCave = IntPtr.Zero;
        private IntPtr _teamDockHero1HookAddress = IntPtr.Zero;
        private IntPtr _teamDockHero2HookAddress = IntPtr.Zero;

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
                DisableUnlimitedSpirits();
            }

            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
            _targetProcess = null;
        }

        public bool EnableUnlimitedSpirits()
        {
            if (_processHandle == IntPtr.Zero || _moduleBase == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for first pattern: 75 03 8B 58 10 49
                // This is at nie.exe+BBDABE
                byte[] aobPattern1 = new byte[] { 0x75, 0x03, 0x8B, 0x58, 0x10, 0x49 };
                byte[] aobMask1 = new byte[] { 1, 1, 1, 1, 1, 1 }; // All bytes must match
                _teamDockHero1HookAddress = AOBScan(aobPattern1, aobMask1);

                if (_teamDockHero1HookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Team Dock Hero 1 AOB pattern.\n\n" +
                        "Make sure:\n" +
                        "1. The game is running\n" +
                        "2. The game version is correct");
                }

                // AOB scan for second pattern: 75 * 8B 40 10 48
                // This is at nie.exe+D82C67
                byte[] aobPattern2 = new byte[] { 0x75, 0x00, 0x8B, 0x40, 0x10, 0x48 };
                byte[] aobMask2 = new byte[] { 1, 0, 1, 1, 1, 1 }; // Wildcard for second byte
                _teamDockHero2HookAddress = AOBScan(aobPattern2, aobMask2);

                if (_teamDockHero2HookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Team Dock Hero 2 AOB pattern.");
                }

                // Allocate code cave for first injection
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _teamDockHero1CodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_teamDockHero1CodeCave != IntPtr.Zero)
                    {
                        long distance = _teamDockHero1CodeCave.ToInt64() - _teamDockHero1HookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _teamDockHero1CodeCave, 0, MEM_RELEASE);
                            _teamDockHero1CodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_teamDockHero1CodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate code cave for Team Dock Hero 1");
                }

                // Build code cave for first injection
                // newmem:
                //   jne return        ; 75 XX (will calculate offset)
                //   mov ebx,[rax+10]  ; 8B 58 10
                //   cmp ebx,5         ; 83 FB 05
                //   jl code           ; 7C XX
                //   mov ebx,4         ; BB 04 00 00 00
                // code:
                //   jmp return        ; E9 XX XX XX XX
                List<byte> codeCave1 = new List<byte>();

                // jne return (placeholder, will patch offset later)
                codeCave1.Add(0x75);
                int jneReturnOffset = codeCave1.Count;
                codeCave1.Add(0x00); // Placeholder

                // mov ebx,[rax+10]
                codeCave1.AddRange(new byte[] { 0x8B, 0x58, 0x10 });

                // cmp ebx,5
                codeCave1.AddRange(new byte[] { 0x83, 0xFB, 0x05 });

                // jl code (skip mov ebx,4)
                codeCave1.Add(0x7C);
                codeCave1.Add(0x05); // Skip 5 bytes (mov ebx,4)

                // mov ebx,4
                codeCave1.AddRange(new byte[] { 0xBB, 0x04, 0x00, 0x00, 0x00 });

                // code: jmp return (back to original code)
                int codeLabel = codeCave1.Count;
                codeCave1.Add(0xE9); // jmp
                int jmpReturnOffset = codeCave1.Count;
                codeCave1.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Placeholder

                // Calculate offsets
                int returnLabel = codeCave1.Count;

                // Patch jne offset (from jne to return label)
                int jneOffset = returnLabel - jneReturnOffset - 1;
                codeCave1[jneReturnOffset] = (byte)jneOffset;

                // Calculate jmp back to original code
                // Original code continues at hook address + 5 (size of original instruction we're replacing)
                long returnAddress = _teamDockHero1HookAddress.ToInt64() + 5;
                long jmpTarget = returnAddress - (_teamDockHero1CodeCave.ToInt64() + codeLabel + 5);
                byte[] jmpBytes = BitConverter.GetBytes((int)jmpTarget);
                for (int i = 0; i < 4; i++)
                {
                    codeCave1[jmpReturnOffset + i] = jmpBytes[i];
                }

                // Write code cave
                if (!WriteProcessMemory(_processHandle, _teamDockHero1CodeCave, codeCave1.ToArray(), codeCave1.Count, out _))
                {
                    throw new Exception("Failed to write code cave for Team Dock Hero 1");
                }

                // Create jump to code cave from hook point
                long hookToCodeCave = _teamDockHero1CodeCave.ToInt64() - _teamDockHero1HookAddress.ToInt64() - 5;
                byte[] jmpToCodeCave = new byte[5];
                jmpToCodeCave[0] = 0xE9; // jmp
                byte[] offsetBytes = BitConverter.GetBytes((int)hookToCodeCave);
                Array.Copy(offsetBytes, 0, jmpToCodeCave, 1, 4);

                // Write jump at hook point (replacing 5 bytes: 75 03 8B 58 10)
                uint oldProtect;
                VirtualProtectEx(_processHandle, _teamDockHero1HookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect);
                if (!WriteProcessMemory(_processHandle, _teamDockHero1HookAddress, jmpToCodeCave, 5, out _))
                {
                    throw new Exception("Failed to write jump for Team Dock Hero 1");
                }
                VirtualProtectEx(_processHandle, _teamDockHero1HookAddress, 5, oldProtect, out _);

                // Second injection: Change 75 to EB (jne to jmp)
                byte[] jmpInstruction = new byte[] { 0xEB };
                VirtualProtectEx(_processHandle, _teamDockHero2HookAddress, 1, PAGE_EXECUTE_READWRITE, out oldProtect);
                if (!WriteProcessMemory(_processHandle, _teamDockHero2HookAddress, jmpInstruction, 1, out _))
                {
                    throw new Exception("Failed to write jump for Team Dock Hero 2");
                }
                VirtualProtectEx(_processHandle, _teamDockHero2HookAddress, 1, oldProtect, out _);

                _isEnabled = true;
                return true;
            }
            catch (Exception)
            {
                // Clean up on failure
                if (_teamDockHero1CodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _teamDockHero1CodeCave, 0, MEM_RELEASE);
                    _teamDockHero1CodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool DisableUnlimitedSpirits()
        {
            if (_processHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                bool success1 = true;
                bool success2 = true;

                // Restore first hook original bytes: 75 03 8B 58 10
                if (_teamDockHero1HookAddress != IntPtr.Zero)
                {
                    byte[] originalBytes1 = new byte[] { 0x75, 0x03, 0x8B, 0x58, 0x10 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _teamDockHero1HookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success1 = WriteProcessMemory(_processHandle, _teamDockHero1HookAddress, originalBytes1, 5, out _);
                    VirtualProtectEx(_processHandle, _teamDockHero1HookAddress, 5, oldProtect, out _);
                }

                // Restore second hook original bytes: 75
                if (_teamDockHero2HookAddress != IntPtr.Zero)
                {
                    byte[] originalBytes2 = new byte[] { 0x75 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _teamDockHero2HookAddress, 1, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success2 = WriteProcessMemory(_processHandle, _teamDockHero2HookAddress, originalBytes2, 1, out _);
                    VirtualProtectEx(_processHandle, _teamDockHero2HookAddress, 1, oldProtect, out _);
                }

                // Free code cave
                if (_teamDockHero1CodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _teamDockHero1CodeCave, 0, MEM_RELEASE);
                    _teamDockHero1CodeCave = IntPtr.Zero;
                }

                _isEnabled = false;
                return success1 && success2;
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
