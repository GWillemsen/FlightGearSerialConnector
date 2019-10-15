using System;
using System.Linq;
using System.Net.Sockets;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using static FlighGearSerialConnector.ArgumentParser;

namespace FlighGearSerialConnector
{
    class Program
    {
        static SerialPort port;
        static UdpClient sendingUdp;
        static UdpClient recievingUdp;
        public static bool debug = false;
        static bool copypastForwarder = false;
        static CancellationTokenSource cancellationToken;
        static IForwarder forwarder;

        /// <summary>
        /// The programs entry point
        /// </summary>
        /// <param name="args">The programs argument list</param>
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

        /// <summary>
        /// Use the arguments to create the forwarder and set the other options
        /// </summary>
        /// <param name="arguments">The argument list to use</param>
        /// <returns>If it was a success or not</returns>
        static bool CreateResources(string[] arguments)
        {
            (bool success, string outIp, int inPort, int outPort, string comName, int baudRate, bool debug, bool copyPast) = LoadArguments(arguments);
            if (success)
            {
                copypastForwarder = copyPast;
                Program.debug = debug;
                if (debug) Console.WriteLine("Creating UDP clients");
                recievingUdp = new UdpClient(inPort);
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
                    if (copypastForwarder)
                        forwarder = new BasicForwarder(port, sendingUdp, recievingUdp, cancellationToken.Token);
                    else
                        forwarder = new SmartForwarder(port, sendingUdp, recievingUdp, cancellationToken.Token);
                    forwarder.Start();
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Dispose and close all resources
        /// </summary>
        static void DestroyResources()
        {
            if (!cancellationToken.IsCancellationRequested)
                cancellationToken.Cancel();
            forwarder.WaitForStopAsync().Wait();
            sendingUdp.Close();
            sendingUdp.Dispose();
            recievingUdp.Close();
            recievingUdp.Dispose();
            port.Close();
            port.Dispose();
            cancellationToken.Dispose();
        }

        /// <summary>
        /// Prints the help to the consolse
        /// </summary>
        static void PrintHelp()
        {
            Console.WriteLine("--help                   Prints this help");
            Console.WriteLine("--debug                  Prints more verbose messages");
            Console.WriteLine("--udp-in-port=[number]   The port to listen to for data from FlightGear");
            Console.WriteLine("--udp-out-port=[number]  The port to send data on to FlightGear");
            Console.WriteLine("--udp-out-ip=[ip]        The IP to send data on to FlightGear");
            Console.WriteLine("--com=[name]             The serial port name to listen to (ex. --com=COM19)");
            Console.WriteLine("--baud=[number]          The serial port baud rate to use (default 9600)");
            Console.WriteLine("--copypast               Use the serial forwarder that just copy pastes the data instead of check it for changes and only then copying it");
        }
    }
}
