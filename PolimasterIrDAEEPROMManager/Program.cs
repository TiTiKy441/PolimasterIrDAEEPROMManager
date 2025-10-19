using InTheHand.Net;
using InTheHand.Net.Sockets;
using System.CommandLine;
using System.IO;

namespace PolimasterIrDAEEPROMManager
{
    internal class Program
    {

        private readonly static CancellationTokenSource _cancellationTokenSource = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("PolimasterIrDAEEPROMManager is licensed under a modified version of the MIT License");
            Console.WriteLine("You should received a copy of the license with this project");

            RootCommand rootCommand = GetRootCommand();
            
            ParseResult parseResult = rootCommand.Parse(args);
            await parseResult.InvokeAsync();

            // Help command or error; do not continue
            if ((parseResult.Action?.GetType() == typeof(System.CommandLine.Help.HelpAction)) || parseResult.Errors.Count > 0)
            {
                return;
            }

            ushort start = parseResult.GetValue<ushort>("--start");
            ushort end = parseResult.GetValue<ushort>("--end");
            Operation operation = parseResult.GetValue<Operation>("--operation");
            FileInfo? file = parseResult.GetValue<FileInfo>("--file");

            // (Redundant) check.
            if ((file is null))
            {
                return;
            }

            Console.WriteLine("start address: {0} ; end address: {1} ; operation: {2} ; file: {3}", start, end, GetOperationAsString(operation), file.Name);

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
            try
            {
                using (DeviceWithMemoryAccess device = new DeviceWithMemoryAccess(irDAClient, new IrDAEndPoint(foundDevice.DeviceAddress, foundDevice.DeviceName)))
                {
                    switch (operation)
                    {
                        case Operation.Read:
                            using (FileStream stream = file.OpenWrite())
                            {
                                stream.SetLength(0);
                                for (ushort i = start; (i < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); i += 2)
                                {
                                    Console.Title = string.Format("reading [{0}/{1}]... ", i, end);
                                    byte[] read1 = await device.ReadBytesFromEEPROM(i, _cancellationTokenSource.Token);
                                    await stream.WriteAsync(read1, 0, read1.Length);
                                    await stream.FlushAsync();
                                }
                                Console.WriteLine("done: output to file {0}", file);
                            }
                            break;

                        case Operation.Write:
                            byte[] read2 = new byte[end - start];
                            using (FileStream stream = file.OpenRead())
                            {
                                await stream.WriteAsync(read2, _cancellationTokenSource.Token);
                            }

                            for (ushort memAddr = start, i = 0; (memAddr < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); memAddr += 2, i += 2)
                            {
                                Console.Title = string.Format("writing [{0}/{1}]...", i, end - start);
                                await device.WriteBytesToEEPROM(memAddr, read2[i], read2[i + 1], _cancellationTokenSource.Token);
                            }
                            break;

                        case Operation.Verify:
                            byte[] read3 = new byte[end - start];
                            using (FileStream stream = file.OpenRead())
                            {
                                await stream.ReadExactlyAsync(read3, _cancellationTokenSource.Token);
                            }

                            uint errCount = 0;
                            for (ushort memAddr = start, i = 0; (memAddr < end) && (!_cancellationTokenSource.Token.IsCancellationRequested); memAddr += 2, i += 2)
                            {
                                Console.Title = string.Format("verifying [{0}/{1}]...", i, end - start);
                                byte[] mem = await device.ReadBytesFromEEPROM(memAddr, _cancellationTokenSource.Token);
                                if (mem[0] != read3[i])
                                {
                                    Console.WriteLine("fail: verification error: address {0}, expected {1}, got {2}", memAddr, read3[i], mem[0]);
                                    errCount += 1;
                                }
                                if (mem[1] != read3[i + 1])
                                {
                                    Console.WriteLine("fail: verification error: address {0}, expected {1}, got {2}", memAddr + 1, read3[i + 1], mem[0 + 1]);
                                    errCount += 1;
                                }
                            }
                            break;
                    }
                }
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("fail: operation failed: {0}", e.Message);
            }
            
            Console.Title = "done";
            Console.WriteLine("done: operation executed");
        }


        private static void ProgramExitEvent(object? sender, EventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }

        private static IrDADeviceInfo? DiscoverOneDevice(IrDAClient client)
        {
            IrDADeviceInfo[] irdaDiscoveredInfo = client.DiscoverDevices(1);
            if (irdaDiscoveredInfo.Length == 0) return null;
            else return irdaDiscoveredInfo[0];
        }

        /// <summary>
        /// Generates a new root command with all options, validators and parsers already added
        /// </summary>
        /// <returns>Generated root command</returns>
        private static RootCommand GetRootCommand()
        {
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
            endArgument.Validators.Add(result =>
            {
                try
                {
                    ushort sArg = result.GetValue(startArgument);
                    ushort eArg = result.GetValue(endArgument);
                    if (eArg <= sArg)
                    {
                        result.AddError("Must be bigger than start address");
                    }
                }catch(Exception e)
                {
                    result.AddError(string.Format("Exception while validating: {0}", e.Message));
                }
            });
            endArgument.Aliases.Add("-e");

            Option<Operation> operationArgument = new Option<Operation>("--operation")
            {
                Description = "Type of operation to execute, r for read, w for write, v for verify",
                DefaultValueFactory = parseResult => Operation.Read,
                Required = true,
                CustomParser = result =>
                {
                    if ((result.Tokens.Count != 1) && (result.Tokens[0].Value.Length != 1))
                    {
                        result.AddError("Must be single character");  
                    };
                    char v = result.Tokens[0].Value[0];
                    Operation? op = v switch
                    {
                        'r' => Operation.Read,
                        'w' => Operation.Write,
                        'v' => Operation.Verify,
                        _ => null
                    };
                    if (op is null)
                    {
                        result.AddError("Not a valid operation");
                        return 0;
                    }
                    return (Operation)op;
                },
            };
            operationArgument.Aliases.Add("-o");

            Option<FileInfo> fileArgument = new Option<FileInfo>("--file")
            {
                Description = "Output/input file",
                DefaultValueFactory = parseResult => new FileInfo("eeprom_dump.hex"),
                Required = true,
            };
            fileArgument.Validators.Add(result =>
            {
                try
                {
                    FileInfo? file = result.GetValue(fileArgument);
                    Operation op = result.GetValue(operationArgument);
                    if (file is null)
                    {
                        result.AddError("Must be a file");
                        return;
                    }
                    if (((op == Operation.Write) || (op == Operation.Verify)) && (!file.Exists))
                    {
                        result.AddError("File must exist for this type of operation");
                    }
                    if ((op == Operation.Read) && file.IsReadOnly)
                    {
                        result.AddError("File must be writable for this type of operation");
                    }
                }
                catch(Exception e)
                {
                    result.AddError(string.Format("Unable to validate: {0}", e.Message));
                }
            });
            fileArgument.Aliases.Add("-f");

            RootCommand rootCommand = new("Utility for reading/writing/verifying EEPROM contents of the Polimaster's PM1703 and PM1401 series radiation pagers")
            {
                startArgument,
                endArgument,
                operationArgument,
                fileArgument,
            };

            return rootCommand;
        }

        private static string GetOperationAsString(Operation op)
        {
            return op switch
            {
                Operation.Read => "read",
                Operation.Write => "write",
                Operation.Verify => "verify",
                _ => throw new ArgumentException()
            };
        }

        /// <summary>
        /// Type of operation to execute
        /// </summary>
        public enum Operation
        {
            Read,
            Write,
            Verify,
        }
    }
}
