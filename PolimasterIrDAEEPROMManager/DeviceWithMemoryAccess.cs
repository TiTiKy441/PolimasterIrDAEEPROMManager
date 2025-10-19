using InTheHand.Net;
using InTheHand.Net.Sockets;

namespace PolimasterIrDAEEPROMManager
{
    internal class DeviceWithMemoryAccess : IrDADevice
    {

        private readonly static Dictionary<string, byte[]> _communicationCommands = new()
        {
            { "SetAddress", new byte[] { 130, 0, 10, 177, 0, 114, 0, 5, 0, 0 } },
            { "ReadBytes", new byte[] { 131, 0, 5, 177, 156} },
            { "WriteBytes", new byte[] { 130, 0, 10, 177, 156, 114, 0, 5, 0, 0 } },
            { "Ok3", new byte[] { 160, 0, 3 } },
            { "Ok4", new byte[] { 160, 0, 8, 114, 0, 5 } },
        };

        public DeviceWithMemoryAccess(IrDAClient irdaClient, IrDAEndPoint endpoint) : base(irdaClient, endpoint)
        {
        }

        private async Task SetAddress(ushort address, CancellationToken token)
        {
            byte[] array = _communicationCommands["SetAddress"].ToArray();
            array[8] = (byte)address;
            array[9] = (byte)(address >> 8);
            _ = await SendAndReceiveAndCheckAsync(array, _communicationCommands["Ok3"], token);
        }

        private async Task<byte[]> ReadBytes(CancellationToken token)
        {
            byte[] array = await SendAndReceiveAndCheckAsync(_communicationCommands["ReadBytes"], _communicationCommands["Ok4"], token);
            return new byte[2] { array[6], array[7] };
        }

        private async Task WriteBytes(byte b1, byte b2, CancellationToken token)
        {
            byte[] array = _communicationCommands["WriteBytes"].ToArray();
            array[8] = b1;
            array[9] = b2;
            _ = await SendAndReceiveAndCheckAsync(array, _communicationCommands["Ok3"], token);
        }

        public async Task<byte[]> ReadBytesFromEEPROM(ushort address, CancellationToken token)
        {
            await SetAddress(address, token);
            return await ReadBytes(token);
        }

        public async Task WriteBytesToEEPROM(ushort address, byte b1, byte b2, CancellationToken token)
        {
            await SetAddress(address, token);
            await WriteBytes(b1, b2, token);
        }
    }
}
