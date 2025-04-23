using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Extensions;

namespace MauiApp1
{
    public class BleManager
    {
        private static readonly Guid SERVICE_UUID = new Guid("0000fff0-0000-1000-8000-00805f9b34fb");
        private static readonly Guid WRITE_CHARACTERISTIC_UUID = new Guid("0000fff2-0000-1000-8000-00805f9b34fb");
        private static readonly Guid READ_CHARACTERISTIC_UUID = new Guid("0000fff1-0000-1000-8000-00805f9b34fb");

        private readonly IAdapter adapter = CrossBluetoothLE.Current.Adapter;
        private IDevice device;
        private ICharacteristic writeChar;
        private ICharacteristic readChar;
        private static bool isConnected = false;

        private readonly List<IDevice> foundDevices = new();

        public event Action<string> OnStatusUpdated;
        public event Action<string> OnMessageReceived;
        public event Action<List<IDevice>> OnDevicesUpdated;

        public IReadOnlyList<IDevice> FoundDevices => foundDevices.AsReadOnly();

        public async Task ScanAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Bluetooth>();

            if (status != PermissionStatus.Granted)
            {
                OnStatusUpdated?.Invoke("❌ BLE permission denied");
                return;
            }

            OnStatusUpdated?.Invoke("🔍 Scanning for BLE devices…");
            foundDevices.Clear();

            adapter.DeviceDiscovered += (s, a) =>
            {
                if (!string.IsNullOrWhiteSpace(a.Device.Name) && !foundDevices.Any(d => d.Id == a.Device.Id))
                {
                    foundDevices.Add(a.Device);
                    OnStatusUpdated?.Invoke($"🔍 Found: {a.Device.Name} ({a.Device.Id})");
                    OnDevicesUpdated?.Invoke(foundDevices);
                }
            };

            await adapter.StartScanningForDevicesAsync();
            await Task.Delay(TimeSpan.FromSeconds(10));
            await adapter.StopScanningForDevicesAsync();
        }


        public async Task ConnectToDeviceAsync(IDevice targetDevice)
        {
            try
            {
                device = targetDevice;
                isConnected = true; // Устанавливаем флаг, что устройство подключено

                OnStatusUpdated?.Invoke($"🔗 Connecting to {device.Name}…");
                await adapter.ConnectToDeviceAsync(device);

                var service = await device.GetServiceAsync(SERVICE_UUID);
                writeChar = await service.GetCharacteristicAsync(WRITE_CHARACTERISTIC_UUID);
                readChar = await service.GetCharacteristicAsync(READ_CHARACTERISTIC_UUID);

                if (readChar.CanUpdate)
                {
                    readChar.ValueUpdated += (s, e) =>
                    {
                        HandleReceivedData(e.Characteristic.Value);
                    };
                    await readChar.StartUpdatesAsync();
                }

                OnStatusUpdated?.Invoke("✅ Connected");
            }
            catch (Exception ex)
            {
                OnStatusUpdated?.Invoke("❌ Connection failed: " + ex.Message);
            }
        }


        public static UInt16 ModRTU_CRC(byte[] buf, int len)
        {
            UInt16 crc = 0xFFFF;

            for (int pos = 0; pos < len; pos++)
            {
                crc ^= (UInt16)buf[pos];          // XOR byte into least sig. byte of crc

                for (int i = 8; i != 0; i--)     // Loop over each bit
                {
                    if ((crc & 0x0001) != 0)     // If the LSB is set
                    {
                        crc >>= 1;               // Shift right and XOR 0xA001
                        crc ^= 0xA001;
                    }
                    else                         // Else LSB is not set
                        crc >>= 1;               // Just shift right
                }
            }

            // Return CRC with low and high bytes swapped
            return crc;
        }
        public async Task SendBytesAsync(byte[] data)
        {
            // Compute CRC and append it to the data
            UInt16 crc = ModRTU_CRC(data, data.Length);
            byte[] crcBytes = BitConverter.GetBytes(crc);
            byte[] dataWithCRC = data.Concat(crcBytes).ToArray(); // Appending CRC

            if (writeChar != null && writeChar.CanWrite)
            {
                await writeChar.WriteAsync(dataWithCRC);
                OnStatusUpdated?.Invoke($"📤 Sent: {BitConverter.ToString(dataWithCRC)}");
            }
            else
            {
                OnStatusUpdated?.Invoke("⚠️ Unable to send bytes");
            }
        }

        private void HandleReceivedData(byte[] rawData)
        {
            // Extract data and CRC from received message
            byte[] data = rawData.Take(rawData.Length - 2).ToArray(); // All bytes except last 2
            byte[] crcReceived = rawData.Skip(rawData.Length - 2).ToArray(); // Last 2 bytes are CRC

            // Calculate CRC for the received data
            UInt16 calculatedCRC = ModRTU_CRC(data, data.Length);
            UInt16 receivedCRC = BitConverter.ToUInt16(crcReceived, 0);

            if (calculatedCRC == receivedCRC)
            {
                string hex = BitConverter.ToString(data).Replace("-", " ");
                OnMessageReceived?.Invoke(hex); // Send data to the UI as hex string
            }
            else
            {
                OnStatusUpdated?.Invoke("❌ CRC mismatch in received data!");
            }
        }

        public async Task DisconnectAsync()
        {
            if (isConnected)
            {
                OnStatusUpdated?.Invoke($"🔌 Disconnecting from {device.Name}...");
                await adapter.DisconnectDeviceAsync(device);
                isConnected = false; // Обновляем состояние
                device = null;
                OnStatusUpdated?.Invoke("✅ Disconnected");
            }
            else
            {
                OnStatusUpdated?.Invoke("⚠️ No device to disconnect");
            }
        }



    }

}
