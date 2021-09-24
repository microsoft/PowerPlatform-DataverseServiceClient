using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
    /// <summary>
    /// Config Mode to run this control in.
    /// </summary>
    public enum ServerLoginConfigCtrlMode
    {
        /// <summary>
        /// Hide the Cancel button and Show the Org Select UI. 
        /// </summary>
        ConfigPanel = 0,

        /// <summary>
        /// Show the Cancel button
        /// </summary>
        FullLoginPanel
    }

    /// <summary>
    /// Data Payload for the Connection status events
    /// </summary>
    public class ConnectStatusEventArgs : EventArgs
    {
        /// <summary>
        /// Flag indicating connection state. 
        /// </summary>
        public bool ConnectSucceeded { get; private set; }
        /// <summary>
        /// Status of the connection process
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bConnected"></param>
        public ConnectStatusEventArgs(bool bConnected)
        {
            ConnectSucceeded = bConnected;
        }

        /// <summary>
        /// 
        /// </summary>
        public ConnectStatusEventArgs()
        {
            ConnectSucceeded = false;
        }
    }

    /// <summary>
    /// Data Payload for the Connection Error Events
    /// </summary>
    public class ConnectErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; }
        /// <summary>
        /// Exception describing the Error
        /// </summary>
        public Exception Ex { get; set; }
    }
}
