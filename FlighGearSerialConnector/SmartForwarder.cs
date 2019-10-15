using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FlighGearSerialConnector
{
    public class SmartForwarder
    {
        private readonly SerialPort port;
        private readonly UdpClient writingUdp;
        private readonly UdpClient readingUdp;
        private readonly CancellationToken cancellationToken;
        private Task udpReader;
        private Task udpWriter;
        private readonly Dictionary<int, string> toSerialData = new Dictionary<int, string>();
        private readonly Dictionary<int, string> fromSerialData = new Dictionary<int, string>();

        public SmartForwarder(SerialPort port, UdpClient sending, UdpClient recieving, CancellationToken token)
        {
            this.port = port;
            writingUdp = sending;
            readingUdp = recieving;
            cancellationToken = token;
        }

        /// <summary>
        /// Start the task to read and write from/to the serial to/from FlightGear
        /// </summary>
        public void Start()
        {
            udpReader = Task.Run(UdpReaderWoker);
            udpWriter = Task.Run(UdpWriterWorker);
        }

        /// <summary>
        /// Await the task from the reader and writer jobs
        /// </summary>
        /// <returns>The task representing the awaiting of the jobs</returns>
        public async Task AwaitStop()
        {
            await udpReader.ConfigureAwait(false);
            await udpWriter.ConfigureAwait(false);
        }

        private async void UdpReaderWoker()
        {
            if (Program.debug) System.Console.WriteLine("UdpReader (serial writer) is started");
            while (!cancellationToken.IsCancellationRequested)
            {
                var lineData = await readingUdp.ReceiveAsync().ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested)
                {
                    string data = System.Text.Encoding.ASCII.GetString(lineData.Buffer);
                    string[] parts = data.Split(",");
                    bool hasChange = false;
                    for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                    {
                        if (!toSerialData.ContainsKey(partIndex))
                            toSerialData.Add(partIndex, string.Empty);
                        if (toSerialData[partIndex] != parts[partIndex])
                        {
                            toSerialData[partIndex] = parts[partIndex];
                            hasChange = true;
                        }
                    }
                    if (hasChange)
                    {
                        if (Program.debug) System.Console.WriteLine("The output from FlightGear has changes. Updating to serial. Data: " + data);
                        await port.BaseStream.WriteAsync(lineData.Buffer).ConfigureAwait(false);
                    }
                }
            }
        }

        private async void UdpWriterWorker()
        {
            byte[] startBuf = new byte[1];
            string serialData = "";
            bool firstRound = true;
            if (Program.debug) System.Console.WriteLine("UdpWriter (serial reader) is started");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (port.IsOpen)
                {
                    var lineData = await port.BaseStream.ReadAsync(startBuf, 0, 1).ConfigureAwait(false);
                    if (lineData > 0 && port.IsOpen)
                    {
                        int toRead = port.BytesToRead;

                        // allocation free buffer
                        byte[] readBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(1 + toRead);
                        readBuf[0] = startBuf[0];
                        int bytesInBuffer = 0;
                        if (toRead > 1)
                        {
                            // either read all data from the serial port, of until the buffer is full
                            bytesInBuffer = await port.BaseStream.ReadAsync(readBuf, 1, readBuf.Length - 1).ConfigureAwait(false);
                        }
                        serialData += port.Encoding.GetString(readBuf, 0, bytesInBuffer + 1);

                        if (DecodeSerialData(ref serialData, firstRound))
                        {
                            string newDataStr = string.Join(",", fromSerialData.Values) + "\n";
                            if (Program.debug) System.Console.WriteLine("The output from serial has changes. Updating to FlightGear. Data: " + newDataStr);
                            byte[] newData = System.Text.Encoding.ASCII.GetBytes(newDataStr);
                            await writingUdp.SendAsync(newData, newData.Length).ConfigureAwait(false);
                        }
                        firstRound = false;

                        //return the buffer
                        System.Buffers.ArrayPool<byte>.Shared.Return(readBuf, true);
                    }
                }
            }
        }

        private bool DecodeSerialData(ref string serialData, bool firstRound)
        {
            int lineEnding = serialData.IndexOf('\n');
            bool hasChange = false;
            while (lineEnding > -1)
            {
                string partData = serialData.Substring(0, lineEnding);
                serialData = serialData.Remove(0, lineEnding + 1);
                if (!firstRound)
                {
                    string[] parts = partData.Split(",");
                    for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                    {
                        if (!fromSerialData.ContainsKey(partIndex))
                            fromSerialData.Add(partIndex, "");
                        if (fromSerialData[partIndex] != parts[partIndex])
                        {
                            fromSerialData[partIndex] = parts[partIndex];
                            hasChange = true;
                        }
                    }
                }
                lineEnding = serialData.IndexOf('\n');
            }
            return hasChange;
        }
    }
}
