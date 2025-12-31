using System;
using System.Collections.Generic;
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
        private IntPtr _storeItemMultiplierCodeCave1 = IntPtr.Zero; // For nie.exe+221AD5
        private IntPtr _storeItemMultiplierCodeCave2 = IntPtr.Zero; // For nie.exe+220A95
        private IntPtr _storeItemMultiplierCodeCave3 = IntPtr.Zero; // For nie.exe+220DD5
        private IntPtr _heroSpiritIncrementCodeCave = IntPtr.Zero; // For Heroes Spirits AOB injection
        private IntPtr _eliteSpiritIncrementCodeCave = IntPtr.Zero; // For Elite Spirits AOB injection
        private IntPtr _passiveValueCodeCave = IntPtr.Zero; // For Passive Value injection
        private IntPtr _passiveValAdrAddress = IntPtr.Zero; // Address where passiveValAdr is stored
        private IntPtr _passiveValTypeAddress = IntPtr.Zero; // Address where passiveValType is stored
        private IntPtr _passiveValueHookAddress = IntPtr.Zero; // Address of the hook
        private IntPtr _spiritCardInjectionCodeCave = IntPtr.Zero; // For Spirit Card injection
        private IntPtr _spiritCardHookAddress = IntPtr.Zero; // Address of the spirit card hook
        private IntPtr _cfHerospiritAddTypeAddress = IntPtr.Zero; // cfHerospiritAddType address
        private IntPtr _cfHerospiritIDAddress = IntPtr.Zero; // cfHerospiritID address
        private bool _isSpiritCardInjectionEnabled = false;
        private IntPtr _unlimitedHeroesCodeCave = IntPtr.Zero; // For Unlimited Heroes injection
        private IntPtr _unlimitedHeroesHookAddress = IntPtr.Zero; // Hook address for Unlimited Heroes
        private IntPtr _freeBuySpiritMarketCodeCave = IntPtr.Zero; // For Free Buy Spirit Market injection
        private IntPtr _freeBuySpiritMarketHookAddress = IntPtr.Zero; // First hook address
        private IntPtr _freeBuySpiritMarketLvHookAddress = IntPtr.Zero; // Second hook address (level)
        private IntPtr _cfFBspiritmarketSpQuanAddress = IntPtr.Zero; // Spirit quantity address

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

        public byte[]? ReadBytes(long address, int length)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                IntPtr targetAddress = new IntPtr(_moduleBase.ToInt64() + address);
                byte[] buffer = new byte[length];

                // Read the bytes
                bool success = ReadProcessMemory(_processHandle, targetAddress, buffer, length, out int bytesRead);

                if (success && bytesRead == length)
                {
                    return buffer;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
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
                InjectAtAddress(0x221CE5, 7, ref _storeItemMultiplierCodeCave1); // First - Hissatsus and Kenshins (return to 221CEC)
                InjectAtAddress(0x220CA5, 5, ref _storeItemMultiplierCodeCave2); // Second - Items unless boots and kizuna items (return to 220CAA)
                InjectAtAddress(0x220FE5, 5, ref _storeItemMultiplierCodeCave3); // Third - Boots and kizuna items (return to 220FEA)

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
                byte[] originalBytes1 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+221CE5: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes2 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+220CA5: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes3 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+220FE5: mov [rsi+10],ecx; mov eax,ebx

                bool success1 = WriteBytes(0x221CE5, originalBytes1);
                bool success2 = WriteBytes(0x220CA5, originalBytes2);
                bool success3 = WriteBytes(0x220FE5, originalBytes3);

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

        private IntPtr AOBScan(byte[] pattern, byte[]? mask = null)
        {
            if (_targetProcess == null || _moduleBase == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                var module = _targetProcess.MainModule;
                if (module == null)
                    return IntPtr.Zero;

                long moduleSize = module.ModuleMemorySize;
                byte[] buffer = new byte[moduleSize];

                if (!ReadProcessMemory(_processHandle, _moduleBase, buffer, (int)moduleSize, out _))
                    return IntPtr.Zero;

                // If no mask provided, create a mask of all 1s (match all bytes)
                if (mask == null)
                {
                    mask = new byte[pattern.Length];
                    for (int i = 0; i < mask.Length; i++)
                        mask[i] = 1;
                }

                for (long i = 0; i < moduleSize - pattern.Length; i++)
                {
                    bool found = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        // Skip comparison if mask is 0 (wildcard)
                        if (mask[j] != 0 && buffer[i + j] != pattern[j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        return new IntPtr(_moduleBase.ToInt64() + i);
                    }
                }

                return IntPtr.Zero;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        public bool InjectSpiritIncrement()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Heroes Spirits: AOB scan for "66 89 68 0C 48"
                byte[] heroesAOB = new byte[] { 0x66, 0x89, 0x68, 0x0C, 0x48 };
                IntPtr heroesAddress = AOBScan(heroesAOB, null);

                if (heroesAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Heroes Spirits AOB pattern");
                }

                // Elite Spirits: DISABLED - needs more work
                // byte[] eliteAOB = new byte[] { 0x66, 0x41, 0x89, 0x6C, 0x78, 0x10 };
                // IntPtr eliteAddress = AOBScan(eliteAOB, null);

                // if (eliteAddress == IntPtr.Zero)
                // {
                //     throw new Exception("Failed to find Elite Spirits AOB pattern");
                // }

                // Inject Heroes Spirits (5 bytes to replace for jmp instruction)
                InjectSpiritAtAddress(heroesAddress, 5, true, ref _heroSpiritIncrementCodeCave);

                // Inject Elite Spirits (DISABLED)
                // InjectSpiritAtAddress(eliteAddress, 6, false, ref _eliteSpiritIncrementCodeCave);

                return true;
            }
            catch (Exception ex)
            {
                RemoveSpiritIncrement();
                throw new Exception($"Spirit increment injection failed: {ex.Message}", ex);
            }
        }

        private bool InjectSpiritAtAddress(IntPtr address, int bytesToReplace, bool isHeroSpirit, ref IntPtr codeCave)
        {
            try
            {
                // Try multiple allocation attempts at different offsets to find memory within ±2GB range
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    codeCave = VirtualAllocEx(_processHandle, preferredAddress, 2048, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (codeCave != IntPtr.Zero)
                    {
                        long distance = codeCave.ToInt64() - address.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                            codeCave = IntPtr.Zero;
                        }
                    }
                }

                if (codeCave == IntPtr.Zero)
                {
                    codeCave = VirtualAllocEx(_processHandle, IntPtr.Zero, 2048, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (codeCave != IntPtr.Zero)
                    {
                        long distance = codeCave.ToInt64() - address.ToInt64();
                        if (Math.Abs(distance) > 0x7FFFFFFF)
                        {
                            VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                            codeCave = IntPtr.Zero;
                        }
                    }
                }

                if (codeCave == IntPtr.Zero)
                {
                    throw new Exception($"Failed to allocate memory within ±2GB range");
                }

                // Build injected code
                byte[] injectedCode;
                int jmpOffsetPos;

                if (isHeroSpirit)
                {
                    // Heroes: add bp, 2; mov [rax+0C],bp; jmp to original destination
                    injectedCode = new byte[13];
                    injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC5; injectedCode[3] = 0x02; // add bp, 2
                    injectedCode[4] = 0x66; injectedCode[5] = 0x89; injectedCode[6] = 0x68; injectedCode[7] = 0x0C; // mov [rax+0C],bp
                    injectedCode[8] = 0xE9; // jmp (offset will be calculated)
                    jmpOffsetPos = 9;
                }
                else
                {
                    // Elite: add bp, 2; mov [r8+rdi*2+10],bp; jmp back
                    injectedCode = new byte[15];
                    injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC5; injectedCode[3] = 0x02; // add bp, 2
                    injectedCode[4] = 0x66; injectedCode[5] = 0x41; injectedCode[6] = 0x89; injectedCode[7] = 0x6C;
                    injectedCode[8] = 0x78; injectedCode[9] = 0x10; // mov [r8+rdi*2+10],bp
                    injectedCode[10] = 0xE9; // jmp (offset will be calculated)
                    jmpOffsetPos = 11;
                }

                // Calculate jump back to continue execution after the replaced bytes
                IntPtr originalDestination = new IntPtr(address.ToInt64() + bytesToReplace);
                long jmpOffset = originalDestination.ToInt64() - (codeCave.ToInt64() + injectedCode.Length);

                byte[] offsetBytes = BitConverter.GetBytes((int)jmpOffset);
                Array.Copy(offsetBytes, 0, injectedCode, jmpOffsetPos, 4);

                // Write injected code to code cave
                if (!WriteProcessMemory(_processHandle, codeCave, injectedCode, injectedCode.Length, out _))
                {
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                    throw new Exception($"Failed to write injected code");
                }

                // Create hook: replace original bytes with jump to code cave
                long jmpToCodeCave = codeCave.ToInt64() - (address.ToInt64() + 5);
                byte[] hookBytes = new byte[bytesToReplace];
                hookBytes[0] = 0xE9; // jmp opcode
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);

                // Fill remaining bytes with NOPs if needed
                for (int i = 5; i < bytesToReplace; i++)
                {
                    hookBytes[i] = 0x90; // NOP
                }

                // Change memory protection and write hook
                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, address, (uint)bytesToReplace, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, address, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, address, (uint)bytesToReplace, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, codeCave, 0, MEM_RELEASE);
                    codeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
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

        public bool RemoveSpiritIncrement()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success1 = true;
                bool success2 = true;

                // Restore Heroes Spirits original bytes
                if (_heroSpiritIncrementCodeCave != IntPtr.Zero)
                {
                    // Use the known offset since it's already hooked
                    IntPtr heroesAddress = new IntPtr(_moduleBase.ToInt64() + 0xCF1F3A);

                    // Restore 5 bytes: the original 4-byte mov instruction + the next byte
                    byte[] heroesOriginal = new byte[] { 0x66, 0x89, 0x68, 0x0C, 0x48 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, heroesAddress, (uint)heroesOriginal.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success1 = WriteProcessMemory(_processHandle, heroesAddress, heroesOriginal, heroesOriginal.Length, out _);
                    VirtualProtectEx(_processHandle, heroesAddress, (uint)heroesOriginal.Length, oldProtect, out _);

                    VirtualFreeEx(_processHandle, _heroSpiritIncrementCodeCave, 0, MEM_RELEASE);
                    _heroSpiritIncrementCodeCave = IntPtr.Zero;
                }

                // Restore Elite Spirits original bytes
                if (_eliteSpiritIncrementCodeCave != IntPtr.Zero)
                {
                    // Use the known offset since it's already hooked
                    IntPtr eliteAddress = new IntPtr(_moduleBase.ToInt64() + 0xCF1E37);

                    // Restore 6 bytes: the original mov instruction
                    byte[] eliteOriginal = new byte[] { 0x66, 0x41, 0x89, 0x6C, 0x78, 0x10 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, eliteAddress, (uint)eliteOriginal.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success2 = WriteProcessMemory(_processHandle, eliteAddress, eliteOriginal, eliteOriginal.Length, out _);
                    VirtualProtectEx(_processHandle, eliteAddress, (uint)eliteOriginal.Length, oldProtect, out _);

                    VirtualFreeEx(_processHandle, _eliteSpiritIncrementCodeCave, 0, MEM_RELEASE);
                    _eliteSpiritIncrementCodeCave = IntPtr.Zero;
                }

                return success1 && success2;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool InjectPassiveValueEditing()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for "48 8B 0F 0F 57 C9 F3 * * C8 E8 * * * * EB"
                // Pattern from Cheat Engine script with wildcards
                byte[] aobPattern = new byte[] {
                    0x48, 0x8B, 0x0F, 0x0F, 0x57, 0xC9, 0xF3, 0x00, 0x00, 0xC8,
                    0xE8, 0x00, 0x00, 0x00, 0x00, 0xEB
                };
                byte[] aobMask = new byte[] {
                    1, 1, 1, 1, 1, 1, 1, 0, 0, 1,  // Match first 7 bytes, wildcards for 2, match 0xC8
                    1, 0, 0, 0, 0, 1                // Match 0xE8, wildcards for 4, match 0xEB
                };
                _passiveValueHookAddress = AOBScan(aobPattern, aobMask);

                if (_passiveValueHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Passive Value AOB pattern.\n\n" +
                        "Make sure:\n" +
                        "1. The game is running\n" +
                        "2. You are in the Abilearn Board screen\n" +
                        "3. The game version is correct");
                }

                // Allocate memory for code cave (need space for code + passiveValAdr + passiveValType)
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _passiveValueCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_passiveValueCodeCave != IntPtr.Zero)
                    {
                        long distance = _passiveValueCodeCave.ToInt64() - _passiveValueHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                            _passiveValueCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_passiveValueCodeCave == IntPtr.Zero)
                {
                    _passiveValueCodeCave = VirtualAllocEx(_processHandle, IntPtr.Zero, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_passiveValueCodeCave != IntPtr.Zero)
                    {
                        long distance = _passiveValueCodeCave.ToInt64() - _passiveValueHookAddress.ToInt64();
                        if (Math.Abs(distance) > 0x7FFFFFFF)
                        {
                            VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                            _passiveValueCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_passiveValueCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory within ±2GB range");
                }

                // Set up addresses for passiveValAdr and passiveValType (at the end of the code cave)
                _passiveValAdrAddress = new IntPtr(_passiveValueCodeCave.ToInt64() + 100); // Store at offset 100
                _passiveValTypeAddress = new IntPtr(_passiveValueCodeCave.ToInt64() + 108); // Store at offset 108

                // Build injected code
                // mov [passiveValAdr],rcx
                // mov [passiveValType],al
                // mov rcx,[rdi]
                // xorps xmm1,xmm1
                // jmp return

                List<byte> injectedCode = new List<byte>();

                // mov [passiveValAdr],rcx  (48 89 0D + 4-byte RIP-relative offset)
                injectedCode.Add(0x48); // REX.W prefix
                injectedCode.Add(0x89); // mov [rel], rcx
                injectedCode.Add(0x0D);
                long adrOffset = _passiveValAdrAddress.ToInt64() - (_passiveValueCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)adrOffset));

                // mov [passiveValType],al  (88 05 + 4-byte RIP-relative offset)
                injectedCode.Add(0x88); // mov [rel], al
                injectedCode.Add(0x05);
                long typeOffset = _passiveValTypeAddress.ToInt64() - (_passiveValueCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)typeOffset));

                // Original code: mov rcx,[rdi]  (48 8B 0F)
                injectedCode.Add(0x48);
                injectedCode.Add(0x8B);
                injectedCode.Add(0x0F);

                // Original code: xorps xmm1,xmm1  (0F 57 C9)
                injectedCode.Add(0x0F);
                injectedCode.Add(0x57);
                injectedCode.Add(0xC9);

                // jmp to return address (E9 + 4-byte offset)
                injectedCode.Add(0xE9);
                IntPtr returnAddress = new IntPtr(_passiveValueHookAddress.ToInt64() + 6); // 6 bytes for the original code
                long jmpOffset = returnAddress.ToInt64() - (_passiveValueCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpOffset));

                // Write injected code
                if (!WriteProcessMemory(_processHandle, _passiveValueCodeCave, injectedCode.ToArray(), injectedCode.Count, out _))
                {
                    VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                    _passiveValueCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write injected code");
                }

                // Initialize passiveValAdr and passiveValType to 0
                byte[] zeroBuffer = new byte[8];
                WriteProcessMemory(_processHandle, _passiveValAdrAddress, zeroBuffer, 8, out _);
                WriteProcessMemory(_processHandle, _passiveValTypeAddress, new byte[4], 4, out _);

                // Create hook: replace original 6 bytes with jump to code cave
                long jmpToCodeCave = _passiveValueCodeCave.ToInt64() - (_passiveValueHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[6];
                hookBytes[0] = 0xE9; // jmp opcode
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);
                hookBytes[5] = 0x90; // NOP to fill the 6th byte

                // Change memory protection and write hook
                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _passiveValueHookAddress, 6, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                    _passiveValueCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _passiveValueHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _passiveValueHookAddress, 6, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                    _passiveValueCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                return true;
            }
            catch (Exception)
            {
                if (_passiveValueCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                    _passiveValueCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool RemovePassiveValueEditing()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success = true;

                if (_passiveValueHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes: 48 8B 0F 0F 57 C9
                    byte[] originalBytes = new byte[] { 0x48, 0x8B, 0x0F, 0x0F, 0x57, 0xC9 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _passiveValueHookAddress, (uint)originalBytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _passiveValueHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _passiveValueHookAddress, (uint)originalBytes.Length, oldProtect, out _);

                    _passiveValueHookAddress = IntPtr.Zero;
                }

                if (_passiveValueCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _passiveValueCodeCave, 0, MEM_RELEASE);
                    _passiveValueCodeCave = IntPtr.Zero;
                }

                _passiveValAdrAddress = IntPtr.Zero;
                _passiveValTypeAddress = IntPtr.Zero;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public (bool hasValue, int valueType, double currentValue) ReadPassiveValue()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || _passiveValAdrAddress == IntPtr.Zero)
            {
                return (false, 0, 0);
            }

            try
            {
                // Read passiveValAdr
                byte[] adrBuffer = new byte[8];
                if (!ReadProcessMemory(_processHandle, _passiveValAdrAddress, adrBuffer, 8, out _))
                {
                    return (false, 0, 0);
                }

                long passiveValAdr = BitConverter.ToInt64(adrBuffer, 0);
                if (passiveValAdr == 0)
                {
                    return (false, 0, 0);
                }

                // Read passiveValType
                byte[] typeBuffer = new byte[4];
                if (!ReadProcessMemory(_processHandle, _passiveValTypeAddress, typeBuffer, 4, out _))
                {
                    return (false, 0, 0);
                }

                int passiveValType = BitConverter.ToInt32(typeBuffer, 0);

                // Read the actual value at passiveValAdr
                IntPtr valueAddress = new IntPtr(passiveValAdr);
                byte[] valueBuffer = new byte[4];
                if (!ReadProcessMemory(_processHandle, valueAddress, valueBuffer, 4, out _))
                {
                    return (false, 0, 0);
                }

                double currentValue;
                if (passiveValType == 3)
                {
                    // Float value
                    currentValue = BitConverter.ToSingle(valueBuffer, 0);
                }
                else
                {
                    // DWord value
                    currentValue = BitConverter.ToInt32(valueBuffer, 0);
                }

                return (true, passiveValType, currentValue);
            }
            catch (Exception)
            {
                return (false, 0, 0);
            }
        }

        public bool WritePassiveValue(string valueString)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || _passiveValAdrAddress == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Read passiveValAdr
                byte[] adrBuffer = new byte[8];
                if (!ReadProcessMemory(_processHandle, _passiveValAdrAddress, adrBuffer, 8, out _))
                {
                    return false;
                }

                long passiveValAdr = BitConverter.ToInt64(adrBuffer, 0);
                if (passiveValAdr == 0)
                {
                    return false;
                }

                // Read passiveValType
                byte[] typeBuffer = new byte[4];
                if (!ReadProcessMemory(_processHandle, _passiveValTypeAddress, typeBuffer, 4, out _))
                {
                    return false;
                }

                int passiveValType = BitConverter.ToInt32(typeBuffer, 0);

                // Parse and write the value
                IntPtr valueAddress = new IntPtr(passiveValAdr);
                byte[] valueBuffer;

                if (passiveValType == 3)
                {
                    // Float value
                    if (!float.TryParse(valueString, out float floatValue))
                    {
                        return false;
                    }
                    valueBuffer = BitConverter.GetBytes(floatValue);
                }
                else
                {
                    // DWord value
                    if (!int.TryParse(valueString, out int intValue))
                    {
                        return false;
                    }
                    valueBuffer = BitConverter.GetBytes(intValue);
                }

                return WriteProcessMemory(_processHandle, valueAddress, valueBuffer, valueBuffer.Length, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool EnableSpiritCardInjection()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            if (_isSpiritCardInjectionEnabled)
            {
                return true; // Already enabled
            }

            try
            {
                // AOB scan for the pattern from CE script: 49 8B 04 24 49 8B CC FF 50 30 * * * 44 8B
                // Pattern matches: mov rax,[r12]; mov rcx,r12; call qword ptr [rax+30]; ...; mov r13d,eax
                byte[] aobPattern = new byte[] { 0x49, 0x8B, 0x04, 0x24, 0x49, 0x8B, 0xCC, 0xFF, 0x50, 0x30, 0x00, 0x00, 0x00, 0x44, 0x8B };
                byte[] aobMask = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1 };

                _spiritCardHookAddress = AOBScan(aobPattern, aobMask);

                if (_spiritCardHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find spirit card injection point.\n\nMake sure:\n1. The game is running\n2. You have opened Team Dock - Spirits at least once\n3. The game version is correct");
                }

                // Allocate code cave ($1000 = 4096 bytes)
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _spiritCardInjectionCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_spiritCardInjectionCodeCave != IntPtr.Zero)
                    {
                        long distance = _spiritCardInjectionCodeCave.ToInt64() - _spiritCardHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                            _spiritCardInjectionCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_spiritCardInjectionCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory for spirit card injection");
                }

                // Set up memory layout in code cave
                // Structure:
                // +0x000: Injection code
                // +0x200: cfHerospiritAddType (4 bytes)
                // +0x204: cfHerospiritID (4 bytes)
                // +0x208: addherospiritdata (16 bytes: spiritID, 0, 1, 0)
                // +0x218: addherospiritTemp (4 bytes)

                _cfHerospiritAddTypeAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x200);
                _cfHerospiritIDAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x204);
                IntPtr addherospiritdataAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x208);
                IntPtr addherospiritTempAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x218);

                // Initialize memory
                byte[] initBytes = BitConverter.GetBytes(1); // Add-One mode
                WriteProcessMemory(_processHandle, _cfHerospiritAddTypeAddress, initBytes, 4, out _);
                initBytes = BitConverter.GetBytes(0); // Initial spirit ID
                WriteProcessMemory(_processHandle, _cfHerospiritIDAddress, initBytes, 4, out _);

                // Write the full injection code based on Cheat Engine script
                List<byte> injectionCode = new List<byte>();

                // cmp dword ptr [r12+8],1
                injectionCode.AddRange(new byte[] { 0x41, 0x83, 0x7C, 0x24, 0x08, 0x01 });

                // jne code (will patch offset later)
                int jneCodeOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x85, 0x00, 0x00, 0x00, 0x00 }); // jne (6 bytes)

                // cmp dword ptr [cfHerospiritAddType],0
                int cfTypeOffset = (int)(_cfHerospiritAddTypeAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x83, 0x3D }); // cmp dword ptr [rel]
                injectionCode.AddRange(BitConverter.GetBytes(cfTypeOffset));
                injectionCode.Add(0x00); // compare with 0

                // je codeA (skip to Add-All)
                int jeCodeAOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 }); // je (6 bytes)

                // Add-One:
                int addOneStart = injectionCode.Count;
                // mov eax,[cfHerospiritID]
                int cfIDOffset = (int)(_cfHerospiritIDAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 6));
                injectionCode.AddRange(new byte[] { 0x8B, 0x05 }); // mov eax,[rel]
                injectionCode.AddRange(BitConverter.GetBytes(cfIDOffset));

                // mov [addherospiritdata],eax
                int dataOffset1 = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 6));
                injectionCode.AddRange(new byte[] { 0x89, 0x05 }); // mov [rel],eax
                injectionCode.AddRange(BitConverter.GetBytes(dataOffset1));

                // call fncAddHerospirit (will add later)
                int callFncOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00 }); // call

                // jmp code
                int jmpFromAddOne = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0xE9, 0x00, 0x00, 0x00, 0x00 }); // jmp

                // codeA (Add-All)
                int codeAStart = injectionCode.Count;

                // lea rcx,[herospiritIDAll]
                IntPtr herospiritIDAllAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x300);
                int herospiritIDAllOffset = (int)(herospiritIDAllAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x48, 0x8D, 0x0D }); // lea rcx,[rel]
                injectionCode.AddRange(BitConverter.GetBytes(herospiritIDAllOffset));

                // codeR (loop):
                int codeRStart = injectionCode.Count;

                // cmp dword ptr [rcx],0
                injectionCode.AddRange(new byte[] { 0x83, 0x39, 0x00 });

                // je code (exit loop)
                int jeCodeFromLoop = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 }); // je (6 bytes)

                // mov eax,[rcx]
                injectionCode.AddRange(new byte[] { 0x8B, 0x01 });

                // mov [addherospiritdata],eax
                int dataOffset2 = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 6));
                injectionCode.AddRange(new byte[] { 0x89, 0x05 }); // mov [rel],eax
                injectionCode.AddRange(BitConverter.GetBytes(dataOffset2));

                // call fncAddHerospirit
                int callFncFromLoop = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00 }); // call (will patch later)

                // add rcx,4
                injectionCode.AddRange(new byte[] { 0x48, 0x83, 0xC1, 0x04 });

                // jmp codeR (loop back)
                int jmpBackToLoop = injectionCode.Count;
                int loopJmpOffset = codeRStart - (jmpBackToLoop + 5);
                injectionCode.AddRange(new byte[] { 0xE9 }); // jmp
                injectionCode.AddRange(BitConverter.GetBytes(loopJmpOffset));

                // code: (original instructions)
                int codeLabel = injectionCode.Count;

                // Patch all jump offsets - need to convert to array first to modify
                byte[] codeArray = injectionCode.ToArray();

                // Patch jne code offset
                int jneTarget = codeLabel - (jneCodeOffset + 6);
                byte[] jneBytes = BitConverter.GetBytes(jneTarget);
                Array.Copy(jneBytes, 0, codeArray, jneCodeOffset + 2, 4);

                // Patch je codeA offset
                int jeTarget = codeAStart - (jeCodeAOffset + 6);
                byte[] jeBytes = BitConverter.GetBytes(jeTarget);
                Array.Copy(jeBytes, 0, codeArray, jeCodeAOffset + 2, 4);

                // Patch jmp from Add-One
                int jmpTarget = codeLabel - (jmpFromAddOne + 5);
                byte[] jmpBytes = BitConverter.GetBytes(jmpTarget);
                Array.Copy(jmpBytes, 0, codeArray, jmpFromAddOne + 1, 4);

                // Patch je from loop (exit loop to code)
                int jeLoopTarget = codeLabel - (jeCodeFromLoop + 6);
                byte[] jeLoopBytes = BitConverter.GetBytes(jeLoopTarget);
                Array.Copy(jeLoopBytes, 0, codeArray, jeCodeFromLoop + 2, 4);

                // Rebuild injectionCode from modified array
                injectionCode.Clear();
                injectionCode.AddRange(codeArray);

                // Original code
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0x04, 0x24 }); // mov rax,[r12]
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0xCC }); // mov rcx,r12

                // Jump back to original code
                injectionCode.Add(0xE9); // jmp
                IntPtr returnAddress = new IntPtr(_spiritCardHookAddress.ToInt64() + 7);
                long jmpOffset = returnAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4);
                injectionCode.AddRange(BitConverter.GetBytes((int)jmpOffset));

                // fncAddHerospirit function
                int fncStart = injectionCode.Count;

                // Patch both calls to fncAddHerospirit
                codeArray = injectionCode.ToArray();

                // Patch call from Add-One
                int callTarget = fncStart - (callFncOffset + 5);
                byte[] callBytes = BitConverter.GetBytes(callTarget);
                Array.Copy(callBytes, 0, codeArray, callFncOffset + 1, 4);

                // Patch call from loop
                int callTargetLoop = fncStart - (callFncFromLoop + 5);
                byte[] callBytesLoop = BitConverter.GetBytes(callTargetLoop);
                Array.Copy(callBytesLoop, 0, codeArray, callFncFromLoop + 1, 4);

                injectionCode.Clear();
                injectionCode.AddRange(codeArray);

                // push rcx, rdx, r8, r9
                injectionCode.AddRange(new byte[] { 0x51 }); // push rcx
                injectionCode.AddRange(new byte[] { 0x52 }); // push rdx
                injectionCode.AddRange(new byte[] { 0x41, 0x50 }); // push r8
                injectionCode.AddRange(new byte[] { 0x41, 0x51 }); // push r9

                // lea r8,[addherospiritdata]
                int r8Offset = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x8D, 0x05 }); // lea r8,[rel]
                injectionCode.AddRange(BitConverter.GetBytes(r8Offset));

                // lea rdx,[addherospiritTemp]
                int rdxOffset = (int)(addherospiritTempAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x48, 0x8D, 0x15 }); // lea rdx,[rel]
                injectionCode.AddRange(BitConverter.GetBytes(rdxOffset));

                // mov r9d,1
                injectionCode.AddRange(new byte[] { 0x41, 0xB9, 0x01, 0x00, 0x00, 0x00 });

                // mov rcx,r12
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0xCC });

                // mov rax,[r12]
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0x04, 0x24 });

                // call qword ptr [rax+20]
                injectionCode.AddRange(new byte[] { 0xFF, 0x50, 0x20 });

                // pop r9, r8, rdx, rcx
                injectionCode.AddRange(new byte[] { 0x41, 0x59 }); // pop r9
                injectionCode.AddRange(new byte[] { 0x41, 0x58 }); // pop r8
                injectionCode.AddRange(new byte[] { 0x5A }); // pop rdx
                injectionCode.AddRange(new byte[] { 0x59 }); // pop rcx

                // ret
                injectionCode.AddRange(new byte[] { 0xC3 });

                // Initialize addherospiritdata structure (spiritID, 0, quantity, 0)
                byte[] spiritDataInit = new byte[16];
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 0); // spiritID (will be set later)
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 4);
                BitConverter.GetBytes(1).CopyTo(spiritDataInit, 8); // Quantity = 1
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 12);
                WriteProcessMemory(_processHandle, addherospiritdataAddress, spiritDataInit, 16, out _);

                // Write injection code to code cave
                if (!WriteProcessMemory(_processHandle, _spiritCardInjectionCodeCave, injectionCode.ToArray(), injectionCode.Count, out _))
                {
                    VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                    _spiritCardInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write injection code");
                }

                // Create hook at the injection point
                long jmpToCodeCave = _spiritCardInjectionCodeCave.ToInt64() - (_spiritCardHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[7];
                hookBytes[0] = 0xE9; // jmp
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);
                hookBytes[5] = 0x90; // nop
                hookBytes[6] = 0x90; // nop

                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _spiritCardHookAddress, 7, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                    _spiritCardInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _spiritCardHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _spiritCardHookAddress, 7, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                    _spiritCardInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                _isSpiritCardInjectionEnabled = true;
                return true;
            }
            catch (Exception)
            {
                if (_spiritCardInjectionCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                    _spiritCardInjectionCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool DisableSpiritCardInjection()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            if (!_isSpiritCardInjectionEnabled)
            {
                return true; // Already disabled
            }

            try
            {
                bool success = true;

                if (_spiritCardHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes: 49 8B 04 24 49 8B CC
                    byte[] originalBytes = new byte[] { 0x49, 0x8B, 0x04, 0x24, 0x49, 0x8B, 0xCC };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _spiritCardHookAddress, (uint)originalBytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _spiritCardHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _spiritCardHookAddress, (uint)originalBytes.Length, oldProtect, out _);

                    _spiritCardHookAddress = IntPtr.Zero;
                }

                if (_spiritCardInjectionCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _spiritCardInjectionCodeCave, 0, MEM_RELEASE);
                    _spiritCardInjectionCodeCave = IntPtr.Zero;
                }

                _cfHerospiritAddTypeAddress = IntPtr.Zero;
                _cfHerospiritIDAddress = IntPtr.Zero;
                _isSpiritCardInjectionEnabled = false;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetSpiritCardToAdd(uint spiritId, int quantity)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || !_isSpiritCardInjectionEnabled)
            {
                return false;
            }

            try
            {
                // Set to Add-One mode
                byte[] addTypeBytes = BitConverter.GetBytes(1); // 1 = Add-One mode
                WriteProcessMemory(_processHandle, _cfHerospiritAddTypeAddress, addTypeBytes, 4, out _);

                // Write the spirit ID to cfHerospiritID
                byte[] spiritIdBytes = BitConverter.GetBytes(spiritId);
                bool success = WriteProcessMemory(_processHandle, _cfHerospiritIDAddress, spiritIdBytes, 4, out _);

                if (!success)
                {
                    return false;
                }

                // Note: The actual addition happens when you interact with the Team Dock - Spirits screen
                // The quantity (50) is already set in the addherospiritdata structure
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetAllSpiritCardsToAdd(List<uint> spiritIds)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || !_isSpiritCardInjectionEnabled)
            {
                return false;
            }

            try
            {
                // Set to Add-All mode
                byte[] addTypeBytes = BitConverter.GetBytes(0); // 0 = Add-All mode
                WriteProcessMemory(_processHandle, _cfHerospiritAddTypeAddress, addTypeBytes, 4, out _);

                // Write all spirit IDs to herospiritIDAll array (after the data structures)
                // herospiritIDAll starts at code cave + 0x300
                IntPtr herospiritIDAllAddress = new IntPtr(_spiritCardInjectionCodeCave.ToInt64() + 0x300);

                List<byte> allSpiritIds = new List<byte>();
                foreach (uint spiritId in spiritIds)
                {
                    allSpiritIds.AddRange(BitConverter.GetBytes(spiritId));
                }
                // Add terminating 0
                allSpiritIds.AddRange(BitConverter.GetBytes(0));

                bool success = WriteProcessMemory(_processHandle, herospiritIDAllAddress, allSpiritIds.ToArray(), allSpiritIds.Count, out _);

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool AddSpiritCardToTeam(uint spiritId, int quantity = 50)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Enable injection if not already enabled
                if (!_isSpiritCardInjectionEnabled)
                {
                    bool enableSuccess = EnableSpiritCardInjection();
                    if (!enableSuccess)
                    {
                        return false;
                    }
                }

                // Set the spirit ID to add
                return SetSpiritCardToAdd(spiritId, quantity);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool InjectUnlimitedHeroes()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for "75 09 8B 40 10"
                byte[] aobPattern = new byte[] { 0x75, 0x09, 0x8B, 0x40, 0x10 };
                byte[] aobMask = new byte[] { 1, 1, 1, 1, 1 }; // All bytes must match exactly
                _unlimitedHeroesHookAddress = AOBScan(aobPattern, aobMask);

                if (_unlimitedHeroesHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Unlimited Heroes AOB pattern.\n\nMake sure the game is running.");
                }

                // Allocate code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _unlimitedHeroesCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_unlimitedHeroesCodeCave != IntPtr.Zero)
                    {
                        long distance = _unlimitedHeroesCodeCave.ToInt64() - _unlimitedHeroesHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                            _unlimitedHeroesCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_unlimitedHeroesCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory for Unlimited Heroes");
                }

                // Build the injection code
                List<byte> injectedCode = new List<byte>();

                // jne return+6 (offset will be calculated)
                int jneOffset = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x75, 0x00 }); // jne (offset to be patched)

                // mov eax,[rax+10]
                injectedCode.AddRange(new byte[] { 0x8B, 0x40, 0x10 });

                // cmp eax,5
                injectedCode.AddRange(new byte[] { 0x83, 0xF8, 0x05 });

                // jl code (short jump)
                int jlOffset = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x7C, 0x00 }); // jl (offset to be patched)

                // mov eax,4
                injectedCode.AddRange(new byte[] { 0xB8, 0x04, 0x00, 0x00, 0x00 });

                // code: (label for jl target)
                int codeLabel = injectedCode.Count;
                // Patch jl offset
                byte jlDist = (byte)(codeLabel - (jlOffset + 2));
                injectedCode[jlOffset + 1] = jlDist;

                // jmp return
                injectedCode.Add(0xE9);
                IntPtr returnAddress = new IntPtr(_unlimitedHeroesHookAddress.ToInt64() + 5); // After the 5-byte hook
                long jmpReturnDist = returnAddress.ToInt64() - (_unlimitedHeroesCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpReturnDist));

                // Calculate jne return+6 offset (jne should jump to return+6 which skips "mov eax,[rax+10]")
                int returnPlus6 = injectedCode.Count;
                IntPtr returnPlus6Address = new IntPtr(_unlimitedHeroesHookAddress.ToInt64() + 5 + 1); // +5 for hook, +1 for the extra byte
                injectedCode.Add(0xE9); // jmp to return+6
                long jmpReturn6Dist = returnPlus6Address.ToInt64() - (_unlimitedHeroesCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpReturn6Dist));

                // Patch jne offset at the beginning
                byte jneDist = (byte)(returnPlus6 - (jneOffset + 2));
                byte[] codeArray = injectedCode.ToArray();
                codeArray[jneOffset + 1] = jneDist;

                // Write injected code
                if (!WriteProcessMemory(_processHandle, _unlimitedHeroesCodeCave, codeArray, codeArray.Length, out _))
                {
                    VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                    _unlimitedHeroesCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write Unlimited Heroes injection code");
                }

                // Create hook
                long jmpToCodeCave = _unlimitedHeroesCodeCave.ToInt64() - (_unlimitedHeroesHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[5];
                hookBytes[0] = 0xE9; // jmp
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);

                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _unlimitedHeroesHookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                    _unlimitedHeroesCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _unlimitedHeroesHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _unlimitedHeroesHookAddress, 5, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                    _unlimitedHeroesCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                return true;
            }
            catch (Exception)
            {
                if (_unlimitedHeroesCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                    _unlimitedHeroesCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool RemoveUnlimitedHeroes()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success = true;

                if (_unlimitedHeroesHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes: 75 09 8B 40 10
                    byte[] originalBytes = new byte[] { 0x75, 0x09, 0x8B, 0x40, 0x10 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _unlimitedHeroesHookAddress, (uint)originalBytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _unlimitedHeroesHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _unlimitedHeroesHookAddress, (uint)originalBytes.Length, oldProtect, out _);

                    _unlimitedHeroesHookAddress = IntPtr.Zero;
                }

                if (_unlimitedHeroesCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _unlimitedHeroesCodeCave, 0, MEM_RELEASE);
                    _unlimitedHeroesCodeCave = IntPtr.Zero;
                }

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool InjectFreeBuySpiritMarket()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for first injection point: "48 8B 0F 0F B7 D6 E8 ?? ?? ?? ?? 48"
                byte[] aobPattern1 = new byte[] { 0x48, 0x8B, 0x0F, 0x0F, 0xB7, 0xD6, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x48 };
                byte[] aobMask1 = new byte[] { 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1 };
                _freeBuySpiritMarketHookAddress = AOBScan(aobPattern1, aobMask1);

                if (_freeBuySpiritMarketHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Free Buy Spirit Market AOB pattern (first injection).\n\nMake sure the game is running.");
                }

                // AOB scan for second injection point: "0F B7 D6 EB ?? 48 ?? ?? E8"
                byte[] aobPattern2 = new byte[] { 0x0F, 0xB7, 0xD6, 0xEB, 0x00, 0x48, 0x00, 0x00, 0xE8 };
                byte[] aobMask2 = new byte[] { 1, 1, 1, 1, 0, 1, 0, 0, 1 };
                _freeBuySpiritMarketLvHookAddress = AOBScan(aobPattern2, aobMask2);

                if (_freeBuySpiritMarketLvHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Free Buy Spirit Market AOB pattern (second injection).\n\nMake sure the game is running.");
                }

                // Allocate code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _freeBuySpiritMarketCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_freeBuySpiritMarketCodeCave != IntPtr.Zero)
                    {
                        long distance = _freeBuySpiritMarketCodeCave.ToInt64() - _freeBuySpiritMarketHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                            _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_freeBuySpiritMarketCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory for Free Buy Spirit Market");
                }

                // Build the injection code first to know its size
                List<byte> injectedCode = new List<byte>();

                // cmp r14d,4
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xFE, 0x04 });
                // je codeA
                int jeCodeA1 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 }); // offset to be patched

                // cmp r15d,6
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xFF, 0x06 });
                // je codeA
                int jeCodeA2 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 });

                // cmp r15d,8
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xFF, 0x08 });
                // je codeA
                int jeCodeA3 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 });

                // jmp code
                int jmpCode = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0xEB, 0x00 });

                // codeA:
                int codeALabel = injectedCode.Count;
                // Patch je offsets
                byte jeA1Dist = (byte)(codeALabel - (jeCodeA1 + 2));
                injectedCode[jeCodeA1 + 1] = jeA1Dist;
                byte jeA2Dist = (byte)(codeALabel - (jeCodeA2 + 2));
                injectedCode[jeCodeA2 + 1] = jeA2Dist;
                byte jeA3Dist = (byte)(codeALabel - (jeCodeA3 + 2));
                injectedCode[jeCodeA3 + 1] = jeA3Dist;

                // mov si,[cfFBspiritmarketSpQuan]
                // We'll calculate the offset later after we know the code size
                injectedCode.AddRange(new byte[] { 0x66, 0x8B, 0x35 });
                int movSiOffsetPosition = injectedCode.Count; // Remember where to patch the offset
                injectedCode.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Placeholder for offset

                // code:
                int codeLabel = injectedCode.Count;
                // Patch jmp code offset
                byte jmpCodeDist = (byte)(codeLabel - (jmpCode + 2));
                byte[] codeArray = injectedCode.ToArray();
                codeArray[jmpCode + 1] = jmpCodeDist;
                injectedCode = new List<byte>(codeArray);

                // mov rcx,[rdi]
                injectedCode.AddRange(new byte[] { 0x48, 0x8B, 0x0F });
                // movzx edx,si
                injectedCode.AddRange(new byte[] { 0x0F, 0xB7, 0xD6 });

                // jmp return
                injectedCode.Add(0xE9);
                IntPtr returnAddress = new IntPtr(_freeBuySpiritMarketHookAddress.ToInt64() + 6);
                long jmpReturnDist = returnAddress.ToInt64() - (_freeBuySpiritMarketCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpReturnDist));

                // Set cfFBspiritmarketSpQuan address right after the code
                _cfFBspiritmarketSpQuanAddress = new IntPtr(_freeBuySpiritMarketCodeCave.ToInt64() + injectedCode.Count);

                // Now calculate and patch the mov si offset
                byte[] codeArray2 = injectedCode.ToArray();
                long movSiInstructionEnd = _freeBuySpiritMarketCodeCave.ToInt64() + movSiOffsetPosition + 4;
                int cfQuanOffset = (int)(_cfFBspiritmarketSpQuanAddress.ToInt64() - movSiInstructionEnd);
                byte[] offsetBytes = BitConverter.GetBytes(cfQuanOffset);
                Array.Copy(offsetBytes, 0, codeArray2, movSiOffsetPosition, 4);
                injectedCode = new List<byte>(codeArray2);

                // Add the quantity value (999) to the end of the code
                byte[] quantityBytes = BitConverter.GetBytes((short)999);
                injectedCode.AddRange(quantityBytes);

                // Write injected code + data
                if (!WriteProcessMemory(_processHandle, _freeBuySpiritMarketCodeCave, injectedCode.ToArray(), injectedCode.Count, out _))
                {
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write Free Buy Spirit Market injection code");
                }

                // Create first hook
                long jmpToCodeCave = _freeBuySpiritMarketCodeCave.ToInt64() - (_freeBuySpiritMarketHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[6];
                hookBytes[0] = 0xE9; // jmp
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);
                hookBytes[5] = 0x90; // nop

                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection (first hook)");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _freeBuySpiritMarketHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write first hook");
                }

                // Create second hook (level)
                byte[] levelHookBytes = new byte[] { 0xB2, 0x63, 0x90 }; // mov dl,99; nop

                if (!VirtualProtectEx(_processHandle, _freeBuySpiritMarketLvHookAddress, 3, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    // Clean up first hook
                    byte[] originalBytes1 = new byte[] { 0x48, 0x8B, 0x0F, 0x0F, 0xB7, 0xD6 };
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, PAGE_EXECUTE_READWRITE, out _);
                    WriteProcessMemory(_processHandle, _freeBuySpiritMarketHookAddress, originalBytes1, 6, out _);
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection (second hook)");
                }

                bool levelHookSuccess = WriteProcessMemory(_processHandle, _freeBuySpiritMarketLvHookAddress, levelHookBytes, levelHookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _freeBuySpiritMarketLvHookAddress, 3, oldProtect, out _);

                if (!levelHookSuccess)
                {
                    // Clean up
                    byte[] originalBytes1 = new byte[] { 0x48, 0x8B, 0x0F, 0x0F, 0xB7, 0xD6 };
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, PAGE_EXECUTE_READWRITE, out _);
                    WriteProcessMemory(_processHandle, _freeBuySpiritMarketHookAddress, originalBytes1, 6, out _);
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write second hook");
                }

                return true;
            }
            catch (Exception)
            {
                if (_freeBuySpiritMarketCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool RemoveFreeBuySpiritMarket()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success = true;

                if (_freeBuySpiritMarketHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes for first hook: 48 8B 0F 0F B7 D6
                    byte[] originalBytes1 = new byte[] { 0x48, 0x8B, 0x0F, 0x0F, 0xB7, 0xD6 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _freeBuySpiritMarketHookAddress, originalBytes1, originalBytes1.Length, out _);
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketHookAddress, 6, oldProtect, out _);

                    _freeBuySpiritMarketHookAddress = IntPtr.Zero;
                }

                if (_freeBuySpiritMarketLvHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes for second hook: 0F B7 D6
                    byte[] originalBytes2 = new byte[] { 0x0F, 0xB7, 0xD6 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketLvHookAddress, 3, PAGE_EXECUTE_READWRITE, out oldProtect);
                    WriteProcessMemory(_processHandle, _freeBuySpiritMarketLvHookAddress, originalBytes2, originalBytes2.Length, out _);
                    VirtualProtectEx(_processHandle, _freeBuySpiritMarketLvHookAddress, 3, oldProtect, out _);

                    _freeBuySpiritMarketLvHookAddress = IntPtr.Zero;
                }

                if (_freeBuySpiritMarketCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _freeBuySpiritMarketCodeCave, 0, MEM_RELEASE);
                    _freeBuySpiritMarketCodeCave = IntPtr.Zero;
                }

                _cfFBspiritmarketSpQuanAddress = IntPtr.Zero;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
