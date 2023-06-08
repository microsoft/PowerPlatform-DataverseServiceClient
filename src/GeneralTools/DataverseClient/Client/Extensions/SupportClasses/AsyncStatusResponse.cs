#pragma warning disable CS1591

using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    public class AsyncStatusResponse
    {
        /// <summary>
        /// Status of the system job.
        /// </summary>
        public enum AsyncStatusResponse_statecode
        {
            Ready = 0,
            Suspended = 1,
            Locked = 2,
            Completed = 3,
            FailedParse = 999
        }

        /// <summary>
        /// Reason for the status of the system job.
        /// </summary>
        public enum AsyncStatusResponse_statuscode
        {
            WaitingForResources = 0,
            Waiting = 10,
            InProgress = 20,
            Pausing = 21,
            Canceling = 22,
            Succeeded = 30,
            Failed = 31,
            Canceled = 32,
            FailedParse = 999
        }

        /// <summary>
        /// Raw entity returned from the operation status poll.
        /// </summary>
        public Entity RetrievedEntity { get; set; }
        
        /// <summary>
        /// Operation Id.
        /// </summary>
        public Guid AsyncOperationId
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Id;
                else
                    return Guid.Empty;
            }
        }

        /// <summary>
        /// Name of the Operation
        /// </summary>
        public string OperationName
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("name") ? RetrievedEntity.GetAttributeValue<string>("name") : string.Empty;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Type of the Operation <see href="https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/asyncoperation#operationtype-choicesoptions"/>
        /// </summary>
        public string OperationType
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("operationtype") ? RetrievedEntity.FormattedValues["operationtype"] : string.Empty;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// User readable message, if available. 
        /// </summary>
        public string FriendlyMessage
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("friendlymessage") ? RetrievedEntity.GetAttributeValue<string>("friendlymessage") : string.Empty;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// System message, if available 
        /// </summary>
        public string Message
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("message") ? RetrievedEntity.GetAttributeValue<string>("message") : string.Empty;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Correlation id
        /// </summary>
        public Guid CorrlationId
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("correlationid") ? RetrievedEntity.GetAttributeValue<Guid>("correlationid") : Guid.Empty;
                else
                    return Guid.Empty;
            }
        }

        /// <summary>
        /// Operation Status Code.
        /// </summary>
        public AsyncStatusResponse_statuscode StatusCode { get; internal set; }

        /// <summary>
        /// Localized text version of Status code, if available
        /// </summary>
        public string StatusCode_Localized {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("statuscode") ? RetrievedEntity.FormattedValues["statuscode"] : string.Empty;
                else
                    return string.Empty;
            }
        }
        
        /// <summary>
        /// Operation State code
        /// </summary>
        public AsyncStatusResponse_statecode State { get; internal set; }

        /// <summary>
        /// Localized text version of state code text, if available 
        /// </summary>
        public string State_Localized
        {
            get
            {
                if (RetrievedEntity != null)
                    return RetrievedEntity.Attributes.ContainsKey("statecode") ? RetrievedEntity.FormattedValues["statecode"] : string.Empty;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Creates an AsyncStatusResponse Object
        /// </summary>
        /// <param name="asyncOperationResponses"></param>
        internal AsyncStatusResponse(EntityCollection asyncOperationResponses)
        {
            // parse the Async Operation type. 
            if (asyncOperationResponses == null)
            {
                // Do something Result is null. 
                
            }
            else if ( asyncOperationResponses != null && !asyncOperationResponses.Entities.Any()) {
                // Do something ( no records ) 
            }else
            {
                // not null and have records. 
                this.RetrievedEntity = asyncOperationResponses.Entities.First(); // get first entity. 
                // Parse state and status 
                OptionSetValue ostatecode =  RetrievedEntity.Attributes.ContainsKey("statecode") ? RetrievedEntity.GetAttributeValue<OptionSetValue>("statecode") : new OptionSetValue(-1);
                try
                {
                    State = (AsyncStatusResponse_statecode)ostatecode.Value; 
                }
                catch
                {
                    State = AsyncStatusResponse_statecode.FailedParse;
                }

                OptionSetValue ostatuscode = RetrievedEntity.Attributes.ContainsKey("statuscode") ? RetrievedEntity.GetAttributeValue<OptionSetValue>("statuscode") : new OptionSetValue(-1);
                try
                {
                    StatusCode = (AsyncStatusResponse_statuscode)ostatuscode.Value;
                }
                catch
                {
                    StatusCode = AsyncStatusResponse_statuscode.FailedParse;
                }

            }
        }

    }
}
#pragma warning restore CS1591