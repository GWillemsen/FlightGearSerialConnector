﻿using System;
using System.Linq;
using System.Net.Sockets;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FlighGearSerialConnector
{
    class Program
    {
        static SerialPort port;
        static UdpClient sendingUdp;
        static UdpClient recievingUdp;
        static bool debug = false;
        static CancellationTokenSource cancellationToken;
        static Task MainRunner;

        static void Main(string[] args)
        {
            if (args.Contains("--help"))
            {
                PrintHelp();
                return;
            }
            if (!CreateResources(args))
                return;
            Console.WriteLine("Successfully created resources");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.ReadLine() == "quit")
                    cancellationToken.Cancel();
            }
            DestroyResources();
        }

        static async void RunMonolithicConnection()
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                if (port.BytesToRead > 0)
                {
                    byte[] buffer = new byte[port.BytesToRead];
                    var bytesRead = await port.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                    Console.Write(System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    await sendingUdp.SendAsync(buffer, bytesRead);
                }
                if (recievingUdp.Available > 0)
                {
                    var recieved = await recievingUdp.ReceiveAsync();
                    await port.BaseStream.WriteAsync(recieved.Buffer, 0, recieved.Buffer.Length);
                }
            }
        }

        static bool CreateResources(string[] arguments)
        {
            (bool success, string inIp, string outIp, int inPort, int outPort, string comName, int baudRate, bool debug) = LoadArguments(arguments);
            if (success)
            {
                Program.debug = debug;
                if (debug) Console.WriteLine("Creating UDP clients");
                recievingUdp = new UdpClient(inIp, inPort);
                sendingUdp = new UdpClient(outIp, outPort);
                if (debug) Console.WriteLine("Doing serial name check");
                if (!SerialPort.GetPortNames().Contains(comName))
                {
                    Console.WriteLine($"Could not find a serial port with the name '{comName}'");
                    return false;
                }
                else
                {
                    if (debug) Console.WriteLine("Creating and opening serial port");
                    port = new SerialPort(comName)
                    {
                        BaudRate = baudRate,
                    };
                    port.Open();

                    cancellationToken = new CancellationTokenSource();
                    MainRunner = Task.Factory.StartNew(RunMonolithicConnection, cancellationToken.Token);
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        static void DestroyResources()
        {
            MainRunner.Wait();
            sendingUdp.Close();
            sendingUdp.Dispose();
            recievingUdp.Close();
            recievingUdp.Dispose();
            port.Close();
            port.Dispose();
            cancellationToken.Dispose();
        }

        static void PrintHelp()
        {
            Console.WriteLine("--help                   Prints this help");
            Console.WriteLine("--debug                  Prints more verbose messages");
            Console.WriteLine("--udp-in-port=[number]   The port to listen to for data from FlightGear");
            Console.WriteLine("--udp-out-port=[number]  The port to send data on to FlightGear");
            Console.WriteLine("--udp-in-ip=[ip]         The port to send data on to FlightGear");
            Console.WriteLine("--udp-out-ip=[ip]        The port to send data on to FlightGear");
            Console.WriteLine("--com=[name]             The serial port name to listen to (ex. --com=COM19)");
            Console.WriteLine("--baud=[number]          The serial port baud rate to use (default 9600)");
        }

        static (bool success, string inIp, string outIp, int inPort, int outPort, string comName, int baud, bool debug) LoadArguments(string[] args)
        {
            bool debug = false;
            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg == "--debug")
                {
                    debug = true;
                }
            }
            (string comName, int baudRate, bool invalidInCom) = RetrieveComData(args);
            (int inPort, int outPort, bool invalidInPorts) = RetreiveInOutPorts(args);
            (string inIp, string outIp, bool invalidInIp) = RetreiveInOutIps(args);
            var invalidArgs = args.Where(arg => arg != "--debug"
                                                && arg != "--echo"
                                                && !arg.StartsWith("--baud=")
                                                && !arg.StartsWith("--com=")
                                                && !arg.StartsWith("--udp-in-port=")
                                                && !arg.StartsWith("--udp-out-port=")
                                                && !arg.StartsWith("--udp-in-ip=")
                                                && !arg.StartsWith("--udp-out-ip="));
            foreach (var argument in invalidArgs)
                Console.WriteLine("Unrecognized argument: " + argument);

            return (!invalidArgs.Any() && !invalidInIp && !invalidInPorts && !invalidInCom,
                inIp,
                outIp,
                inPort,
                outPort,
                comName,
                baudRate,
                debug);

        }

        private static (string comName, int baudRate, bool invalidCommandsFound) RetrieveComData(string[] args)
        {
            string comName = string.Empty;
            int baudRate = 9600;
            bool invalidCommand = false;
            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg.StartsWith("--com="))
                {
                    comName = arg[6..];
                }
                else if (arg.StartsWith("--baud="))
                {
                    if (!int.TryParse(arg[7..], out baudRate))
                    {
                        Console.WriteLine("Unrecognized value for the baud rate");
                        invalidCommand = true;
                    }
                }
            }
            return (comName, baudRate, invalidCommand);
        }

        /// <summary>
        /// Retrieves the input port and output port from the arguments list
        /// </summary>
        /// <param name="args">The argument list to search in</param>
        /// <returns>The input port, output port and if there where invalid commands found for these actions (IE double assignments)</returns>

        private static (int inPort, int outPort, bool invalidCommandsFound) RetreiveInOutPorts(string[] args)
        {
            int inPort = 0;
            int outPort = 0;
            bool doubleAssignInPort = false;
            bool doubleAssignOutPort = false;
            bool invalidCommands = false;
            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg.StartsWith("--udp-in-port="))
                {
                    if (inPort == 0)
                    {
                        if (!int.TryParse(arg[14..], out inPort))
                        {
                            Console.WriteLine("Unrecognized argument value for UDP input port: " + arg);
                            invalidCommands = true;
                        }
                    }
                    else
                    {
                        doubleAssignInPort = true;
                    }
                }
                else if (arg.StartsWith("--udp-out-port="))
                {
                    if (outPort == 0)
                    {
                        if (!int.TryParse(arg[15..], out outPort))
                        {
                            Console.WriteLine("Unrecognized argument value for UDP output port: " + arg);
                            invalidCommands = true;
                        }
                    }
                    else
                    {
                        doubleAssignOutPort = true;
                    }
                }
            }
            if (doubleAssignInPort)
            {
                Console.WriteLine("--udp-in-port can only be assigned once");
                invalidCommands = true;
            }
            if (doubleAssignOutPort)
            {
                Console.WriteLine("--udp-out-port can only be assigned once");
                invalidCommands = true;
            }
            return (inPort, outPort, invalidCommands);
        }

        /// <summary>
        /// Retrieves the input IP and output IP from the arguments list
        /// </summary>
        /// <param name="args">The argument list to search in</param>
        /// <returns>The input IP, output IP and if there where invalid commands found for these actions (IE double assignments)</returns>
        private static (string inIp, string outIp, bool invalidCommandsFound) RetreiveInOutIps(string[] args)
        {
            string inIp = string.Empty;
            string outIp = string.Empty;
            bool doubleAssignInIp = false;
            bool doubleAssignOutIp = false;
            bool invalidCommands = false;

            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg.StartsWith("--udp-in-ip="))
                {
                    if (inIp == string.Empty)
                        inIp = arg[12..];
                    else
                        doubleAssignInIp = true;
                }
                else if (arg.StartsWith("--udp-out-ip="))
                {
                    if (outIp == string.Empty)
                        outIp = arg[13..];
                    else
                        doubleAssignOutIp = true;
                }
            }
            if (doubleAssignInIp)
            {
                Console.WriteLine("--udp-in-ip can only be assigned once");
                invalidCommands = true;
            }
            if (doubleAssignOutIp)
            {
                Console.WriteLine("--udp-out-ip can only be assigned once");
                invalidCommands = true;
            }
            return (inIp, outIp, invalidCommands);
        }
    }
}
