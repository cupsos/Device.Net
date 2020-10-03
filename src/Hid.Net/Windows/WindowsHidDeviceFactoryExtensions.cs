﻿using Device.Net;
using Device.Net.Exceptions;
using Device.Net.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Hid.Net.Windows
{

    public static class WindowsHidDeviceFactoryExtensions
    {
        public static IDeviceManager CreateWindowsHidDeviceManager(
        this FilterDeviceDefinition filterDeviceDefinition,
        ILoggerFactory loggerFactory = null,
        IHidApiService hidApiService = null,
        Guid? classGuid = null,
        ushort? readBufferSize = null,
        ushort? writeBufferSize = null)
        {
            var factory = CreateWindowsHidDeviceFactory(
                filterDeviceDefinition,
                loggerFactory,
                hidApiService,
                classGuid,
                readBufferSize,
                writeBufferSize
                );

            return new DeviceManager(new ReadOnlyCollection<IDeviceFactory>(new List<IDeviceFactory> { factory }), loggerFactory);
        }

        public static IDeviceFactory CreateWindowsHidDeviceFactory(
        this FilterDeviceDefinition filterDeviceDefinition,
        ILoggerFactory loggerFactory = null,
        IHidApiService hidApiService = null,
        Guid? classGuid = null,
        ushort? readBufferSize = null,
        ushort? writeBufferSize = null,
        byte? defaultReportId = null)
        {
            return CreateWindowsHidDeviceFactory(
                new List<FilterDeviceDefinition> { filterDeviceDefinition },
                loggerFactory,
                hidApiService,
                classGuid,
                readBufferSize,
                writeBufferSize,
                defaultReportId
                );
        }

        public static IDeviceFactory CreateWindowsHidDeviceFactory(
            this IEnumerable<FilterDeviceDefinition> filterDeviceDefinitions,
            ILoggerFactory loggerFactory = null,
            IHidApiService hidApiService = null,
            Guid? classGuid = null,
            ushort? readBufferSize = null,
            ushort? writeBufferSize = null,
            byte? defaultReportId = null)
        {
            loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

            var selectedHidApiService = hidApiService ?? new WindowsHidApiService(loggerFactory);

            var windowsDeviceEnumerator = new WindowsDeviceEnumerator(
                loggerFactory.CreateLogger<WindowsDeviceEnumerator>(),
                classGuid ?? selectedHidApiService.GetHidGuid(),
                d => GetDeviceDefinition(d, selectedHidApiService, loggerFactory.CreateLogger(nameof(WindowsHidDeviceFactoryExtensions))),
                async c =>
                    filterDeviceDefinitions.FirstOrDefault(f => DeviceManager.IsDefinitionMatch(f, c, DeviceType.Hid)) != null
                );

            return new DeviceFactory(
                loggerFactory,
                windowsDeviceEnumerator.GetConnectedDeviceDefinitionsAsync,
                async c => new WindowsHidDevice
                (
                    c,
                    loggerFactory: loggerFactory,
                    hidService: selectedHidApiService,
                    readBufferSize: readBufferSize,
                    writeBufferSize: writeBufferSize,
                    defaultReportId: defaultReportId
                ),
                DeviceType.Hid);
        }

        private static ConnectedDeviceDefinition GetDeviceDefinition(string deviceId, IHidApiService HidService, ILogger logger)
        {
            IDisposable logScope = null;

            try
            {
                logger = logger ?? NullLogger.Instance;

                logScope = logger.BeginScope("DeviceId: {deviceId} Call: {call}", deviceId, nameof(GetDeviceDefinition));

                using (var safeFileHandle = HidService.CreateReadConnection(deviceId, FileAccessRights.None))
                {
                    if (safeFileHandle.IsInvalid) throw new DeviceException($"{nameof(HidService.CreateReadConnection)} call with Id of {deviceId} failed.");

                    logger.LogDebug(Messages.InformationMessageFoundDevice);

                    return HidService.GetDeviceDefinition(deviceId, safeFileHandle);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, Messages.ErrorMessageCouldntGetDevice);
                return null;
            }
            finally
            {
                logScope.Dispose();
            }
        }
    }

}