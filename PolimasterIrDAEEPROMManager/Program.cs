using InTheHand.Net;
using InTheHand.Net.Sockets;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace PolimasterIrDAEEPROMManager
{
    internal class Program
    {

        private static CancellationTokenSource _cancellationTokenSource = new();

        private static Dictionary<string, byte[]> CommunicationCommands = new()
        {
            { "SetAddress", new byte[] { 130, 0, 10, 177, 0, 114, 0, 5, 0, 0 } },
            { "ReadBytes", new byte[] { 131, 0, 5, 177, 156} },
            { "WriteBytes", new byte[] { 130, 0, 10, 177, 156, 114, 0, 5, 0, 0 } },
            { "Ok3", new byte[] { 160, 0, 3 } },
            { "Ok4", new byte[] { 160, 0, 8, 114, 0, 5 } },
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("PolimasterIrDAEEPROMManager is licensed under a modified version of the MIT License");
            Console.WriteLine("You should received a copy of the license with this project");

            Option<ushort> startArgument = new Option<ushort>("--start")
            {
                Description = "Operation's start address",
                DefaultValueFactory = parseResult => 0,
                Required = true,
            };
            startArgument.Aliases.Add("-s");

            Option<ushort> endArgument = new Option<ushort>("--end")
            {
                Description = "Operation's end address",
                DefaultValueFactory = parseResult => 1024,
                Required = true,
            };
            endArgument.Aliases.Add("-e");

            // Cant parse chars
            Option<string> operationArgument = new Option<string>("--operation")
            {
                Description = "Type of operation to execute, r for read, w for write, v for verify",
                DefaultValueFactory = parseResult => "r",
                Required = true,
            };
            operationArgument.Aliases.Add("-o");

            Option<FileInfo> fileArgument = new Option<FileInfo>("--file")
            {
                Description = "Output/input file",
                DefaultValueFactory = parseResult => new FileInfo("eeprom_dump.hex"),
                Required = true,
            };
            fileArgument.Aliases.Add("-f");

            RootCommand rootCommand = new("Utility for reading/writing/verifying EEPROM contents of the Polimaster's PM1703 and PM1401 series radiation pagers")
            {
                startArgument,
                endArgument,
                operationArgument,
                fileArgument,
            };

            ParseResult parseResult = rootCommand.Parse(args);
            await parseResult.InvokeAsync();

            if (parseResult.Action?.GetType() == typeof(System.CommandLine.Help.HelpAction))
            {
                return;
            }

            if (parseResult.Errors.Count != 0)
            {
                foreach (ParseError error in parseResult.Errors)
                {
                    Console.WriteLine("fail: Unable to parse options: {0}", error.Message);
                }
                Exit("Errors while parsing options", 29);
            }

            ushort start = parseResult.GetValue(startArgument);
            ushort end = parseResult.GetValue(endArgument);
            char operation = parseResult.GetValue(operationArgument).FirstOrDefault();
            string file = parseResult.GetValue(fileArgument).FullName;

            if ((operation != 'r') && (operation != 'w') && (operation != 'v')) Exit(string.Format("Unknown operation type: {0}", operation), 30);

            if (end <= start) Exit(string.Format("End address is smaller or equals to the start address"), 31);

            byte[] fileContents = Array.Empty<byte>();
            if ((operation == 'w') || (operation == 'v'))
            {
                try
                {
                    fileContents = File.ReadAllBytes(file);
                }
                catch (Exception e)
                {
                    Exit(string.Format("Unable to read file: {0}", e.Message), 32);
                }
            }

            AppDomain.CurrentDomain.ProcessExit += ProgramExitEvent;

            IrDAClient irDAClient = new IrDAClient();
            IrDADeviceInfo? foundDevice = null;
            Console.WriteLine("done: begin continuous scan for IrDA devices...");
            while (foundDevice == null)
            {
                foundDevice = DiscoverOneDevice(irDAClient);
                await Task.Delay(100);
            }
            Console.WriteLine("done: device found!");

            IrDADevice device = new IrDADevice(irDAClient, new IrDAEndPoint(foundDevice.DeviceAddress, foundDevice.DeviceName));

            switch (operation)
            {
                case 'r':
                    File.WriteAllText(file, string.Empty);
                    for (ushort i = start; (i < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); i += 2)
                    {
                        Console.Title = string.Format("reading [{0}/{1}]... ", i, end);
                        await SetAddress(device, i);
                        byte[] read = await ReadBytes(device);
                        await File.AppendAllBytesAsync(file, read, _cancellationTokenSource.Token);
                    }
                    Console.WriteLine("done: output to file {0}", file);
                    break;

                case 'w':
                    for (ushort i = start; (i < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); i += 2)
                    {
                        Console.Title = string.Format("writing [{0}/{1}]...", i, end);
                        await SetAddress(device, i);
                        await WriteBytes(device, fileContents[i], fileContents[i + 1]);
                    }
                    break;

                case 'v':
                    uint errCount = 0;
                    for (ushort i = start; (i < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); i += 2)
                    {
                        Console.Title = string.Format("verifying [{0}/{1}]...", i, end);
                        await SetAddress(device, i);
                        byte[] read = await ReadBytes(device);
                        if (read[0] != fileContents[i])
                        {
                            Console.WriteLine("fail: verification error: address {0}, expected {1}, got {2}", i, fileContents[i], read[0]);
                            errCount += 1;
                        }
                        if (read[1] != fileContents[i + 1])
                        {
                            Console.WriteLine("fail: verification error: address {0}, expected {1}, got {2}", i + 1, fileContents[i + 1], read[1]);
                            errCount += 1;
                        }
                    }
                    Console.WriteLine("done: verified: {0} errors", errCount);
                    break;

            }
            Console.Title = "done";
            Console.WriteLine("done: operation done");
            device.Close();
            device.Dispose();
        }

        private async static Task SetAddress(IrDADevice device, ushort address)
        {
            byte[] array = CommunicationCommands["SetAddress"].ToArray();
            array[8] = (byte)address;
            array[9] = (byte)(address >> 8);
            _ = await device.SendAndReceiveAndCheckAsync(array, CommunicationCommands["Ok3"], _cancellationTokenSource.Token);
        }

        private async static Task<byte[]> ReadBytes(IrDADevice device)
        {
            byte[] array = await device.SendAndReceiveAndCheckAsync(CommunicationCommands["ReadBytes"], CommunicationCommands["Ok4"], _cancellationTokenSource.Token);
            return new byte[2] { array[6], array[7] };
        }

        private async static Task WriteBytes(IrDADevice device, byte b1, byte b2)
        {
            byte[] array = CommunicationCommands["WriteBytes"].ToArray();
            array[8] = b1;
            array[9] = b2;
            _ = await device.SendAndReceiveAndCheckAsync(array, CommunicationCommands["Ok3"], _cancellationTokenSource.Token);
        }

        private static void ProgramExitEvent(object? sender, EventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }

        private static void Exit(string logMessage, int exitCode)
        {
            Console.WriteLine("fail: {0}", logMessage);
            Console.WriteLine("exiting...");
            Environment.Exit(exitCode);
        }

        private static IrDADeviceInfo? DiscoverOneDevice(IrDAClient client)
        {
            IrDADeviceInfo[] irdaDiscoveredInfo = client.DiscoverDevices(1);
            if (irdaDiscoveredInfo.Length == 0) return null;
            else return irdaDiscoveredInfo[0];
        }
    }
}
