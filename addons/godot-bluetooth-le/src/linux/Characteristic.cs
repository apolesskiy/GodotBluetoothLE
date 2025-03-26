using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Plugin.BLE
{
    public class Characteristic : CharacteristicBase<Linux.Bluetooth.GattCharacteristic>
    {
        public Characteristic(IService service, GattCharacteristic nativeCharacteristic) : base(service, nativeCharacteristic)
        {
        }

        public override Guid Id => throw new NotImplementedException();

        public override string Uuid => throw new NotImplementedException();

        public override byte[] Value => throw new NotImplementedException();

        public override CharacteristicPropertyType Properties => throw new NotImplementedException();

        public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        protected override Task<IReadOnlyList<IDescriptor>> GetDescriptorsNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task<(byte[] data, int resultCode)> ReadNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task StartUpdatesNativeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        protected override Task StopUpdatesNativeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        protected override Task<int> WriteNativeAsync(byte[] data, CharacteristicWriteType writeType)
        {
            throw new NotImplementedException();
        }
    }
}