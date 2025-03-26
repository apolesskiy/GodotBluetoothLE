using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE
{
    public class Service : ServiceBase<Linux.Bluetooth.IGattService1>
    {
        public Service(IDevice device, IGattService1 nativeService) : base(device, nativeService)
        {
        }

        public override Guid Id => throw new NotImplementedException();

        public override bool IsPrimary => throw new NotImplementedException();

        protected override Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync()
        {
            throw new NotImplementedException();
        }
    }
}