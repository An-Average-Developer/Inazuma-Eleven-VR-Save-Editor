using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace InazumaElevenVRSaveEditor.Features.MemoryEditor.Services
{
    /// <summary>
    /// Service for applying custom passive IDs to players
    /// Based on the Cheat Engine script that injects at passive type read
    /// </summary>
    public class CustomPassivesService
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
        private IntPtr _codeCave = IntPtr.Zero;
        private IntPtr _hookAddress = IntPtr.Zero;
        private IntPtr _customPassivesAddress = IntPtr.Zero; // User's custom values to write
        private IntPtr _currentPassivesAddress = IntPtr.Zero; // Current values read from game
        private byte[] _originalBytes = Array.Empty<byte>();

        // Store custom passive IDs (as hex strings)
        private string[] _customPassiveIds = new string[5];

        public bool IsEnabled => _isEnabled;

        public string[] GetCurrentPassives()
        {
            if (_currentPassivesAddress == IntPtr.Zero || _processHandle == IntPtr.Zero)
            {
                return new string[5];
            }

            byte[] passiveData = new byte[40]; // 5 passives * 8 bytes each
            if (!ReadProcessMemory(_processHandle, _currentPassivesAddress, passiveData, 40, out _))
            {
                return new string[5];
            }

            string[] passives = new string[5];
            for (int i = 0; i < 5; i++)
            {
                // Read first 4 bytes of each 8-byte entry
                uint passiveId = BitConverter.ToUInt32(passiveData, i * 8);
                passives[i] = passiveId == 0 ? "" : passiveId.ToString("X8");
            }

            return passives;
        }

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
                DisableCustomPassives();
            }

            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
            _targetProcess = null;
        }

        public bool ApplyCustomPassives(string[] passiveIds)
        {
            if (passiveIds.Length != 5)
            {
                throw new ArgumentException("Must provide exactly 5 passive IDs");
            }

            _customPassiveIds = (string[])passiveIds.Clone();

            if (_isEnabled)
            {
                // If already enabled, just update the passive data in memory
                return UpdatePassiveData();
            }
            else
            {
                // Enable for the first time
                return EnableCustomPassives();
            }
        }

        public bool EnableTracking()
        {
            // Enable the hook in tracking-only mode (no custom overrides)
            // This allows us to read current passives without modifying them
            if (_isEnabled)
            {
                return true; // Already enabled
            }

            // Apply with all empty strings (no overrides)
            string[] emptyOverrides = new string[5] { "", "", "", "", "" };
            return ApplyCustomPassives(emptyOverrides);
        }

        private bool EnableCustomPassives()
        {
            if (_processHandle == IntPtr.Zero || _moduleBase == IntPtr.Zero)
            {
                throw new InvalidOperationException("Not attached to process");
            }

            try
            {
                // AOB scan for pattern: 42 8B 9C AD * * 00 00
                // This is the instruction: mov ebx,[rbp+r13*4+000001E0]
                byte[] aobPattern = new byte[] { 0x42, 0x8B, 0x9C, 0xAD, 0x00, 0x00, 0x00, 0x00 };
                byte[] aobMask = new byte[] { 1, 1, 1, 1, 0, 0, 1, 1 }; // Wildcards for bytes 4-5
                _hookAddress = AOBScan(aobPattern, aobMask);

                if (_hookAddress == IntPtr.Zero)
                {
                    throw new Exception("Failed to find Custom Passives AOB pattern.\n\n" +
                        "Make sure:\n" +
                        "1. The game is running\n" +
                        "2. You are in the Abilearn Board or hovering over a player\n" +
                        "3. The game version is correct");
                }

                // Read original bytes (8 bytes for the full instruction)
                _originalBytes = new byte[8];
                if (!ReadProcessMemory(_processHandle, _hookAddress, _originalBytes, 8, out _))
                {
                    throw new Exception("Failed to read original bytes at hook address");
                }

                // Allocate memory for code cave
                long[] offsets = { 0x10000000, 0x20000000, 0x30000000, -0x10000000, -0x20000000, 0x05000000, 0x01000000 };
                foreach (long offset in offsets)
                {
                    IntPtr preferredAddress = new IntPtr(_moduleBase.ToInt64() + offset);
                    _codeCave = VirtualAllocEx(_processHandle, preferredAddress, 8192, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

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
                    throw new Exception("Failed to allocate code cave");
                }

                // Calculate data addresses (8 bytes per entry for 5 passives = 40 bytes each)
                // Place after code cave (reserve 256 bytes for code)
                _currentPassivesAddress = new IntPtr(_codeCave.ToInt64() + 256);  // Current passives read from game
                _customPassivesAddress = new IntPtr(_codeCave.ToInt64() + 512);   // Custom passives set by user

                // Initialize both arrays to zero
                byte[] zeroData = new byte[40];
                if (!WriteProcessMemory(_processHandle, _currentPassivesAddress, zeroData, 40, out _))
                {
                    throw new Exception("Failed to initialize current passives array");
                }
                if (!WriteProcessMemory(_processHandle, _customPassivesAddress, zeroData, 40, out _))
                {
                    throw new Exception("Failed to initialize custom passives array");
                }

                // Write initial custom passive data
                if (!UpdatePassiveData())
                {
                    throw new Exception("Failed to write initial passive data");
                }

                // Build code cave
                // The code cave needs to:
                // 1. Load the passive ID from our custom data array based on r13 index
                // 2. Return to original code
                List<byte> codeCaveBytes = BuildCodeCave();

                // Write code cave
                if (!WriteProcessMemory(_processHandle, _codeCave, codeCaveBytes.ToArray(), codeCaveBytes.Count, out _))
                {
                    throw new Exception("Failed to write code cave");
                }

                // Create jump to code cave from hook point
                long hookToCodeCave = _codeCave.ToInt64() - _hookAddress.ToInt64() - 5;
                if (Math.Abs(hookToCodeCave) > 0x7FFFFFFF)
                {
                    throw new Exception("Code cave is too far from hook point for relative jump");
                }

                byte[] jmpToCodeCave = new byte[8];
                jmpToCodeCave[0] = 0xE9; // jmp rel32
                byte[] offsetBytes = BitConverter.GetBytes((int)hookToCodeCave);
                Array.Copy(offsetBytes, 0, jmpToCodeCave, 1, 4);
                // Fill remaining bytes with NOPs
                for (int i = 5; i < 8; i++)
                {
                    jmpToCodeCave[i] = 0x90; // NOP
                }

                // Write jump at hook point
                uint oldProtect;
                VirtualProtectEx(_processHandle, _hookAddress, 8, PAGE_EXECUTE_READWRITE, out oldProtect);
                if (!WriteProcessMemory(_processHandle, _hookAddress, jmpToCodeCave, 8, out _))
                {
                    throw new Exception("Failed to write jump at hook point");
                }
                VirtualProtectEx(_processHandle, _hookAddress, 8, oldProtect, out _);

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

        private List<byte> BuildCodeCave()
        {
            List<byte> code = new List<byte>();

            // NEW APPROACH - Always execute original instruction first
            // This matches the CE script logic and should prevent crashes
            // 1. Execute original instruction to read current passive into EBX
            // 2. Store EBX to currentPassivesAddress so UI can read it
            // 3. Check customPassivesAddress for user override
            // 4. If override exists (non-zero), replace EBX with custom value
            // 5. Return to game

            // Execute original instruction first
            code.AddRange(_originalBytes); // mov ebx,[rbp+r13*4+0x1E0]

            // Save registers we'll use
            code.AddRange(new byte[] { 0x50 }); // push rax
            code.AddRange(new byte[] { 0x51 }); // push rcx

            // Bounds check: r13 < 5
            code.AddRange(new byte[] { 0x41, 0x83, 0xFD, 0x05 }); // cmp r13d, 5
            int jaeSkipOffset = code.Count;
            code.AddRange(new byte[] { 0x73, 0x00 }); // jae skip (short jump, will patch)

            // Store current passive to tracking array: currentPassivesAddress[r13*8] = ebx
            code.AddRange(new byte[] { 0x48, 0xB8 }); // movabs rax, currentPassivesAddress
            code.AddRange(BitConverter.GetBytes(_currentPassivesAddress.ToInt64()));
            code.AddRange(new byte[] { 0x42, 0x89, 0x1C, 0xE8 }); // mov [rax+r13*8], ebx

            // Check for custom override: load customPassivesAddress[r13*8] into ecx
            code.AddRange(new byte[] { 0x48, 0xB8 }); // movabs rax, customPassivesAddress
            code.AddRange(BitConverter.GetBytes(_customPassivesAddress.ToInt64()));
            code.AddRange(new byte[] { 0x42, 0x8B, 0x0C, 0xE8 }); // mov ecx, [rax+r13*8]

            // Test if custom value is zero
            code.AddRange(new byte[] { 0x85, 0xC9 }); // test ecx, ecx
            int jzNoOverrideOffset = code.Count;
            code.AddRange(new byte[] { 0x74, 0x00 }); // jz noOverride (short jump, will patch)

            // Apply custom override: ebx = ecx
            code.AddRange(new byte[] { 0x89, 0xCB }); // mov ebx, ecx

            // noOverride label
            int noOverrideOffset = code.Count;

            // skip label (for bounds check)
            int skipOffset = code.Count;

            // Restore registers
            code.AddRange(new byte[] { 0x59 }); // pop rcx
            code.AddRange(new byte[] { 0x58 }); // pop rax

            // Return to game
            long returnAddress = _hookAddress.ToInt64() + 8;
            long jmpToReturnOffset = returnAddress - (_codeCave.ToInt64() + code.Count + 5);
            code.Add(0xE9); // jmp rel32
            code.AddRange(BitConverter.GetBytes((int)jmpToReturnOffset));

            // Patch short jumps
            code[jaeSkipOffset + 1] = (byte)(skipOffset - (jaeSkipOffset + 2));
            code[jzNoOverrideOffset + 1] = (byte)(noOverrideOffset - (jzNoOverrideOffset + 2));

            return code;
        }

        private bool UpdatePassiveData()
        {
            if (_customPassivesAddress == IntPtr.Zero || _processHandle == IntPtr.Zero)
            {
                return false;
            }

            // Convert hex string IDs to 32-bit integers and write them to CUSTOM array
            // Use 8-byte entries to match CE script and BuildCodeCave (4 bytes for value + 4 bytes padding)
            byte[] passiveData = new byte[40]; // 5 passives * 8 bytes each

            for (int i = 0; i < 5; i++)
            {
                uint passiveId = 0;
                if (!string.IsNullOrEmpty(_customPassiveIds[i]))
                {
                    try
                    {
                        passiveId = Convert.ToUInt32(_customPassiveIds[i], 16);
                    }
                    catch
                    {
                        passiveId = 0;
                    }
                }

                byte[] idBytes = BitConverter.GetBytes(passiveId);
                Array.Copy(idBytes, 0, passiveData, i * 8, 4); // First 4 bytes at offset i*8
                // Remaining 4 bytes stay as 0 (padding)
            }

            return WriteProcessMemory(_processHandle, _customPassivesAddress, passiveData, 40, out _);
        }

        public bool DisableCustomPassives()
        {
            if (_processHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Restore original bytes at hook point
                if (_hookAddress != IntPtr.Zero && _originalBytes.Length > 0)
                {
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, _hookAddress, (uint)_originalBytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    WriteProcessMemory(_processHandle, _hookAddress, _originalBytes, _originalBytes.Length, out _);
                    VirtualProtectEx(_processHandle, _hookAddress, (uint)_originalBytes.Length, oldProtect, out _);
                }

                // Free code cave
                if (_codeCave != IntPtr.Zero)
                {
                    VirtualFreeEx(_processHandle, _codeCave, 0, MEM_RELEASE);
                    _codeCave = IntPtr.Zero;
                }

                _currentPassivesAddress = IntPtr.Zero;
                _customPassivesAddress = IntPtr.Zero;
                _isEnabled = false;
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
