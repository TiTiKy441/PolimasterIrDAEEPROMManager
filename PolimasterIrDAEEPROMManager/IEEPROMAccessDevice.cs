namespace PolimasterIrDAEEPROMManager
{
    internal interface IEEPROMAccessDevice : IDisposable
    {

        Task<byte[]> ReadBytesFromEEPROM(ushort address, CancellationToken token);

        Task WriteBytesToEEPROMAsync(ushort address, byte b1, byte b2, CancellationToken token);

    }
}
