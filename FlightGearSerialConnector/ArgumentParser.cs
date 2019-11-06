using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlighGearSerialConnector
{
    /// <summary>
    /// Some helpers to parse the argument list
    /// </summary>
    internal static class ArgumentParser
    {
        /// <summary>
        /// Parse all arguments from the argument list
        /// </summary>
        /// <param name="args">The argument list to search in</param>
        /// <returns>The loaded arguments</returns>
        public static (bool success, string outIp, int inPort, int outPort, string comName, int baud, bool debug, bool copypast) LoadArguments(string[] args)
        {
            bool debug = false;
            bool copyPast = false;
            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg == "--debug")
                    debug = true;
                else if (arg == "--copypast")
                    copyPast = true;
            }
            (string comName, int baudRate, bool invalidInCom) = RetrieveComData(args);
            (int inPort, int outPort, bool invalidInPorts) = RetreiveInOutPorts(args);
            (string outIp, bool invalidInIp) = RetreiveInOutIps(args);
            var invalidArgs = args.Where(arg => arg != "--debug"
                                                && arg != "--copypast"
                                                && !arg.StartsWith("--baud=", StringComparison.InvariantCultureIgnoreCase)
                                                && !arg.StartsWith("--com=", StringComparison.InvariantCultureIgnoreCase)
                                                && !arg.StartsWith("--udp-in-port=", StringComparison.InvariantCultureIgnoreCase)
                                                && !arg.StartsWith("--udp-out-port=", StringComparison.InvariantCultureIgnoreCase)
                                                && !arg.StartsWith("--udp-out-ip=", StringComparison.InvariantCultureIgnoreCase));
            foreach (var argument in invalidArgs)
                Console.WriteLine("Unrecognized argument: " + argument);

            return (!invalidArgs.Any() && !invalidInIp && !invalidInPorts && !invalidInCom,
                outIp,
                inPort,
                outPort,
                comName,
                baudRate,
                debug,
                copyPast);
        }

        /// <summary>
        /// Retrieves the com port name and the baud rate to use from the argument list
        /// </summary>
        /// <param name="args">The argument list to search in</param>
        /// <returns>The com port name, the baud rate and if there where invalid commands found for these arguments (i.e. double assignments)</returns>
        private static (string comName, int baudRate, bool invalidCommandsFound) RetrieveComData(string[] args)
        {
            string comName = string.Empty;
            int baudRate = 9600;
            bool invalidCommand = false;
            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg.StartsWith("--com=", StringComparison.InvariantCultureIgnoreCase))
                {
                    comName = arg[6..];
                }
                else if (arg.StartsWith("--baud=", StringComparison.InvariantCultureIgnoreCase))
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
                if (arg.StartsWith("--udp-in-port=", StringComparison.InvariantCultureIgnoreCase))
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
                else if (arg.StartsWith("--udp-out-port=", StringComparison.InvariantCultureIgnoreCase))
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
        private static (string outIp, bool invalidCommandsFound) RetreiveInOutIps(string[] args)
        {
            string outIp = string.Empty;
            bool doubleAssignOutIp = false;
            bool invalidCommands = false;

            foreach (var argument in args)
            {
                var arg = argument.Trim();
                if (arg.StartsWith("--udp-out-ip=", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(outIp))
                        outIp = arg[13..];
                    else
                        doubleAssignOutIp = true;
                }
            }
            if (doubleAssignOutIp)
            {
                Console.WriteLine("--udp-out-ip can only be assigned once");
                invalidCommands = true;
            }
            return (outIp, invalidCommands);
        }
    }
}
