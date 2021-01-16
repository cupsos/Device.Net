using Device.Net;
using Device.Net.Exceptions;
using Device.Net.UWP;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Foundation;
using Windows.Storage;
using hidDevice = Windows.Devices.HumanInterfaceDevice.HidDevice;

namespace Hid.Net.UWP
{
    ///<inheritdoc cref="IHidDevice"/>
    public class UwpHidDeviceHandler : UwpDeviceHandler<hidDevice>, IHidDeviceHandler
    {

        #region Private Fields

        //private readonly Func<TransferResult, ReadReport> _readTransferTransform;
        private readonly SemaphoreSlim _WriteAndReadLock = new SemaphoreSlim(1, 1);
        //private readonly Func<byte[], byte, byte[]> _writeTransferTransform;
        private ushort? _readBufferSize = null;
        private ushort? _writeBufferSize = null;
        private bool disposed;

        #endregion Private Fields

        #region Public Constructors

        public UwpHidDeviceHandler(
                    ConnectedDeviceDefinition connectedDeviceDefinition,
                    IDataReceiver dataReceiver,
                    ILoggerFactory loggerFactory = null,
                    ushort? writeBufferSize = null,
                    ushort? readBufferSize = null) : base(connectedDeviceDefinition.DeviceId, dataReceiver, loggerFactory)
        {
            ConnectedDeviceDefinition = connectedDeviceDefinition ?? throw new ArgumentNullException(nameof(connectedDeviceDefinition));
            _writeBufferSize = writeBufferSize;
            _writeBufferSize = readBufferSize;
        }

        #endregion Public Constructors

        #region Public Properties

        public bool? IsReadOnly => false;

        public ushort? ReadBufferSize => _readBufferSize ?? throw new InvalidOperationException("Not initialized");

        /// <summary>
        /// TODO: These vales are completely wrong. 
        /// </summary>
        public ushort? WriteBufferSize => _writeBufferSize ?? throw new InvalidOperationException("Not initialized");

        #endregion Public Properties

        #region Public Methods

        public static IAsyncOperation<hidDevice> GetHidDevice(string id) => hidDevice.FromIdAsync(id, FileAccessMode.ReadWrite);

        public override void Dispose()
        {
            if (disposed)
            {
                Logger.LogWarning(Messages.WarningMessageAlreadyDisposed, DeviceId);
                return;
            }

            disposed = true;

            Logger.LogInformation(Messages.InformationMessageDisposingDevice, DeviceId);

            DataReceiver.Dispose();
            _WriteAndReadLock.Dispose();
            ConnectedDevice.InputReportReceived -= ConnectedDevice_InputReportReceived;

            base.Dispose();
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            //TODO: Put a lock here to stop reentrancy of multiple calls
            using var loggerScope = Logger?.BeginScope("DeviceId: {deviceId} Region: {region}", DeviceId, nameof(UwpHidDeviceHandler));

            Logger.LogInformation("Initializing Hid device {deviceId}", DeviceId);

            try
            {
                if (disposed) throw new ValidationException(Messages.DeviceDisposedErrorMessage);

                Logger.LogDebug(Messages.InformationMessageInitializingDevice);

                await GetDeviceAsync(DeviceId, cancellationToken);

                if (_writeBufferSize == null)
                {
                    //I can't figure out how to get device descriptors for Hid on UWP...
                    //We can create an output report and get the length which should give us the size the write buffer size
                    //and then we guess that the read buffer size is the same?

                    var hidOutputReport = ConnectedDevice.CreateOutputReport();
                    _writeBufferSize = (ushort)hidOutputReport.Data.ToArray().Length;
                    _readBufferSize = _writeBufferSize;
                }

                if (ConnectedDevice != null)
                {
                    ConnectedDevice.InputReportReceived += ConnectedDevice_InputReportReceived;
                }
                else
                {
                    throw new DeviceException($"The device {DeviceId} failed to initialize");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, Messages.ErrorMessageCouldntIntializeDevice);
                throw;
            }
        }

        public async Task<ReadReport> ReadReportAsync(CancellationToken cancellationToken = default)
        {
            var transferResult = await DataReceiver.ReadAsync(cancellationToken);

            var length = transferResult.Data.Length - 1;
            var data = new byte[length];
            Array.Copy(transferResult.Data, 1, data, 0, length);
            transferResult = new TransferResult(data, transferResult.BytesTransferred);

            Logger.LogDataTransfer(new Trace(false, transferResult));

            var reportId = transferResult.Data[0];

            return new ReadReport(reportId, new TransferResult(transferResult, transferResult.BytesTransferred));
        }

        public async Task<uint> WriteReportAsync(byte[] data, byte reportId, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (DataReceiver.HasData) Logger.LogWarning("Writing to device but data has already been received that has not been read");

            byte[] bytes;

            bytes = new byte[data.Length + 1];
            Array.Copy(data, 0, bytes, 1, data.Length);
            bytes[0] = reportId;

            var buffer = bytes.AsBuffer();
            var outReport = ConnectedDevice.CreateOutputReport();
            outReport.Data = buffer;

            try
            {
                var operation = ConnectedDevice.SendOutputReportAsync(outReport);
                var count = await operation.AsTask(cancellationToken);
                if (count == bytes.Length)
                {
                    Logger.LogDataTransfer(new Trace(true, bytes));
                }
                else
                {
                    Logger.LogError(Messages.GetErrorMessageInvalidWriteLength(bytes.Length, count) + "{length} {count}", bytes.Length, count, GetType().Name);
                    throw new IOException(Messages.GetErrorMessageInvalidWriteLength(bytes.Length, count));
                }

                return count;
            }
            catch (ArgumentException ex)
            {
                //TODO: Check the string is nasty. Validation on the size of the array being sent should be done earlier anyway
                if (string.Equals(ex.Message, "Value does not fall within the expected range.", StringComparison.Ordinal))
                {
                    throw new IOException("It seems that the data being sent to the device does not match the accepted size. Have you checked DataHasExtraByte?", ex);
                }
                throw;
            }
        }

        #endregion Public Methods

        #region Protected Methods

        protected override IAsyncOperation<hidDevice> FromIdAsync(string id) => GetHidDevice(id);

        #endregion Protected Methods

        #region Private Methods

        private void ConnectedDevice_InputReportReceived(hidDevice sender, HidInputReportReceivedEventArgs args)
        {
            Logger.LogDebug("Received Hid report Id: {id}", args?.Report?.Id);

            using var stream = args.Report.Data.AsStream();

            var bytes = new byte[args.Report.Data.Length];

            var bytesRead = (uint)stream.Read(bytes, 0, (int)args.Report.Data.Length);

            DataReceiver.DataReceived(new TransferResult(bytes, bytesRead));
        }

        #endregion Private Methods

    }
}
