// Ignore Spelling: Dataverse cmdlet

using System.Management.Automation;

namespace Microsoft.PowerPlatform.Dataverse.Client.PowerShell.Commands
{
    /// <summary>
    /// Adapter Class
    /// </summary>
    internal class PowerShellAdapter
    {
        private ManualResetEvent queueEvent = new ManualResetEvent(false);

        #region Parameters
        private Cmdlet cmdlet { get; set; }
        private Queue<object> queue { get; set; }
        private object LockToken { get; set; }
        public State state;
        public ErrorRecord? fatalErrorRecord;
        #endregion

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="cmdlet">cmdlet</param>
        public PowerShellAdapter(Cmdlet cmdlet)
        {
            this.cmdlet = cmdlet;
            this.LockToken = new object();
            this.queue = new Queue<object>();
            this.state = State.Running;
            fatalErrorRecord = null;
        }

        #region Methods
        /// <summary>
        /// Listener Method
        /// </summary>
        /// <param name="timeout">Time after which Operation will declared as Timed Out</param>
        public void Listen(TimeSpan timeout)
        {
            DateTime dateTime = DateTime.UtcNow;
            dateTime = dateTime.Add(timeout);
            while (state == State.Running || queue.Count > 0)
            {
                queueEvent.WaitOne(timeout);
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();

                    if (obj is ErrorRecord)
                    {
                        cmdlet.WriteError((ErrorRecord)obj);
                    }
                    else if (obj is ProgressRecord)
                    {
                        cmdlet.WriteProgress((ProgressRecord)obj);
                    }
                    else if (obj is GeneralWriteInfo)
                    {
                        // General write info to the UX. 
                        switch (((GeneralWriteInfo)obj).MessageType)
                        {
                            case WriteInfoType.Warning:
                                cmdlet.WriteWarning(((GeneralWriteInfo)obj).Message);
                                break;
                            case WriteInfoType.Verbose:
                                cmdlet.WriteVerbose(((GeneralWriteInfo)obj).Message);
                                break;
                            default:
                                cmdlet.WriteDebug(((GeneralWriteInfo)obj).Message);
                                break;
                        }
                    }
                }
                if (DateTime.UtcNow > dateTime)
                {
                    cmdlet.ThrowTerminatingError(new ErrorRecord(new Exception("TIMED_OUT"), "1", ErrorCategory.OperationTimeout, this));
                }
                Thread.Sleep(100);
            }

            if (state == State.FinishedWithError)
            {
                cmdlet.ThrowTerminatingError(fatalErrorRecord);
            }
        }

        public void Write(object obj)
        {
            lock (LockToken)
            {
                queue.Enqueue(obj);
                queueEvent.Set();
            }
        }
        #endregion
    }

    /// <summary>
    /// Determines the State of the Running Import
    /// </summary>
    internal enum State
    {
        Running,
        FinishedWithError,
        FinishedWithSuccess,
        TimedOut
    };
}
