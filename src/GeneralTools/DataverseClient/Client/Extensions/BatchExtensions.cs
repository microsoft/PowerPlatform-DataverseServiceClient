using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Dataverse Service Client extensions for batch operations. 
    /// </summary>
    public static class BatchExtensions
    {
        #region Batch Interface methods.
        /// <summary>
        /// Create a Batch Request for executing batch operations.  This returns an ID that will be used to identify a request as a batch request vs a "normal" request.
        /// </summary>
        /// <param name="batchName">Name of the Batch</param>
        /// <param name="returnResults">Should Results be returned</param>
        /// <param name="continueOnError">Should the process continue on an error.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static Guid CreateBatchOperationRequest(this ServiceClient serviceClient, string batchName, bool returnResults = true, bool continueOnError = false)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            Guid guBatchId = Guid.Empty;
            if (serviceClient._batchManager != null)
            {
                // Try to create a new Batch here.
                guBatchId = serviceClient._batchManager.CreateNewBatch(batchName, returnResults, continueOnError);
            }
            return guBatchId;
        }

        /// <summary>
        /// Returns the batch id for a given batch name.
        /// </summary>
        /// <param name="batchName">Name of Batch</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static Guid GetBatchOperationIdRequestByName(this ServiceClient serviceClient, string batchName)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return Guid.Empty;
            }
            #endregion

            if (serviceClient._batchManager != null)
            {
                var b = serviceClient._batchManager.GetRequestBatchByName(batchName);
                if (b != null)
                    return b.BatchId;
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Returns the organization request at a give position
        /// </summary>
        /// <param name="batchId">ID of the batch</param>
        /// <param name="position">Position</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        public static OrganizationRequest GetBatchRequestAtPosition(this ServiceClient serviceClient, Guid batchId, int position)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            RequestBatch b = serviceClient.GetBatchById(batchId);
            if (b != null)
            {
                if (b.BatchItems.Count >= position)
                    return b.BatchItems[position].Request;
            }
            return null;
        }

        /// <summary>
        /// Release a batch from the stack
        /// Once you have completed using a batch, you must release it from the system.
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="batchId">ID of the batch</param>
        public static void ReleaseBatchInfoById(this ServiceClient serviceClient, Guid batchId)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return;
            }
            #endregion

            if (serviceClient._batchManager != null)
                serviceClient._batchManager.RemoveBatch(batchId);

        }

        /// <summary>
        /// Returns a request batch by BatchID
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="batchId">ID of the batch</param>
        /// <returns></returns>
        public static RequestBatch GetBatchById(this ServiceClient serviceClient, Guid batchId)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            if (serviceClient._batchManager != null)
            {
                return serviceClient._batchManager.GetRequestBatchById(batchId);
            }
            return null;
        }

        /// <summary>
        /// Executes the batch command and then parses the retrieved items into a list.
        /// If there exists a exception then the LastException would be filled with the first item that has the exception.
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="batchId">ID of the batch to run</param>
        /// <returns>results which is a list of responses(type <![CDATA[ List<Dictionary<string, Dictionary<string, object>>> ]]>) in the order of each request or null or complete failure  </returns>
        public static List<Dictionary<string, Dictionary<string, object>>> RetrieveBatchResponse(this ServiceClient serviceClient, Guid batchId)
        {
            ExecuteMultipleResponse results = serviceClient.ExecuteBatch(batchId);
            if (results == null)
            {
                return null;
            }
            if (results.IsFaulted)
            {
                foreach (var response in results.Responses)
                {
                    if (response.Fault != null)
                    {
                        FaultException<OrganizationServiceFault> ex = new FaultException<OrganizationServiceFault>(response.Fault, new FaultReason(new FaultReasonText(response.Fault.Message)));

                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Failed to Execute Batch - {0}", batchId), TraceEventType.Verbose);
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ BatchExecution failed - : {0}\n\r{1}", response.Fault.Message, response.Fault.ErrorDetails.ToString()), TraceEventType.Error, ex);
                        break;
                    }
                }
            }
            List<Dictionary<string, Dictionary<string, object>>> retrieveMultipleResponseList = new List<Dictionary<string, Dictionary<string, object>>>();
            foreach (var response in results.Responses)
            {
                if (response.Response != null)
                {
                    retrieveMultipleResponseList.Add(QueryExtensions.CreateResultDataSet(((RetrieveMultipleResponse)response.Response).EntityCollection));
                }
            }
            return retrieveMultipleResponseList;
        }

        /// <summary>
        /// Begins running the Batch command.
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="batchId">ID of the batch to run</param>
        /// <returns>true if the batch begins, false if not. </returns>
        public static ExecuteMultipleResponse ExecuteBatch(this ServiceClient serviceClient, Guid batchId)
        {
            #region PreChecks
            serviceClient._logEntry.ResetLastError();
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return null;
            }

            if (!serviceClient.IsBatchOperationsAvailable)
            {
                serviceClient._logEntry.Log("Batch Operations are not available", TraceEventType.Error);
                return null;
            }
            #endregion

            if (serviceClient._batchManager != null)
            {
                var b = serviceClient._batchManager.GetRequestBatchById(batchId);
                if (b.Status == BatchStatus.Complete || b.Status == BatchStatus.Running)
                {
                    serviceClient._logEntry.Log("Batch is not in the correct state to run", TraceEventType.Error);
                    return null;
                }

                if (!(b.BatchItems.Count > 0))
                {
                    serviceClient._logEntry.Log("No Items in the batch", TraceEventType.Error);
                    return null;
                }

                // Ready to run the batch.
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "Executing Batch {0}|{1}, Sending {2} events.", b.BatchId, b.BatchName, b.BatchItems.Count), TraceEventType.Verbose);
                ExecuteMultipleRequest req = new ExecuteMultipleRequest();
                req.Settings = b.BatchRequestSettings;
                OrganizationRequestCollection reqstList = new OrganizationRequestCollection();

                // Make sure the batch is ordered.
                reqstList.AddRange(b.BatchItems.Select(s => s.Request));

                req.Requests = reqstList;
                b.Status = BatchStatus.Running;
                ExecuteMultipleResponse resp = (ExecuteMultipleResponse)serviceClient.Command_Execute(req, "Execute Batch Command");
                // Need to add retry logic here to deal with a "server busy" status.
                b.Status = BatchStatus.Complete;
                if (resp != null)
                {
                    if (resp.IsFaulted)
                        serviceClient._logEntry.Log("Batch request faulted.", TraceEventType.Warning);
                    b.BatchResults = resp;
                    return b.BatchResults;
                }
                serviceClient._logEntry.Log("Batch request faulted - No Results.", TraceEventType.Warning);
            }
            return null;
        }

        /// <summary>
        /// Adds a request to a batch with display and handling logic
        /// will fail out if batching is not enabled.
        /// </summary>
        /// <param name="batchId">ID of the batch to add too</param>
        /// <param name="req">Organization request to Add</param>
        /// <param name="batchTagText">Batch Add Text, this is the text that will be reflected when the batch is added - appears in the batch diags</param>
        /// <param name="successText">Success Added Batch - appears in webSvcActions diag</param>
        /// <param name="bypassPluginExecution">Adds the bypass plugin behavior to this request. Note: this will only apply if the caller has the prvBypassPlugins permission to bypass plugins.  If its attempted without the permission the request will fault.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns></returns>
        internal static bool AddRequestToBatch(this ServiceClient serviceClient, Guid batchId, OrganizationRequest req, string batchTagText, string successText, bool bypassPluginExecution)
        {
            if (batchId != Guid.Empty)
            {
                // if request should bypass plugin exec.
                if (bypassPluginExecution &&
                    Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient.ConnectedOrgVersion, Utilities.FeatureVersionMinimums.AllowBypassCustomPlugin))
                    req.Parameters[Utilities.RequestHeaders.BYPASSCUSTOMPLUGINEXECUTION] =  true;

                if (serviceClient.IsBatchOperationsAvailable)
                {
                    if (serviceClient._batchManager.AddNewRequestToBatch(batchId, req, batchTagText))
                    {
                        serviceClient._logEntry.Log(successText, TraceEventType.Verbose);
                        return true;
                    }
                    else
                        serviceClient._logEntry.Log("Unable to add request to batch queue, Executing normally", TraceEventType.Warning);
                }
                else
                {
                    // Error and fall though.
                    serviceClient._logEntry.Log("Unable to add request to batch, Batching is not currently available, Executing normally", TraceEventType.Warning);
                }
            }
            return false;
        }

        #endregion
    }
}
