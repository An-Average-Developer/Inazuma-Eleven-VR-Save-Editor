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
        private IntPtr _storeItemMultiplierCodeCave1 = IntPtr.Zero; // For nie.exe+1FC045
        private IntPtr _storeItemMultiplierCodeCave2 = IntPtr.Zero; // For nie.exe+1FAF65
        private IntPtr _storeItemMultiplierCodeCave3 = IntPtr.Zero; // For nie.exe+1FB2C5
        private IntPtr _heroSpiritIncrementCodeCave = IntPtr.Zero; // For Heroes Spirits AOB injection
        private IntPtr _eliteSpiritIncrementCodeCave = IntPtr.Zero; // For Elite Spirits AOB injection
        private IntPtr _passiveValueCodeCave = IntPtr.Zero; // For Passive Value injection
        private IntPtr _passiveValAdrAddress = IntPtr.Zero; // Address where passiveValAdr is stored
        private IntPtr _passiveValTypeAddress = IntPtr.Zero; // Address where passiveValType is stored
        private IntPtr _passiveValueHookAddress = IntPtr.Zero; // Address of the hook
        private IntPtr _specialMoveTypeCodeCave = IntPtr.Zero; // For Special Move Type injection
        private IntPtr _specialMoveTypeAdrAddress = IntPtr.Zero; // Address where specialMoveTypeAdr is stored (9 qwords)
        private IntPtr _specialMoveTypeHookAddress = IntPtr.Zero; // Address of the hook
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
        private IntPtr _freeBuyShopCodeCave = IntPtr.Zero; // For Free Buy Shop injection
        private IntPtr _freeBuyShopHookAddress = IntPtr.Zero; // Hook address for Free Buy Shop
        private IntPtr _cfFreeBuyShopTokenQuanAddress = IntPtr.Zero; // Token quantity address
        private IntPtr _playerSpiritInjectionCodeCave = IntPtr.Zero; // For Player Spirit injection
        private IntPtr _playerSpiritHookAddress = IntPtr.Zero; // Address of the player spirit hook
        private IntPtr _cfPlayerspiritAddTypeAddress = IntPtr.Zero; // cfPlayerspiritAddType address
        private IntPtr _cfPlayerspiritIDAddress = IntPtr.Zero; // cfPlayerspiritID address
        private IntPtr _cfPlayerspiritRarityAddress = IntPtr.Zero; // cfPlayerspiritRarity address
        private bool _isPlayerSpiritInjectionEnabled = false;

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
                InjectAtAddress(0x1FC045, 7, ref _storeItemMultiplierCodeCave1); // First - Hissatsus and Kenshins (return to 221CEC)
                InjectAtAddress(0x1FAF65, 5, ref _storeItemMultiplierCodeCave2); // Second - Items unless boots and kizuna items (return to 220CAA)
                InjectAtAddress(0x1FB2C5, 5, ref _storeItemMultiplierCodeCave3); // Third - Boots and kizuna items (return to 1FB2C8)

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
                byte[] originalBytes1 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+1FC045: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes2 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+1FAF65: mov [rsi+10],ecx; mov eax,ebx
                byte[] originalBytes3 = new byte[] { 0x89, 0x4E, 0x10, 0x8B, 0xC3 }; // nie.exe+1FB2C5: mov [rsi+10],ecx; mov eax,ebx

                bool success1 = WriteBytes(0x1FC045, originalBytes1);
                bool success2 = WriteBytes(0x1FAF65, originalBytes2);
                bool success3 = WriteBytes(0x1FB2C5, originalBytes3);

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
                // Heroes Spirits: AOB scan for "66 89 70 10 4C" (mov [rax+10],si followed by mov r14)
                byte[] heroesAOB = new byte[] { 0x66, 0x89, 0x70, 0x10, 0x4C };
                IntPtr heroesAddress = AOBScan(heroesAOB, null);

                if (heroesAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Heroes Spirits AOB pattern");
                }

                // Inject Heroes Spirits (9 bytes to replace: 4 for mov [rax+10],si + 5 for mov r14,[rsp+40])
                InjectHeroSpiritAtAddress(heroesAddress, 9, ref _heroSpiritIncrementCodeCave);

                return true;
            }
            catch (Exception ex)
            {
                RemoveSpiritIncrement();
                throw new Exception($"Spirit increment injection failed: {ex.Message}", ex);
            }
        }

        public bool InjectEliteSpiritIncrement()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Elite Spirits: AOB scan for "66 41 89 74 42 14 E9" (mov [r10+rax*2+14],si followed by jmp)
                byte[] eliteAOB = new byte[] { 0x66, 0x41, 0x89, 0x74, 0x42, 0x14, 0xE9 };
                IntPtr eliteAddress = AOBScan(eliteAOB, null);

                if (eliteAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Elite Spirits AOB pattern");
                }

                // Inject Elite Spirits (6 bytes to replace for jmp instruction - only replaces the mov instruction)
                InjectEliteSpiritAtAddress(eliteAddress, 6, ref _eliteSpiritIncrementCodeCave);

                return true;
            }
            catch (Exception ex)
            {
                RemoveEliteSpiritIncrement();
                throw new Exception($"Elite Spirit increment injection failed: {ex.Message}", ex);
            }
        }

        private bool InjectHeroSpiritAtAddress(IntPtr address, int bytesToReplace, ref IntPtr codeCave)
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
                // Heroes: add si, 2; mov [rax+10],si; mov r14,[rsp+40]; jmp to original destination
                byte[] injectedCode = new byte[18];
                injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC6; injectedCode[3] = 0x02; // add si, 2
                injectedCode[4] = 0x66; injectedCode[5] = 0x89; injectedCode[6] = 0x70; injectedCode[7] = 0x10; // mov [rax+10],si
                injectedCode[8] = 0x4C; injectedCode[9] = 0x8B; injectedCode[10] = 0x74; injectedCode[11] = 0x24; injectedCode[12] = 0x40; // mov r14,[rsp+40]
                injectedCode[13] = 0xE9; // jmp (offset will be calculated)
                int jmpOffsetPos = 14;

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

        private bool InjectEliteSpiritAtAddress(IntPtr address, int bytesToReplace, ref IntPtr codeCave)
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
                // Elite: add si, 2; mov [r10+rax*2+14],si; jmp to original destination
                byte[] injectedCode = new byte[15];
                injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC6; injectedCode[3] = 0x02; // add si, 2
                injectedCode[4] = 0x66; injectedCode[5] = 0x41; injectedCode[6] = 0x89; injectedCode[7] = 0x74;
                injectedCode[8] = 0x42; injectedCode[9] = 0x14; // mov [r10+rax*2+14],si
                injectedCode[10] = 0xE9; // jmp (offset will be calculated)
                int jmpOffsetPos = 11;

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
                // Restore Heroes Spirits original bytes
                if (_heroSpiritIncrementCodeCave != IntPtr.Zero)
                {
                    // Use the known offset since it's already hooked
                    IntPtr heroesAddress = new IntPtr(_moduleBase.ToInt64() + 0xCD19DB);

                    // Restore 9 bytes: mov [rax+10],si (4 bytes) + mov r14,[rsp+40] (5 bytes)
                    byte[] heroesOriginal = new byte[] { 0x66, 0x89, 0x70, 0x10, 0x4C, 0x8B, 0x74, 0x24, 0x40 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, heroesAddress, (uint)heroesOriginal.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    bool success = WriteProcessMemory(_processHandle, heroesAddress, heroesOriginal, heroesOriginal.Length, out _);
                    VirtualProtectEx(_processHandle, heroesAddress, (uint)heroesOriginal.Length, oldProtect, out _);

                    VirtualFreeEx(_processHandle, _heroSpiritIncrementCodeCave, 0, MEM_RELEASE);
                    _heroSpiritIncrementCodeCave = IntPtr.Zero;

                    return success;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool RemoveEliteSpiritIncrement()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // Restore Elite Spirits original bytes
                if (_eliteSpiritIncrementCodeCave != IntPtr.Zero)
                {
                    // Use the known offset since it's already hooked
                    IntPtr eliteAddress = new IntPtr(_moduleBase.ToInt64() + 0xCD1917);

                    // Restore 6 bytes: the original mov instruction
                    byte[] eliteOriginal = new byte[] { 0x66, 0x41, 0x89, 0x74, 0x42, 0x14 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, eliteAddress, (uint)eliteOriginal.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    bool success = WriteProcessMemory(_processHandle, eliteAddress, eliteOriginal, eliteOriginal.Length, out _);
                    VirtualProtectEx(_processHandle, eliteAddress, (uint)eliteOriginal.Length, oldProtect, out _);

                    VirtualFreeEx(_processHandle, _eliteSpiritIncrementCodeCave, 0, MEM_RELEASE);
                    _eliteSpiritIncrementCodeCave = IntPtr.Zero;

                    return success;
                }

                return true;
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

        public bool InjectSpecialMoveTypeEditing()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for "74 3D 8B 58 04" (je +3D; mov ebx,[rax+04])
                byte[] aobPattern = new byte[] { 0x74, 0x3D, 0x8B, 0x58, 0x04 };
                byte[] aobMask = new byte[] { 1, 1, 1, 1, 1 }; // No wildcards
                _specialMoveTypeHookAddress = AOBScan(aobPattern, aobMask);

                if (_specialMoveTypeHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Special Move Type AOB pattern.\n\n" +
                        "Make sure:\n" +
                        "1. The game is running\n" +
                        "2. You are in the Abilearn Board screen\n" +
                        "3. The game version is correct");
                }

                // Allocate memory for code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _specialMoveTypeCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_specialMoveTypeCodeCave != IntPtr.Zero)
                    {
                        long distance = _specialMoveTypeCodeCave.ToInt64() - _specialMoveTypeHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                            _specialMoveTypeCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_specialMoveTypeCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory within ±2GB range");
                }

                // Memory layout:
                // +0x000: Injection code
                // +0x200: specialMoveTypeAdr (9 qwords = 72 bytes at offsets 0, 8, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40)
                _specialMoveTypeAdrAddress = new IntPtr(_specialMoveTypeCodeCave.ToInt64() + 0x200);

                // Initialize specialMoveTypeAdr to zeros (72 bytes for 9 qwords)
                byte[] zeroBuffer = new byte[72];
                WriteProcessMemory(_processHandle, _specialMoveTypeAdrAddress, zeroBuffer, 72, out _);

                // Build injected code matching CE script exactly
                List<byte> injectedCode = new List<byte>();
                IntPtr returnAddress = new IntPtr(_specialMoveTypeHookAddress.ToInt64() + 5);
                IntPtr originalJeTarget = new IntPtr(_specialMoveTypeHookAddress.ToInt64() + 5 + 0x3A); // return+3A

                // ===== newmem: =====
                // je return+3A (jump to original target if ZF is set)
                injectedCode.AddRange(new byte[] { 0x0F, 0x84 }); // je rel32
                int jeTargetPlaceholder = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Placeholder

                // cmp r8d,0
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x00 });

                // jne @f (skip the clearing block)
                int jneSkipClearOffset = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x75, 0x00 }); // jne rel8 (patch later)

                // xor rbx,rbx
                injectedCode.AddRange(new byte[] { 0x48, 0x31, 0xDB });

                // Clear all specialMoveTypeAdr offsets when r8d=0
                // mov [specialMoveTypeAdr+8],rbx
                int off8 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 8) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off8));

                // mov [specialMoveTypeAdr+10],rbx
                int off10 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x10) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off10));

                // mov [specialMoveTypeAdr+18],rbx
                int off18 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x18) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off18));

                // mov [specialMoveTypeAdr+20],rbx
                int off20 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x20) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off20));

                // mov [specialMoveTypeAdr+28],rbx
                int off28 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x28) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off28));

                // mov [specialMoveTypeAdr+30],rbx
                int off30 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x30) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off30));

                // mov [specialMoveTypeAdr+38],rbx
                int off38 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x38) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off38));

                // mov [specialMoveTypeAdr+40],rbx
                int off40 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x40) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off40));

                // Patch jne @f to skip clearing
                int skipClearTarget = injectedCode.Count - (jneSkipClearOffset + 2);
                injectedCode[jneSkipClearOffset + 1] = (byte)skipClearTarget;

                // @@: lea rbx,[rax+04]
                injectedCode.AddRange(new byte[] { 0x48, 0x8D, 0x58, 0x04 });

                // === r8d comparisons and stores ===

                // cmp r8d,0 / jne @f / mov [specialMoveTypeAdr],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x00 }); // cmp r8d,0
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                int offBase = (int)(_specialMoveTypeAdrAddress.ToInt64() - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(offBase));

                // cmp r8d,2 / jne @f / mov [specialMoveTypeAdr+8],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x02 }); // cmp r8d,2
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off8 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 8) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off8));

                // cmp r8d,4 / jne @f / mov [specialMoveTypeAdr+10],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x04 }); // cmp r8d,4
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off10 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x10) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off10));

                // cmp r8d,8 / jne @f / mov [specialMoveTypeAdr+18],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x08 }); // cmp r8d,8
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off18 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x18) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off18));

                // cmp r8d,9 / jne @f / mov [specialMoveTypeAdr+18],rbx (same offset as r8d=8)
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x09 }); // cmp r8d,9
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off18 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x18) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off18));

                // cmp r8d,A / jne @f / mov [specialMoveTypeAdr+20],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x0A }); // cmp r8d,0xA
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off20 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x20) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off20));

                // cmp r8d,B / jne @f / mov [specialMoveTypeAdr+20],rbx (same offset as r8d=A)
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x0B }); // cmp r8d,0xB
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off20 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x20) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off20));

                // cmp r8d,C / jne @f / mov [specialMoveTypeAdr+28],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x0C }); // cmp r8d,0xC
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off28 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x28) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off28));

                // cmp r8d,D / jne @f / mov [specialMoveTypeAdr+28],rbx (same offset as r8d=C)
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x0D }); // cmp r8d,0xD
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off28 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x28) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off28));

                // cmp r8d,13 / jne @f / mov [specialMoveTypeAdr+30],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x13 }); // cmp r8d,0x13
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off30 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x30) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off30));

                // cmp r8d,15 / jne @f / mov [specialMoveTypeAdr+38],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x15 }); // cmp r8d,0x15
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7
                off38 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x38) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off38));

                // cmp r8d,17 / jne code / mov [specialMoveTypeAdr+40],rbx
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xF8, 0x17 }); // cmp r8d,0x17
                injectedCode.AddRange(new byte[] { 0x75, 0x07 }); // jne +7 (to code label)
                off40 = (int)((_specialMoveTypeAdrAddress.ToInt64() + 0x40) - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 7));
                injectedCode.AddRange(new byte[] { 0x48, 0x89, 0x1D });
                injectedCode.AddRange(BitConverter.GetBytes(off40));

                // ===== code: =====
                int codeLabel = injectedCode.Count;

                // mov ebx,[rax+04] - original instruction
                injectedCode.AddRange(new byte[] { 0x8B, 0x58, 0x04 });

                // jmp return
                injectedCode.Add(0xE9);
                long jmpOffset = returnAddress.ToInt64() - (_specialMoveTypeCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpOffset));

                // Patch je to original target
                long jeOffset = originalJeTarget.ToInt64() - (_specialMoveTypeCodeCave.ToInt64() + jeTargetPlaceholder + 4);
                byte[] jeOffsetBytes = BitConverter.GetBytes((int)jeOffset);
                for (int i = 0; i < 4; i++)
                {
                    injectedCode[jeTargetPlaceholder + i] = jeOffsetBytes[i];
                }

                // Write injected code
                if (!WriteProcessMemory(_processHandle, _specialMoveTypeCodeCave, injectedCode.ToArray(), injectedCode.Count, out _))
                {
                    VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                    _specialMoveTypeCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write injected code");
                }

                // Create hook: 5-byte jmp to code cave
                long jmpToCodeCave = _specialMoveTypeCodeCave.ToInt64() - (_specialMoveTypeHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[5];
                hookBytes[0] = 0xE9; // jmp opcode
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);

                // Change memory protection and write hook
                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _specialMoveTypeHookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                    _specialMoveTypeCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _specialMoveTypeHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _specialMoveTypeHookAddress, 5, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                    _specialMoveTypeCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                return true;
            }
            catch (Exception)
            {
                if (_specialMoveTypeCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                    _specialMoveTypeCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool RemoveSpecialMoveTypeEditing()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success = true;

                if (_specialMoveTypeHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes: 74 3D 8B 58 04
                    byte[] originalBytes = new byte[] { 0x74, 0x3D, 0x8B, 0x58, 0x04 };
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _specialMoveTypeHookAddress, (uint)originalBytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _specialMoveTypeHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _specialMoveTypeHookAddress, (uint)originalBytes.Length, oldProtect, out _);

                    _specialMoveTypeHookAddress = IntPtr.Zero;
                }

                if (_specialMoveTypeCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _specialMoveTypeCodeCave, 0, MEM_RELEASE);
                    _specialMoveTypeCodeCave = IntPtr.Zero;
                }

                _specialMoveTypeAdrAddress = IntPtr.Zero;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public (bool hasValue, long address, int currentValue) ReadSpecialMoveValue(int slotIndex)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || _specialMoveTypeAdrAddress == IntPtr.Zero)
            {
                return (false, 0, 0);
            }

            if (slotIndex < 0 || slotIndex > 8)
            {
                return (false, 0, 0);
            }

            try
            {
                // Read the address at specialMoveTypeAdr + (slotIndex * 8)
                IntPtr slotAddress = new IntPtr(_specialMoveTypeAdrAddress.ToInt64() + (slotIndex * 8));
                byte[] adrBuffer = new byte[8];
                if (!ReadProcessMemory(_processHandle, slotAddress, adrBuffer, 8, out _))
                {
                    return (false, 0, 0);
                }

                long valueAddress = BitConverter.ToInt64(adrBuffer, 0);
                if (valueAddress == 0)
                {
                    return (false, 0, 0);
                }

                // Read the actual dword value at that address
                byte[] valueBuffer = new byte[4];
                if (!ReadProcessMemory(_processHandle, new IntPtr(valueAddress), valueBuffer, 4, out _))
                {
                    return (false, 0, 0);
                }

                int currentValue = BitConverter.ToInt32(valueBuffer, 0);
                return (true, valueAddress, currentValue);
            }
            catch (Exception)
            {
                return (false, 0, 0);
            }
        }

        public bool WriteSpecialMoveValue(int slotIndex, int newValue)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || _specialMoveTypeAdrAddress == IntPtr.Zero)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex > 8)
            {
                return false;
            }

            try
            {
                // Read the address at specialMoveTypeAdr + (slotIndex * 8)
                IntPtr slotAddress = new IntPtr(_specialMoveTypeAdrAddress.ToInt64() + (slotIndex * 8));
                byte[] adrBuffer = new byte[8];
                if (!ReadProcessMemory(_processHandle, slotAddress, adrBuffer, 8, out _))
                {
                    return false;
                }

                long valueAddress = BitConverter.ToInt64(adrBuffer, 0);
                if (valueAddress == 0)
                {
                    return false;
                }

                // Write the new value
                byte[] valueBuffer = BitConverter.GetBytes(newValue);
                return WriteProcessMemory(_processHandle, new IntPtr(valueAddress), valueBuffer, valueBuffer.Length, out _);
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
                // AOB scan for first injection point: "0F B7 C3 41 89 06 0F"
                byte[] aobPattern1 = new byte[] { 0x0F, 0xB7, 0xC3, 0x41, 0x89, 0x06, 0x0F };
                byte[] aobMask1 = new byte[] { 1, 1, 1, 1, 1, 1, 1 };
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

                // Build the injection code
                // cmp ebp,4 / je codeA / cmp ebp,5 / je codeA / cmp ebp,6 / je codeA / cmp ebp,7 / je codeA / jmp code
                // codeA: mov bx,[cfFBspiritmarketSpQuan]
                // code: movzx eax,bx / mov [r14],eax / jmp return
                List<byte> injectedCode = new List<byte>();

                // cmp ebp,4
                injectedCode.AddRange(new byte[] { 0x83, 0xFD, 0x04 });
                // je codeA
                int jeCodeA1 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 }); // offset to be patched

                // cmp ebp,5
                injectedCode.AddRange(new byte[] { 0x83, 0xFD, 0x05 });
                // je codeA
                int jeCodeA2 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 });

                // cmp ebp,6
                injectedCode.AddRange(new byte[] { 0x83, 0xFD, 0x06 });
                // je codeA
                int jeCodeA3 = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x74, 0x00 });

                // cmp ebp,7
                injectedCode.AddRange(new byte[] { 0x83, 0xFD, 0x07 });
                // je codeA
                int jeCodeA4 = injectedCode.Count;
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
                byte jeA4Dist = (byte)(codeALabel - (jeCodeA4 + 2));
                injectedCode[jeCodeA4 + 1] = jeA4Dist;

                // mov bx,[cfFBspiritmarketSpQuan]
                // We'll calculate the offset later after we know the code size
                injectedCode.AddRange(new byte[] { 0x66, 0x8B, 0x1D });
                int movBxOffsetPosition = injectedCode.Count; // Remember where to patch the offset
                injectedCode.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Placeholder for offset

                // code:
                int codeLabel = injectedCode.Count;
                // Patch jmp code offset
                byte jmpCodeDist = (byte)(codeLabel - (jmpCode + 2));
                byte[] codeArray = injectedCode.ToArray();
                codeArray[jmpCode + 1] = jmpCodeDist;
                injectedCode = new List<byte>(codeArray);

                // movzx eax,bx
                injectedCode.AddRange(new byte[] { 0x0F, 0xB7, 0xC3 });
                // mov [r14],eax
                injectedCode.AddRange(new byte[] { 0x41, 0x89, 0x06 });

                // jmp return
                injectedCode.Add(0xE9);
                IntPtr returnAddress = new IntPtr(_freeBuySpiritMarketHookAddress.ToInt64() + 6);
                long jmpReturnDist = returnAddress.ToInt64() - (_freeBuySpiritMarketCodeCave.ToInt64() + injectedCode.Count + 4);
                injectedCode.AddRange(BitConverter.GetBytes((int)jmpReturnDist));

                // Set cfFBspiritmarketSpQuan address right after the code
                _cfFBspiritmarketSpQuanAddress = new IntPtr(_freeBuySpiritMarketCodeCave.ToInt64() + injectedCode.Count);

                // Now calculate and patch the mov bx offset
                byte[] codeArray2 = injectedCode.ToArray();
                long movBxInstructionEnd = _freeBuySpiritMarketCodeCave.ToInt64() + movBxOffsetPosition + 4;
                int cfQuanOffset = (int)(_cfFBspiritmarketSpQuanAddress.ToInt64() - movBxInstructionEnd);
                byte[] offsetBytes = BitConverter.GetBytes(cfQuanOffset);
                Array.Copy(offsetBytes, 0, codeArray2, movBxOffsetPosition, 4);
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

                // Create first hook (5 bytes jmp + 1 nop)
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

                // Create second hook (level): mov dl,99; nop
                byte[] levelHookBytes = new byte[] { 0xB2, 0x63, 0x90 }; // mov dl,99; nop

                if (!VirtualProtectEx(_processHandle, _freeBuySpiritMarketLvHookAddress, 3, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    // Clean up first hook
                    byte[] originalBytes1 = new byte[] { 0x0F, 0xB7, 0xC3, 0x41, 0x89, 0x06 };
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
                    byte[] originalBytes1 = new byte[] { 0x0F, 0xB7, 0xC3, 0x41, 0x89, 0x06 };
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
                    // Restore original bytes for first hook: 0F B7 C3 41 89 06
                    byte[] originalBytes1 = new byte[] { 0x0F, 0xB7, 0xC3, 0x41, 0x89, 0x06 };
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

        public bool InjectFreeBuyShop()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for: "4C 8B 7C 24 * 8B * 4C 8B 64"
                // Original instruction: mov r15,[rsp+30]
                byte[] aobPattern = new byte[] { 0x4C, 0x8B, 0x7C, 0x24, 0x00, 0x8B, 0x00, 0x4C, 0x8B, 0x64 };
                byte[] aobMask = new byte[] { 1, 1, 1, 1, 0, 1, 0, 1, 1, 1 };
                _freeBuyShopHookAddress = AOBScan(aobPattern, aobMask);

                if (_freeBuyShopHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Free Buy Shop AOB pattern.\n\nMake sure the game is running.");
                }

                // Allocate code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _freeBuyShopCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_freeBuyShopCodeCave != IntPtr.Zero)
                    {
                        long distance = _freeBuyShopCodeCave.ToInt64() - _freeBuyShopHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                            _freeBuyShopCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_freeBuyShopCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory for Free Buy Shop");
                }

                // Memory layout (matching CE script):
                // +0x00: newmem - injected code
                // +0x??: cfFreeBuyTokenQuan - dd 999
                // +0x??: memfreebuyBck - original 5 bytes backup

                // Read original 5 bytes for backup
                byte[] originalBytes = new byte[5];
                ReadProcessMemory(_processHandle, _freeBuyShopHookAddress, originalBytes, 5, out _);

                // Build the injection code
                List<byte> injectedCode = new List<byte>();

                // newmem:
                // readMem(INJECTfreebuy,5) - Execute original instruction first
                injectedCode.AddRange(originalBytes); // 5 bytes at offset 0-4

                // cmp r15d, 0Bh
                injectedCode.AddRange(new byte[] { 0x41, 0x83, 0xFF, 0x0B }); // 4 bytes at offset 5-8

                // jne code (skip mov eax if r15d != 0x0B)
                // mov eax,[rel32] is 6 bytes, so jne +6
                injectedCode.AddRange(new byte[] { 0x75, 0x06 }); // 2 bytes at offset 9-10

                // mov eax,[cfFreeBuyTokenQuan] (RIP-relative)
                injectedCode.AddRange(new byte[] { 0x8B, 0x05 }); // 2 bytes at offset 11-12
                int movEaxOffsetPosition = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 4 bytes placeholder at offset 13-16

                // code:
                // jmp return
                injectedCode.Add(0xE9); // 1 byte at offset 17
                IntPtr returnAddress = new IntPtr(_freeBuyShopHookAddress.ToInt64() + 5);
                int jmpCodeLabelOffset = injectedCode.Count;
                injectedCode.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 4 bytes placeholder at offset 18-21

                // cfFreeBuyTokenQuan:
                int cfTokenQuanOffset = injectedCode.Count;
                _cfFreeBuyShopTokenQuanAddress = new IntPtr(_freeBuyShopCodeCave.ToInt64() + cfTokenQuanOffset);
                injectedCode.AddRange(BitConverter.GetBytes((int)999)); // dd 999 at offset 22-25

                // memfreebuyBck: (backup of original bytes for disable)
                int memBckOffset = injectedCode.Count;
                injectedCode.AddRange(originalBytes); // 5 bytes backup at offset 26-30

                // Now calculate and patch the RIP-relative offsets
                byte[] codeArray = injectedCode.ToArray();

                // Patch mov eax,[cfFreeBuyTokenQuan] offset
                // RIP-relative: offset = target - (instruction_end)
                long movEaxInstructionEnd = _freeBuyShopCodeCave.ToInt64() + movEaxOffsetPosition + 4;
                int cfQuanRelOffset = (int)(_cfFreeBuyShopTokenQuanAddress.ToInt64() - movEaxInstructionEnd);
                byte[] cfQuanOffsetBytes = BitConverter.GetBytes(cfQuanRelOffset);
                Array.Copy(cfQuanOffsetBytes, 0, codeArray, movEaxOffsetPosition, 4);

                // Patch jmp return offset
                long jmpInstructionEnd = _freeBuyShopCodeCave.ToInt64() + jmpCodeLabelOffset + 4;
                int jmpReturnRelOffset = (int)(returnAddress.ToInt64() - jmpInstructionEnd);
                byte[] jmpReturnOffsetBytes = BitConverter.GetBytes(jmpReturnRelOffset);
                Array.Copy(jmpReturnOffsetBytes, 0, codeArray, jmpCodeLabelOffset, 4);

                // Write injected code + data to code cave
                if (!WriteProcessMemory(_processHandle, _freeBuyShopCodeCave, codeArray, codeArray.Length, out _))
                {
                    VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                    _freeBuyShopCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write Free Buy Shop injection code");
                }

                // Create hook: jmp newmem
                long jmpToCodeCave = _freeBuyShopCodeCave.ToInt64() - (_freeBuyShopHookAddress.ToInt64() + 5);
                byte[] hookBytes = new byte[5];
                hookBytes[0] = 0xE9; // jmp rel32
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);

                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _freeBuyShopHookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                    _freeBuyShopCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _freeBuyShopHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _freeBuyShopHookAddress, 5, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                    _freeBuyShopCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                return true;
            }
            catch (Exception)
            {
                if (_freeBuyShopCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                    _freeBuyShopCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool RemoveFreeBuyShop()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                bool success = true;

                if (_freeBuyShopHookAddress != IntPtr.Zero)
                {
                    // Restore original bytes from memfreebuyBck (at offset 26 in code cave)
                    byte[] originalBytes = new byte[5];
                    IntPtr memBckAddress = new IntPtr(_freeBuyShopCodeCave.ToInt64() + 26);
                    ReadProcessMemory(_processHandle, memBckAddress, originalBytes, 5, out _);

                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _freeBuyShopHookAddress, 5, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _freeBuyShopHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _freeBuyShopHookAddress, 5, oldProtect, out _);

                    _freeBuyShopHookAddress = IntPtr.Zero;
                }

                if (_freeBuyShopCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _freeBuyShopCodeCave, 0, MEM_RELEASE);
                    _freeBuyShopCodeCave = IntPtr.Zero;
                }

                _cfFreeBuyShopTokenQuanAddress = IntPtr.Zero;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool EnablePlayerSpiritInjection()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            if (_isPlayerSpiritInjectionEnabled)
            {
                return true; // Already enabled
            }

            try
            {
                // AOB scan for the pattern from CE script: 4D 8B 64 C? 08 4D 85 E4
                // This is: mov r12,[r14+rax*8+08] followed by test r12,r12
                byte[] aobPattern = new byte[] { 0x4D, 0x8B, 0x64, 0x00, 0x08, 0x4D, 0x85, 0xE4 };
                byte[] aobMask = new byte[] { 1, 1, 1, 0, 1, 1, 1, 1 }; // ? at position 3

                _playerSpiritHookAddress = AOBScan(aobPattern, aobMask);

                if (_playerSpiritHookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find player spirit injection point (AOB pattern not found).\n\nMake sure:\n1. The game is running\n2. You have opened Team Dock - Spirits at least once\n3. The game version is supported");
                }

                // Allocate code cave ($1000 = 4096 bytes)
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };

                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _playerSpiritInjectionCodeCave = VirtualAllocEx(_processHandle, preferredAddress, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

                    if (_playerSpiritInjectionCodeCave != IntPtr.Zero)
                    {
                        long distance = _playerSpiritInjectionCodeCave.ToInt64() - _playerSpiritHookAddress.ToInt64();
                        if (Math.Abs(distance) <= 0x7FFFFFFF)
                        {
                            break;
                        }
                        else
                        {
                            VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                            _playerSpiritInjectionCodeCave = IntPtr.Zero;
                        }
                    }
                }

                if (_playerSpiritInjectionCodeCave == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate memory for player spirit injection");
                }

                // Read the original instruction (5 bytes for mov r12,[r14+rax*8+08])
                byte[] originalBytes = new byte[5];
                ReadProcessMemory(_processHandle, _playerSpiritHookAddress, originalBytes, 5, out _);

                // Memory layout in code cave (matching CE script exactly):
                // +0x000: Injection code
                // +0x200: cfPlayerspiritAddType (4 bytes) - 0 = disabled, 1 = enabled
                // +0x204: cfPlayerspiritID (4 bytes)
                // +0x208: cfPlayerspiritRarity (4 bytes)
                // +0x210: bckRxreg (24 bytes for rax, r14, r13)
                // +0x228: addplayerspiritdata (16 bytes: PlayerID, Rarity, 0, 0)
                // +0x238: addplayerspiritTemp (4 bytes)

                _cfPlayerspiritAddTypeAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x200);
                _cfPlayerspiritIDAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x204);
                _cfPlayerspiritRarityAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x208);
                IntPtr bckRxregAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x210);
                IntPtr addplayerspiritdataAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x228);
                IntPtr addplayerspiritTempAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x238);

                // Initialize memory - DISABLED by default
                byte[] initBytes = BitConverter.GetBytes(0); // Disabled by default
                WriteProcessMemory(_processHandle, _cfPlayerspiritAddTypeAddress, initBytes, 4, out _);
                initBytes = BitConverter.GetBytes(0); // Initial player ID
                WriteProcessMemory(_processHandle, _cfPlayerspiritIDAddress, initBytes, 4, out _);
                initBytes = BitConverter.GetBytes(1); // Initial rarity (Growing)
                WriteProcessMemory(_processHandle, _cfPlayerspiritRarityAddress, initBytes, 4, out _);

                // Initialize addplayerspiritdata structure (PlayerID, Rarity, 0, 0)
                byte[] spiritDataInit = new byte[16];
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 0); // PlayerID
                BitConverter.GetBytes(1).CopyTo(spiritDataInit, 4); // Rarity
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 8);
                BitConverter.GetBytes(0).CopyTo(spiritDataInit, 12);
                WriteProcessMemory(_processHandle, addplayerspiritdataAddress, spiritDataInit, 16, out _);

                // Initialize bckRxreg to zeros
                byte[] bckRxregInit = new byte[24];
                WriteProcessMemory(_processHandle, bckRxregAddress, bckRxregInit, 24, out _);

                // Build injection code matching CE script (Add-One only)
                List<byte> injectionCode = new List<byte>();

                // ===== newmem: =====
                // readMem(INJECTaddplayerspirit,5) - Execute original: mov r12,[r14+rax*8+08]
                injectionCode.AddRange(originalBytes);

                // test r12,r12
                injectionCode.AddRange(new byte[] { 0x4D, 0x85, 0xE4 });

                // je code (skip if r12 is NULL)
                int jeNullOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 }); // je rel32 (patch later)

                // cmp dword ptr [r12+8],0
                injectionCode.AddRange(new byte[] { 0x41, 0x83, 0x7C, 0x24, 0x08, 0x00 });

                // jne code (skip if [r12+8] != 0)
                int jneNotEmptyOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x85, 0x00, 0x00, 0x00, 0x00 }); // jne rel32 (patch later)

                // ===== Save registers =====
                // mov [bckRxreg],rax
                int bckRaxOff = (int)(bckRxregAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x48, 0x89, 0x05 });
                injectionCode.AddRange(BitConverter.GetBytes(bckRaxOff));

                // mov [bckRxreg+8],r14
                int bckR14Off = (int)((bckRxregAddress.ToInt64() + 8) - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x89, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(bckR14Off));

                // mov [bckRxreg+10],r13
                int bckR13Off = (int)((bckRxregAddress.ToInt64() + 0x10) - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x89, 0x2D });
                injectionCode.AddRange(BitConverter.GetBytes(bckR13Off));

                // cmp dword ptr [cfPlayerspiritAddType],0
                int cfTypeOff = (int)(_cfPlayerspiritAddTypeAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 8));
                injectionCode.AddRange(new byte[] { 0x83, 0x3D });
                injectionCode.AddRange(BitConverter.GetBytes(cfTypeOff));
                injectionCode.Add(0x00);

                // je codeE (skip if type == 0, meaning disabled/not ready)
                int jeDisabledOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 }); // je rel32 (patch later)

                // ===== Add-One Mode =====
                // mov r14d,[cfPlayerspiritID]
                int cfIDOff = (int)(_cfPlayerspiritIDAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x44, 0x8B, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(cfIDOff));

                // mov [addplayerspiritdata],r14d
                int dataIDOff = (int)(addplayerspiritdataAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x44, 0x89, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(dataIDOff));

                // mov r14d,[cfPlayerspiritRarity]
                int cfRarityOff = (int)(_cfPlayerspiritRarityAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x44, 0x8B, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(cfRarityOff));

                // mov [addplayerspiritdata+4],r14d
                int dataRarityOff = (int)((addplayerspiritdataAddress.ToInt64() + 4) - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x44, 0x89, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(dataRarityOff));

                // call fncAddPlayerspirit
                int callFncOffset = injectionCode.Count;
                injectionCode.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00 }); // call rel32 (patch later)

                // ===== codeE: Restore registers =====
                int codeEStart = injectionCode.Count;

                // mov rax,[bckRxreg]
                int restoreRaxOff = (int)(bckRxregAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x48, 0x8B, 0x05 });
                injectionCode.AddRange(BitConverter.GetBytes(restoreRaxOff));

                // mov r14,[bckRxreg+8]
                int restoreR14Off = (int)((bckRxregAddress.ToInt64() + 8) - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x8B, 0x35 });
                injectionCode.AddRange(BitConverter.GetBytes(restoreR14Off));

                // mov r13,[bckRxreg+10]
                int restoreR13Off = (int)((bckRxregAddress.ToInt64() + 0x10) - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x8B, 0x2D });
                injectionCode.AddRange(BitConverter.GetBytes(restoreR13Off));

                // ===== code: Exit point =====
                int codeLabel = injectionCode.Count;

                // jmp return (return to hook+8)
                injectionCode.Add(0xE9);
                IntPtr returnAddress = new IntPtr(_playerSpiritHookAddress.ToInt64() + 8);
                long jmpReturnOff = returnAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 4);
                injectionCode.AddRange(BitConverter.GetBytes((int)jmpReturnOff));

                // ===== fncAddPlayerspirit function =====
                int fncStart = injectionCode.Count;

                // push rcx
                injectionCode.Add(0x51);
                // push rdx
                injectionCode.Add(0x52);
                // push r8
                injectionCode.AddRange(new byte[] { 0x41, 0x50 });
                // push r9
                injectionCode.AddRange(new byte[] { 0x41, 0x51 });

                // lea r8,[addplayerspiritdata]
                int r8Off = (int)(addplayerspiritdataAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x4C, 0x8D, 0x05 });
                injectionCode.AddRange(BitConverter.GetBytes(r8Off));

                // lea rdx,[addplayerspiritTemp]
                int rdxOff = (int)(addplayerspiritTempAddress.ToInt64() - (_playerSpiritInjectionCodeCave.ToInt64() + injectionCode.Count + 7));
                injectionCode.AddRange(new byte[] { 0x48, 0x8D, 0x15 });
                injectionCode.AddRange(BitConverter.GetBytes(rdxOff));

                // mov r9d,1
                injectionCode.AddRange(new byte[] { 0x41, 0xB9, 0x01, 0x00, 0x00, 0x00 });

                // mov rcx,r12
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0xCC });

                // mov rax,[r12]
                injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0x04, 0x24 });

                // call qword ptr [rax+20]
                injectionCode.AddRange(new byte[] { 0xFF, 0x50, 0x20 });

                // pop r9
                injectionCode.AddRange(new byte[] { 0x41, 0x59 });
                // pop r8
                injectionCode.AddRange(new byte[] { 0x41, 0x58 });
                // pop rdx
                injectionCode.Add(0x5A);
                // pop rcx
                injectionCode.Add(0x59);

                // ret
                injectionCode.Add(0xC3);

                // ===== Patch all jump offsets =====
                byte[] codeArray = injectionCode.ToArray();

                // Patch je (null check) -> code
                int jeNullTarget = codeLabel - (jeNullOffset + 6);
                Array.Copy(BitConverter.GetBytes(jeNullTarget), 0, codeArray, jeNullOffset + 2, 4);

                // Patch jne (not empty) -> code
                int jneNotEmptyTarget = codeLabel - (jneNotEmptyOffset + 6);
                Array.Copy(BitConverter.GetBytes(jneNotEmptyTarget), 0, codeArray, jneNotEmptyOffset + 2, 4);

                // Patch je (disabled) -> codeE
                int jeDisabledTarget = codeEStart - (jeDisabledOffset + 6);
                Array.Copy(BitConverter.GetBytes(jeDisabledTarget), 0, codeArray, jeDisabledOffset + 2, 4);

                // Patch call fncAddPlayerspirit
                int callTarget = fncStart - (callFncOffset + 5);
                Array.Copy(BitConverter.GetBytes(callTarget), 0, codeArray, callFncOffset + 1, 4);

                // Write injection code to code cave
                if (!WriteProcessMemory(_processHandle, _playerSpiritInjectionCodeCave, codeArray, codeArray.Length, out _))
                {
                    VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                    _playerSpiritInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write injection code");
                }

                // Create hook: replace 8 bytes with jmp(5) + nop(3)
                // Original: mov r12,[r14+rax*8+08] (5 bytes) + test r12,r12 (3 bytes)
                byte[] hookBytes = new byte[8];
                hookBytes[0] = 0xE9; // jmp rel32
                long jmpToCodeCave = _playerSpiritInjectionCodeCave.ToInt64() - (_playerSpiritHookAddress.ToInt64() + 5);
                byte[] hookOffsetBytes = BitConverter.GetBytes((int)jmpToCodeCave);
                Array.Copy(hookOffsetBytes, 0, hookBytes, 1, 4);
                hookBytes[5] = 0x90; // nop
                hookBytes[6] = 0x90; // nop
                hookBytes[7] = 0x90; // nop

                uint oldProtect;
                if (!VirtualProtectEx(_processHandle, _playerSpiritHookAddress, 8, PAGE_EXECUTE_READWRITE, out oldProtect))
                {
                    VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                    _playerSpiritInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to change memory protection");
                }

                bool hookSuccess = WriteProcessMemory(_processHandle, _playerSpiritHookAddress, hookBytes, hookBytes.Length, out _);
                VirtualProtectEx(_processHandle, _playerSpiritHookAddress, 8, oldProtect, out _);

                if (!hookSuccess)
                {
                    VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                    _playerSpiritInjectionCodeCave = IntPtr.Zero;
                    throw new Exception("Failed to write hook");
                }

                _isPlayerSpiritInjectionEnabled = true;
                return true;
            }
            catch (Exception)
            {
                if (_playerSpiritInjectionCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                    _playerSpiritInjectionCodeCave = IntPtr.Zero;
                }
                throw;
            }
        }

        public bool DisablePlayerSpiritInjection()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            if (!_isPlayerSpiritInjectionEnabled)
            {
                return true; // Already disabled
            }

            try
            {
                bool success = true;

                if (_playerSpiritHookAddress != IntPtr.Zero && _playerSpiritInjectionCodeCave != IntPtr.Zero)
                {
                    // Read the original 8 bytes from the beginning of code cave (we stored them there)
                    // First 5 bytes: mov r12,[r14+rax*8+08]
                    // Next 3 bytes: test r12,r12
                    byte[] originalBytes = new byte[8];
                    ReadProcessMemory(_processHandle, _playerSpiritInjectionCodeCave, originalBytes, 8, out _);

                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _playerSpiritHookAddress, 8, PAGE_EXECUTE_READWRITE, out oldProtect);
                    success = WriteProcessMemory(_processHandle, _playerSpiritHookAddress, originalBytes, originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _playerSpiritHookAddress, 8, oldProtect, out _);

                    _playerSpiritHookAddress = IntPtr.Zero;
                }

                if (_playerSpiritInjectionCodeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _playerSpiritInjectionCodeCave, 0, MEM_RELEASE);
                    _playerSpiritInjectionCodeCave = IntPtr.Zero;
                }

                _cfPlayerspiritAddTypeAddress = IntPtr.Zero;
                _cfPlayerspiritIDAddress = IntPtr.Zero;
                _cfPlayerspiritRarityAddress = IntPtr.Zero;
                _isPlayerSpiritInjectionEnabled = false;

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetPlayerSpiritToAdd(uint playerId, int rarity)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || !_isPlayerSpiritInjectionEnabled)
            {
                return false;
            }

            try
            {
                // Set to Add-One mode
                byte[] addTypeBytes = BitConverter.GetBytes(1); // 1 = Add-One mode
                WriteProcessMemory(_processHandle, _cfPlayerspiritAddTypeAddress, addTypeBytes, 4, out _);

                // Write the player ID
                byte[] playerIdBytes = BitConverter.GetBytes(playerId);
                WriteProcessMemory(_processHandle, _cfPlayerspiritIDAddress, playerIdBytes, 4, out _);

                // Write the rarity
                byte[] rarityBytes = BitConverter.GetBytes(rarity);
                bool success = WriteProcessMemory(_processHandle, _cfPlayerspiritRarityAddress, rarityBytes, 4, out _);

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetAllPlayerSpiritsToAdd(List<uint> playerIds, int rarity)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || !_isPlayerSpiritInjectionEnabled)
            {
                return false;
            }

            try
            {
                // Set to Add-All mode
                byte[] addTypeBytes = BitConverter.GetBytes(0); // 0 = Add-All mode
                WriteProcessMemory(_processHandle, _cfPlayerspiritAddTypeAddress, addTypeBytes, 4, out _);

                // Write the rarity
                byte[] rarityBytes = BitConverter.GetBytes(rarity);
                WriteProcessMemory(_processHandle, _cfPlayerspiritRarityAddress, rarityBytes, 4, out _);

                // Write all player IDs to playerSpiritIDList array (at code cave + 0x300)
                IntPtr playerSpiritIDListAddress = new IntPtr(_playerSpiritInjectionCodeCave.ToInt64() + 0x300);

                List<byte> allPlayerIds = new List<byte>();
                foreach (uint playerId in playerIds)
                {
                    allPlayerIds.AddRange(BitConverter.GetBytes(playerId));
                }
                // Add terminating 0
                allPlayerIds.AddRange(BitConverter.GetBytes(0));

                bool success = WriteProcessMemory(_processHandle, playerSpiritIDListAddress, allPlayerIds.ToArray(), allPlayerIds.Count, out _);

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool AddPlayerSpiritToTeam(uint playerId, int rarity)
        {
            if (!_isAttached || _processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            // Enable injection if not already enabled
            if (!_isPlayerSpiritInjectionEnabled)
            {
                EnablePlayerSpiritInjection(); // Let exceptions propagate
            }

            // Set the player spirit to add
            return SetPlayerSpiritToAdd(playerId, rarity);
        }

        public bool ClearPlayerSpiritToAdd()
        {
            if (!_isAttached || _processHandle == IntPtr.Zero || !_isPlayerSpiritInjectionEnabled)
            {
                return false;
            }

            try
            {
                // Set cfPlayerspiritAddType to 0 to disable injection
                byte[] disableBytes = BitConverter.GetBytes(0);
                bool success = WriteProcessMemory(_processHandle, _cfPlayerspiritAddTypeAddress, disableBytes, 4, out _);
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
