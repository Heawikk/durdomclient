using System;
using System.Runtime.InteropServices;

namespace DurdomClient.Services
{
    public sealed class JobObjectManager : IDisposable
    {
        private readonly IntPtr _hJob;
        private bool _disposed;

        public JobObjectManager()
        {
            _hJob = CreateJobObject(IntPtr.Zero, null);
            if (_hJob == IntPtr.Zero)
                return;

            var extInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            extInfo.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            int size   = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(extInfo, ptr, false);
                SetInformationJobObject(_hJob, JobObjectExtendedLimitInformation, ptr, (uint)size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void AssignProcess(System.Diagnostics.Process process)
        {
            if (_hJob == IntPtr.Zero) return;
            try { AssignProcessToJobObject(_hJob, process.Handle); }
            catch { /* process may have already exited */ }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_hJob != IntPtr.Zero)
                    CloseHandle(_hJob);
            }
        }

        private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE    = 0x2000;
        private const int JobObjectExtendedLimitInformation = 9;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, int infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long    PerProcessUserTimeLimit;
            public long    PerJobUserTimeLimit;
            public int     LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public int     ActiveProcessLimit;
            public IntPtr  Affinity;
            public int     PriorityClass;
            public int     SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount,  WriteTransferCount,  OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr     ProcessMemoryLimit;
            public UIntPtr     JobMemoryLimit;
            public UIntPtr     PeakProcessMemoryUsed;
            public UIntPtr     PeakJobMemoryUsed;
        }
    }
}
