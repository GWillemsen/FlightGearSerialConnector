using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FlighGearSerialConnector
{
    /// <summary>
    /// Basis for a forwarder
    /// </summary>
    public interface IForwarder
    {
        /// <summary>
        /// Wait for the forwarder to stop all reading and writing
        /// </summary>
        /// <returns>The task representing the waiting</returns>
        Task WaitForStopAsync();

        /// <summary>
        /// Starts the forwarder to process messages
        /// </summary>
        void Start();
    }
}
