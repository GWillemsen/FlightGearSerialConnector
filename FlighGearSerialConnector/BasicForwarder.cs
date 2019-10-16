using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlighGearSerialConnector
{
    /// <summary>
    /// A forwarder that just copy pastes all incomming and outgoing data
    /// </summary>
    internal sealed class BasicForwarder : IForwarder
    {
        private readonly SerialPort port;
        private readonly UdpClient writingUdp;
        private readonly UdpClient readingUdp;
        private readonly CancellationToken cancellationToken;
        private Task udpReader;
        private Task udpWriter;

        /// <summary>
        /// Creates a new <see cref="BasicForwarder"/>
        /// </summary>
        /// <param name="port">The serial port to use</param>
        /// <param name="sending">The UDP client to use to send to FlightGear</param>
        /// <param name="recieving">The UDP client to use to retrieve data from FlightGear</param>
        /// <param name="token">The token to check for stopping the forwarder</param>
        public BasicForwarder(SerialPort port, UdpClient sending, UdpClient recieving, CancellationToken token)
        {
            this.port = port;
            writingUdp = sending;
            readingUdp = recieving;
            cancellationToken = token;
        }

        /// <summary>
        /// Wait for the forwarder to stop all reading and writing
        /// </summary>
        /// <returns>The task representing the waiting</returns>
        public async Task WaitForStopAsync()
        {
            if (Program.Debug) Console.WriteLine("Awaiting basic forwarder read task");
            await udpReader.ConfigureAwait(false);
            if (Program.Debug) Console.WriteLine("Awaiting basic forwarder write task");
            await udpWriter.ConfigureAwait(false);
        }

        /// <summary>
        /// Starts the forwarder to process messages
        /// </summary>
        public void Start()
        {
            if (Program.Debug) Console.WriteLine("Starting basic forwarder read and write tasks");

            // the task that is busy reading the serial and copying it to UDP
            udpWriter = Task.Run(UdpWriterTask);

            // the tasks that read incoming UDP messages and puts them on the serial connection
            udpReader = Task.Run(UpdReaderTask);
        }

        /// <summary>
        /// The worker that processes the incoming serial data
        /// </summary>
        private async void UdpWriterTask()
        {
            byte[] startBuf = new byte[1];
            while (!cancellationToken.IsCancellationRequested)
            {
                if (port.IsOpen)
                {
                    await port.BaseStream.ReadAsync(startBuf, 0, 1, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    if (port.IsOpen)
                    {
                        int toRead = port.BytesToRead;
                        byte[] extraBuf = new byte[1 + toRead];
                        extraBuf[0] = startBuf[0];
                        if (toRead > 0)
                        {
                            if (port.IsOpen)
                            {
                                toRead = (await port.BaseStream.ReadAsync(extraBuf, 1, toRead, cancellationToken).ConfigureAwait(false)) + 1;
                            }
                        }
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        await writingUdp.SendAsync(extraBuf, extraBuf.Length).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// The worker that processes the incoming UDP data
        /// </summary>
        private async void UpdReaderTask()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var data = await readingUdp.ReceiveAsync().ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    break;
                if (data.Buffer.Length > 0 && port.IsOpen && !cancellationToken.IsCancellationRequested)
                {
                    await port.BaseStream.WriteAsync(data.Buffer, 0, data.Buffer.Length).ConfigureAwait(false);
                }
            }
        }
    }
}
