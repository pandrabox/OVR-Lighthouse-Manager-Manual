﻿
using System.Runtime.InteropServices.WindowsRuntime;
using OVRLighthouseManager.Helpers;
using Serilog;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace OVRLighthouseManager.Models;

public class LighthouseDevice
{
    public enum DeviceType
    {
        Unknown,
        Lighthouse,
        NotLighthouse,
    };

    public string Name => _device?.Name ?? "(Unknown)";
    public ulong BluetoothAddress => _device?.BluetoothAddress ?? 0;

    public bool IsInitialized => _powerCharacteristic != null;

    public event EventHandler OnDisconnected = delegate { };

    private readonly BluetoothLEDevice _device;
    private GattDeviceService? _controlService;
    private GattCharacteristic? _powerCharacteristic;

    private static readonly Guid ControlService = new("00001523-1212-efde-1523-785feabcd124");
    private static readonly Guid PowerCharacteristic = new("00001525-1212-efde-1523-785feabcd124");

    private ILogger _log = LogHelper.ForContext<LighthouseDevice>();

    private LighthouseDevice(BluetoothLEDevice device)
    {
        _device = device;
        _device.ConnectionStatusChanged += (sender, args) =>
        {
            _log.Debug($"{Name} ({BluetoothAddress:X012}) Connection status changed: {sender.ConnectionStatus}");
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                _controlService = null;
                _powerCharacteristic = null;
                OnDisconnected(this, EventArgs.Empty);
            }
        };
    }

    internal static async Task<LighthouseDevice> FromBluetoothAddressAsync(ulong bluetoothAddress)
    {
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
        return new LighthouseDevice(device);
    }

    internal static async Task<LighthouseDevice> FromIdAsync(string deviceId)
    {
        var device = await BluetoothLEDevice.FromIdAsync(deviceId);
        return new LighthouseDevice(device);
    }

    public async Task<DeviceType> Identify()
    {
        const int retryCount = 5;
        if (_controlService == null)
        {
            GattDeviceServicesResult? result = null;
            for (var i = 0; i < retryCount; i++)
            {
                result = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                var shouldBreak = false;
                switch (result.Status)
                {
                    case GattCommunicationStatus.Success:
                        shouldBreak = true;
                        break;
                    case GattCommunicationStatus.ProtocolError:
                    case GattCommunicationStatus.AccessDenied:
                        Log.Information($"{Name} ({BluetoothAddress:X012}) Failed to get services: {result.Status}");
                        return DeviceType.NotLighthouse;
                    case GattCommunicationStatus.Unreachable:
                        Log.Information($"{Name} ({BluetoothAddress:X012}) Failed to get services: {result.Status}");
                        break;
                }
                if (shouldBreak)
                {
                    break;
                }
                await Task.Delay(100);
            }
            if (result?.Status == GattCommunicationStatus.Unreachable)
            {
                return DeviceType.Unknown;
            }
            _controlService = result?.Services.FirstOrDefault(s => s.Uuid == ControlService);
            if (_controlService == null)
            {
                return DeviceType.NotLighthouse;
            }
        }

        if (_powerCharacteristic == null)
        {
            GattCharacteristicsResult? result = null;
            for (var i = 0; i < retryCount; i++)
            {
                result = await _controlService?.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                var shouldBreak = false;
                switch (result.Status)
                {
                    case GattCommunicationStatus.Success:
                        shouldBreak = true;
                        break;
                    case GattCommunicationStatus.ProtocolError:
                    case GattCommunicationStatus.AccessDenied:
                        Log.Information($"{Name}  ( {BluetoothAddress:X012} ) Failed to get characteristics: {result.Status}");
                        return DeviceType.NotLighthouse;
                    case GattCommunicationStatus.Unreachable:
                        Log.Information($"{Name}  ( {BluetoothAddress:X012} ) Failed to get characteristics: {result.Status}");
                        break;
                }
                if (shouldBreak)
                {

                    break;
                }
                await Task.Delay(100);
            }
            if (result?.Status == GattCommunicationStatus.Unreachable)
            {
                return DeviceType.Unknown;
            }
            _powerCharacteristic = result?.Characteristics.FirstOrDefault(c => c.Uuid == PowerCharacteristic);
            if (_powerCharacteristic == null)
            {
                return DeviceType.NotLighthouse;
            }
        }

        return DeviceType.Lighthouse;
    }

    public async Task<bool> PowerOnAsync()
    {
        if (_powerCharacteristic == null)
        {
            throw new Exception("Power characteristic is null");
        }
        return await WriteCharacteristicAsync(_powerCharacteristic, 0x01);
    }

    public async Task<bool> SleepAsync()
    {
        if (_powerCharacteristic == null)
        {
            throw new Exception("Power characteristic is null");
        }
        return await WriteCharacteristicAsync(_powerCharacteristic, 0x00);
    }

    public async Task<bool> StandbyAsync()
    {
        if (_powerCharacteristic == null)
        {
            throw new Exception("Power characteristic is null");
        }
        return await WriteCharacteristicAsync(_powerCharacteristic, 0x02);
    }


    private async Task<bool> WriteCharacteristicAsync(GattCharacteristic characteristic, byte data)
    {
        const int retryCount = 5;
        for (var i = 0; i < retryCount; i++)
        {
            var result = await characteristic.WriteValueAsync(new byte[] { data }.AsBuffer());
            switch (result)
            {
                case GattCommunicationStatus.Success:
                    return true;
                case GattCommunicationStatus.Unreachable:
                    Log.Information($"{Name} ({BluetoothAddress:X012}) Failed to write characteristic: {result}");
                    continue;
                case GattCommunicationStatus.ProtocolError:
                case GattCommunicationStatus.AccessDenied:
                    Log.Information($"{Name} ({BluetoothAddress:X012}) Failed to write characteristic: {result}");
                    return false;
            }
            await Task.Delay(100);
        }
        return false;
    }
}
