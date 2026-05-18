using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class Scanner
{
    private IntPtr _processHandle;
    private int _pid;

    public int f1, f2, f3, f4, f5, f6;

    private List<IntPtr>[] candidates = new List<IntPtr>[6];
    private Dictionary<IntPtr, float>[] lastValues = new Dictionary<IntPtr, float>[6];

    const int CHUNK_SIZE = 4096; // 4KB pages → safe

    public Scanner(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out _pid);
        _processHandle = OpenProcess(ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VirtualMemoryRead, false, _pid);

        for (int i = 0; i < 6; i++)
        {
            candidates[i] = new List<IntPtr>();
            lastValues[i] = new Dictionary<IntPtr, float>();
        }
    }

    public void SetData((int,int,int,int,int,int) data) {
        f1 = data.Item1;
        f2 = data.Item2;
        f3 = data.Item3;
        f4 = data.Item4;
        f5 = data.Item5;
        f6 = data.Item6;
    }
    
    private object[] locks = new object[6]
    {
        new object(), new object(), new object(),
        new object(), new object(), new object()
    };

    // ================= FIRST SCAN =================
    public void FirstScan()
    {
        ClearAll();

        List<MEMORY_BASIC_INFORMATION> regions = GetMemoryRegions();

        Parallel.ForEach(regions, region =>
        {
            Console.WriteLine($"Scanning Region {region.BaseAddress} with size {region.RegionSize}");
            ScanRegion(region.BaseAddress, region.RegionSize);
        });
    }
    
    private List<MEMORY_BASIC_INFORMATION> GetMemoryRegions()
    {
        var list = new List<MEMORY_BASIC_INFORMATION>();

        IntPtr addr = IntPtr.Zero;
        MEMORY_BASIC_INFORMATION mbi;

        while (VirtualQueryEx(_processHandle, addr, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0)
        {
            if (IsReadable(mbi))
            {
                list.Add(mbi);
            }

            addr = new IntPtr(mbi.BaseAddress.ToInt64() + (long)mbi.RegionSize);
        }

        return list;
    }

    // ================= NEXT SCAN =================
    public void NextScan()
    {
        FilterCandidates(0, f1);
        FilterCandidates(1, f2);
        FilterCandidates(2, f3);
        FilterCandidates(3, f4);
        FilterCandidates(4, f5);
        // FilterCandidates(5, f6);
    }

    private void FilterCandidates(int idx, int target)
    {
        var newList = new List<IntPtr>();
        var newDict = new Dictionary<IntPtr, float>();

        foreach (var addr in candidates[idx])
        {
            if (ReadFloat(addr, out float val))
            {
                if (InRange(val, target))
                {
                    newList.Add(addr);
                    newDict[addr] = val;
                }
            }
        }

        candidates[idx] = newList;
        lastValues[idx] = newDict;
    }

    // ================= SCAN REGION (CHUNKED) =================
    private void ScanRegion(IntPtr baseAddr, long size)
    {
        long offset = 0;

        while (offset < size)
        {
            int toRead = (int)Math.Min(CHUNK_SIZE, size - offset);
            byte[] buffer = new byte[toRead];

            IntPtr addr = (IntPtr)(baseAddr + offset);

            if (ReadProcessMemory(_processHandle, addr, buffer, buffer.Length, out int bytesRead))
            {
                ScanBuffer(buffer, addr, bytesRead);
            }

            offset += toRead;
        }

        Console.WriteLine("Region Scanned");
    }

    private void ScanBuffer(byte[] buffer, IntPtr baseAddress, int bytesRead)
    {
        for (int i = 0; i < bytesRead - 4; i += 4)
        {
            float val = BitConverter.ToSingle(buffer, i);

            if (float.IsNaN(val) || float.IsInfinity(val))
                continue;

            CheckAll(val, baseAddress + i);
        }
    }

    private void CheckAll(float val, IntPtr addr)
    {
        Check(0, val, addr, f1);
        Check(1, val, addr, f2);
        Check(2, val, addr, f3);
        Check(3, val, addr, f4);
        Check(4, val, addr, f5);
        // Check(5, val, addr, f6);
    }

    private void Check(int idx, float val, IntPtr addr, int target)
    {
        if (target == 0) {
            return;
        }
        if (InRange(val, target))
        {
            lock (locks[idx]) {
                candidates[idx].Add(addr);
                lastValues[idx][addr] = val;
            }
        }
    }

    private bool ReadFloat(IntPtr addr, out float value)
    {
        byte[] buffer = new byte[4];

        if (ReadProcessMemory(_processHandle, addr, buffer, 4, out int read) && read == 4)
        {
            value = BitConverter.ToSingle(buffer, 0);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        value = 0;
        return false;
    }

    private bool InRange(float val, int target)
    {
        return val >= target && val < target + 1;
    }

    private bool IsReadable(MEMORY_BASIC_INFORMATION mbi)
    {
        return mbi.State == MEM_COMMIT &&
               (mbi.Protect & PAGE_GUARD) == 0 &&
               (mbi.Protect & PAGE_NOACCESS) == 0;
    }

    // ================= OUTPUT =================

    public IntPtr[] GetMatches(int idx)
    {
        return candidates[idx].ToArray();
    }

    public int Count(int idx)
    {
        return candidates[idx].Count;
    }

    private void ClearAll()
    {
        for (int i = 0; i < 6; i++)
        {
            candidates[i].Clear();
            lastValues[i].Clear();
        }
    }

    // ================= WIN32 =================

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    const uint MEM_COMMIT = 0x1000;
    const uint PAGE_GUARD = 0x100;
    const uint PAGE_NOACCESS = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [Flags]
    enum ProcessAccessFlags : uint
    {
        VirtualMemoryRead = 0x0010,
        QueryInformation = 0x0400
    }
}