
using System;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE
{
    public class Descriptor : DescriptorBase<Linux.Bluetooth.IGattDescriptor1>
    {
        public Descriptor(ICharacteristic characteristic, IGattDescriptor1 nativeDescriptor) : base(characteristic, nativeDescriptor)
        {
        }

        public override Guid Id => throw new NotImplementedException();

        public override byte[] Value => throw new NotImplementedException();

        protected override Task<byte[]> ReadNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task WriteNativeAsync(byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}