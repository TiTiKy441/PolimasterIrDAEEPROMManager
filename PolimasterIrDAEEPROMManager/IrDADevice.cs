using InTheHand.Net;
using InTheHand.Net.Sockets;
using System.Net.Sockets;

namespace PolimasterIrDAEEPROMManager
{
    internal class IrDADevice : IDisposable
    {

        public bool Disposed { get; private set; } = false;

        public readonly IrDAClient IrDAClient;

        public NetworkStream? IrDAStream { get; private set; } = null;

        public readonly IrDAEndPoint DeviceEndPoint;

        private SemaphoreSlim _IOSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Wait for response for this amount of milliseconds
        /// </summary>
        public static int ResponseTimeout = 500;

        /// <summary>
        /// Attempt to resend data this amount of times
        /// </summary>
        public static int ResendAttempts = 2;

        public static bool PrintDebugInfo = false;

        public IrDADevice(IrDAClient irdaClient, IrDAEndPoint endpoint)
        {
            IrDAClient = irdaClient;
            DeviceEndPoint = endpoint;

            Task.Run(async () =>
            {
                while (!Disposed)
                {
                    try
                    {
                        ConnectIfNotConnected();
                    }
                    catch (Exception)
                    {
                    }
                    await Task.Delay(100);
                }
            });
        }

        protected async Task<byte[]> SendAndReceiveAsync(byte[] send, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            /**
             * Note to all future edits: async here doesnt work with the IrDAClient.Client, only through the stream
             * Or at least it didnt work for me
             **/
            try
            {
                if (PrintDebugInfo) Console.Write("P");
                await _IOSemaphore.WaitAsync(cancellationToken); // One operation at a time so we are waiting until we are cleared to use

                if (PrintDebugInfo) Console.Write("! C");
                while (!IrDAClient.Connected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(1);
                }

                if (PrintDebugInfo) Console.Write("! F");
                while (IrDAStream.DataAvailable) // Flush the stream if it already had any garbage data in it
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = IrDAStream.ReadByte();
                }
                if (PrintDebugInfo) Console.Write("! S");

                await IrDAStream.WriteAsync(send, cancellationToken); // Write our data

                if (PrintDebugInfo) Console.Write("! W");
                List<byte> receive = new List<byte>();
                int elapsed = 0;
                int resendAttempt = 0;
                while (!IrDAStream.DataAvailable) // Waiting for the response (or for the cancelling), for me takes like 130 ms
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(1); // DataAvailable is slow, so the delay here works very good at reducing CPU usage
                    elapsed++;
                    if (elapsed >= ResponseTimeout)
                    {
                        if (resendAttempt == ResendAttempts)
                        {
                            throw new TimeoutException(string.Format("No response from device after {0} attemps", resendAttempt));
                        }
                        elapsed = 0;
                        resendAttempt++;
                        if (PrintDebugInfo) Console.Write("! S");
                        await IrDAStream.WriteAsync(send, cancellationToken);
                        if (PrintDebugInfo) Console.Write("!({0}) W", resendAttempt);
                    }
                }
                if (PrintDebugInfo) Console.Write("! R");

                while (IrDAStream.DataAvailable) // Reading data byte by byte
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    receive.Add((byte)IrDAStream.ReadByte());
                }
                if (PrintDebugInfo) Console.Write("!({0})", receive.Count);

                return receive.ToArray();
            }
            finally
            {
                if (PrintDebugInfo) Console.WriteLine();
                _IOSemaphore.Release();
            }
        }

        protected async Task<byte[]> SendAndReceiveAndCheckAsync(byte[] send, byte[] check, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            byte[] bytes = await SendAndReceiveAsync(send, cancellationToken);
            if (!CheckResult(bytes, check))
            {
                throw new InvalidDataException("Result check failed");
            }
            return bytes;
        }

        public static bool CheckResult(byte[] bytes, byte[] check)
        {
            for (int i = 0; i < check.Length; i++)
            {
                if (bytes[i] != check[i]) return false;
            }
            return true;
        }

        public virtual void ConnectIfNotConnected()
        {
            ThrowIfDisposed();
            if (!IrDAClient.Connected)
            {
                IrDAClient.Connect(DeviceEndPoint);
                IrDAStream = IrDAClient.GetStream();
            }
            if ((IrDAStream == null) || (!IrDAStream.Socket.Connected))
            {
                IrDAStream?.Dispose();
                IrDAStream = IrDAClient.GetStream();
            }
        }

        public void Close()
        {
            ThrowIfDisposed();
            if (IrDAStream != null)
            {
                IrDAStream.Close();
            }
            if (IrDAClient.Connected)
            {
                IrDAClient.Close();
            }
        }

        ~IrDADevice()
        {
            if (!Disposed) Dispose(false);
        }

        private void ThrowIfDisposed()
        {
            if (Disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (IrDAStream != null)
            {
                IrDAStream.Close();
                IrDAStream.Dispose();
            }

            if (IrDAClient.Connected)
            {
                IrDAClient.Client.Disconnect(false);
            }

            IrDAClient.Close();
            IrDAClient.Dispose();

            Disposed = true;
        }
    }
}
