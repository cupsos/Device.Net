﻿using Android.Hardware.Usb;
using System;

namespace Usb.Net.Android
{
    public class AndroidUsbEndpoint : IUsbInterfaceEndpoint
    {
        public UsbEndpoint UsbEndpoint { get; }
        public bool IsRead { get; }
        public bool IsWrite { get; }
        public bool IsInterrupt { get; }
        public byte PipeId { get; }
        public ushort MaxPacketSize => (ushort)UsbEndpoint.MaxPacketSize;

        public AndroidUsbEndpoint(UsbEndpoint usbEndpoint)
        {
            if (usbEndpoint == null) throw new ArgumentNullException(nameof(usbEndpoint));

            var isRead = usbEndpoint.Direction == UsbAddressing.In;
            var isWrite = usbEndpoint.Direction == UsbAddressing.Out;
            var isInterrupt = usbEndpoint.Type == UsbAddressing.XferInterrupt;

            IsRead = isRead;
            IsWrite = isWrite;
            IsInterrupt = isInterrupt;
            UsbEndpoint = usbEndpoint;
            PipeId = (byte)usbEndpoint.Address;
        }
    }
}