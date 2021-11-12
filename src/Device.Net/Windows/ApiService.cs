using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1021 // Avoid out parameters
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
#pragma warning disable CA1060 // Move pinvokes to native methods class

namespace Device.Net.Windows
{
    internal class ApiService : IApiService
    {
        #region Fields
#if NETFRAMEWORK
        private const uint FILE_FLAG_OVERLAPPED = 0;
#else
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
#endif

        protected ILogger Logger { get; }
        #endregion

        #region Constructor
        public ApiService(ILogger logger = null) => Logger = logger ?? NullLogger.Instance;
        #endregion

        #region Implementation
        public SafeFileHandle CreateFile(string lpFileName, FileAccessRights dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile)
        {
            const string LOG_FORMAT =
                "Calling {call} Area: {area} for DeviceId: {lpFileName}. " +
                "Desired Access: {dwDesiredAccess}. " +
                "Share mode: {dwShareMode}. " +
                "Creation Disposition: {dwCreationDisposition}. " +
                "File Flag: {dwFlagsAndAttributes}";
            Logger.LogInformation(
                LOG_FORMAT,
                nameof(APICalls.CreateFile),
                nameof(ApiService),
                lpFileName,
                dwDesiredAccess,
                dwShareMode,
                dwCreationDisposition,
                dwFlagsAndAttributes);
            return APICalls.CreateFile(
                lpFileName,
                dwDesiredAccess,
                dwShareMode,
                lpSecurityAttributes,
                dwCreationDisposition,
                dwFlagsAndAttributes,
                hTemplateFile);
        }

        public SafeFileHandle CreateWriteConnection(string deviceId) =>
            CreateFile(
                deviceId,
                FileAccessRights.GenericRead | FileAccessRights.GenericWrite,
                APICalls.FileShareRead | APICalls.FileShareWrite,
                IntPtr.Zero,
                APICalls.OpenExisting,
                FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

        public SafeFileHandle CreateReadConnection(string deviceId, FileAccessRights desiredAccess) =>
            CreateFile(
                deviceId,
                desiredAccess,
                APICalls.FileShareRead | APICalls.FileShareWrite,
                IntPtr.Zero,
                APICalls.OpenExisting,
                FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

        public bool AGetCommState(SafeFileHandle hFile, ref Dcb lpDCB) => GetCommState(hFile, ref lpDCB);
        public bool APurgeComm(SafeFileHandle hFile, int dwFlags) => PurgeComm(hFile, dwFlags);
        public bool ASetCommTimeouts(SafeFileHandle hFile, ref CommTimeouts lpCommTimeouts) => SetCommTimeouts(hFile, ref lpCommTimeouts);
        public bool AWriteFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, int lpOverlapped) => WriteFile(hFile, lpBuffer, nNumberOfBytesToWrite, out lpNumberOfBytesWritten, lpOverlapped);
        public bool AReadFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out uint lpNumberOfBytesRead, int lpOverlapped) => ReadFile(hFile, lpBuffer, nNumberOfBytesToRead, out lpNumberOfBytesRead, lpOverlapped);
        public bool ASetCommState(SafeFileHandle hFile, [In] ref Dcb lpDCB) => SetCommState(hFile, ref lpDCB);
        #endregion

        #region DLL Imports
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PurgeComm(SafeFileHandle hFile, int dwFlags);
        [DllImport("kernel32.dll")]
        private static extern bool GetCommState(SafeFileHandle hFile, ref Dcb lpDCB);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(SafeFileHandle hFile, ref CommTimeouts lpCommTimeouts);
        [DllImport("kernel32.dll")]
        private static extern bool SetCommState(SafeFileHandle hFile, [In] ref Dcb lpDCB);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, int lpOverlapped);
        [DllImport("kernel32.dll")]
        private static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out uint lpNumberOfBytesRead, int lpOverlapped);
        #endregion
    }
}
