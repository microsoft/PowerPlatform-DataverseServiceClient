using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// This class manages batches for CDS execute Multiple.
	/// </summary>
	internal sealed class BatchManager
	{
		#region Vars
		/// <summary>
		/// Collection of Batches in use.
		/// </summary>
		private Dictionary<Guid, RequestBatch> RequestBatches = null;

		/// <summary>
		/// Max number of concurrent batches allowed.
		/// </summary>
		private int MaxNumberOfBatches = 0;
		/// <summary>
		/// Max number of requests per batch allowed.
		/// </summary>
		private int MaxNumberOfRequestsInABatch = 0;

		/// <summary>
		/// Local Log file.
		/// </summary>
		private CdsTraceLogger logger = null;

		#endregion

		/// <summary>
		/// Base Constructor..
		/// </summary>
		/// <param name="MaxBatches">Max number of concurrent batches possible</param>
		/// <param name="MaxRequestPerBatch">Max number of requests per Batch</param>
		/// <param name="traceLogger">TraceLogger</param>
		public BatchManager(CdsTraceLogger traceLogger, int MaxBatches = 50000, int MaxRequestPerBatch = 5000)
		{
			logger = traceLogger;
			// Do a Version Check here? 
			MaxNumberOfBatches = MaxBatches;
			MaxNumberOfRequestsInABatch = MaxRequestPerBatch;
			RequestBatches = new Dictionary<Guid, RequestBatch>();
			logger.Log(string.Format(CultureInfo.InvariantCulture, "New Batch Manager Created, Max #of Batches:{0}, Max #of RequestsPerBatch:{1}", MaxBatches, MaxRequestPerBatch), System.Diagnostics.TraceEventType.Verbose);
		}

		/// <summary>
		/// Adds a new Batch to the Queue
		/// </summary>
		/// <param name="name">Name of the batch</param>
		/// <param name="returnResults">Should the Batch Return results. </param>
		/// <param name="continueOnError">Should the Batch Continue on Error. </param>
		/// <returns></returns>
		[SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "", MessageId = "")]
		public Guid CreateNewBatch(string name, bool returnResults = true, bool continueOnError = false)
		{
			if (RequestBatches.Count > MaxNumberOfBatches)
			{
				// Set last error as no more room. 
				logger.Log(string.Format(CultureInfo.InvariantCulture, "No more batches available"), System.Diagnostics.TraceEventType.Error, new Exception("MAX CURRENT BATCHES EXCEEDED"));
				return Guid.Empty;
			}

			RequestBatch nBatch = new RequestBatch(name, returnResults, continueOnError);
			RequestBatches.Add(nBatch.BatchId, nBatch);

			return nBatch.BatchId;
		}

		/// <summary>
		/// Returns the request batch by ID,
		/// </summary>
		/// <param name="batchId">ID of the batch to return</param>
		/// <returns></returns>
		public RequestBatch GetRequestBatchById(Guid batchId)
		{
			if (RequestBatches != null)
				if (RequestBatches.ContainsKey(batchId))
					return RequestBatches[batchId];
			return null;
		}

		/// <summary>
		/// Returns a request batch by name.
		/// </summary>
		/// <param name="batchName">Name of the Batch. </param>
		/// <returns></returns>
		public RequestBatch GetRequestBatchByName(string batchName)
		{
			try
			{
				if (RequestBatches != null)
				{
					var Rslt = RequestBatches.Where(w => w.Value.BatchName.Equals(batchName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
					if (Rslt.Value != null)
						return Rslt.Value;
				}
			}
			catch
			{
				// eat the exception
			}
			return null;
		}

		/// <summary>
		/// Add an item to batch.
		/// </summary>
		/// <param name="batchId">ID of the batch</param>
		/// <param name="request">Organization Service Request to add to the batch</param>
		/// <param name="debugMsg">debug messages to associate to the batch</param>
		/// <returns></returns>
		[SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "", MessageId = "")]
		public bool AddNewRequestToBatch(Guid batchId, OrganizationRequest request, string debugMsg)
		{
			try
			{
				RequestBatch wBatch = GetRequestBatchById(batchId);
				if (wBatch != null)
				{
					if (wBatch.BatchItems.Count() > MaxNumberOfRequestsInABatch)
					{
						//too many items.. echo an error. 
						logger.Log(string.Format(CultureInfo.InvariantCulture, "Number of concurrent requests in a batch exceeded, Max number of requests per batch is {0}",
							MaxNumberOfRequestsInABatch), System.Diagnostics.TraceEventType.Error, new Exception("MAX NUMBER OF RECORDS IN A BATCHE EXCEEDED"));
						return false;
					}

					// add item to batch 
					request.RequestId = Guid.NewGuid();
					wBatch.BatchItems.Add(new BatchItemOrganizationRequest() { Request = request, RequestReferenceNumber = request.RequestId.Value, RequestDebugMessage = debugMsg });
					return true;
				}
			}
			catch
			{
				// eat the exception
			}
			// Missing Batch.. 
			return false;
		}

		/// <summary>
		/// Removes and releases the batch by ID.
		/// </summary>
		/// <param name="batchId"></param>
		public void RemoveBatch(Guid batchId)
		{
			if (RequestBatches.ContainsKey(batchId))
				RequestBatches.Remove(batchId);
		}

	}

	/// <summary>
	/// Container class for Batches.
	/// </summary>
	public sealed class RequestBatch
	{
		/// <summary>
		/// ID of the batch.
		/// </summary>
		public Guid BatchId { get; internal set; }

		/// <summary>
		/// DisplayName of the batch.
		/// </summary>
		public string BatchName { get; internal set; }

		/// <summary>
		/// Settings for this Execute Multiple Request.
		/// </summary>
		public ExecuteMultipleSettings BatchRequestSettings { get; private set; }

		/// <summary>
		/// Items to execute
		/// </summary>
		//public OrganizationRequestCollection BatchItems { get; set; }
		public List<BatchItemOrganizationRequest> BatchItems { get; set; }

		/// <summary>
		/// Results from the Batch.
		/// </summary>
		public ExecuteMultipleResponse BatchResults { get; set; }

		/// <summary>
		/// Status of the batch.
		/// </summary>
		public BatchStatus Status { get; set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="returnResponses">True to return responses, False to not return responses</param>
		/// <param name="continueOnError">True to continue if anyone item trips an error, False to stop on the first error. </param>
		/// <param name="batchName">String name of the batch, if blank, a GUID is used</param>
		public  RequestBatch(string batchName = "", bool returnResponses = true, bool continueOnError = false)
		{
			// Create the batch ID and name. 
			BatchId = Guid.NewGuid();
			if (!string.IsNullOrWhiteSpace(batchName))
				BatchName = batchName;
			else
				BatchName = string.Format(CultureInfo.InvariantCulture, "BATCH-{0}-{1}", DateTime.UtcNow.Ticks, BatchId.ToString());  // Generate a reasonably Unique Name. 

			BatchRequestSettings = new ExecuteMultipleSettings() { ContinueOnError = continueOnError, ReturnResponses = returnResponses };
			BatchItems = new List<BatchItemOrganizationRequest>(); //new OrganizationRequestCollection(); 
		}
	}
	/// <summary>
	/// Request object.
	/// </summary>
	public sealed class BatchItemOrganizationRequest
	{
		/// <summary>
		/// Organization Service request for the batch
		/// </summary>
		public OrganizationRequest Request { get; set; }
		/// <summary>
		/// Reference Correlation ID
		/// </summary>
		public Guid RequestReferenceNumber { get; set; }
		/// <summary>
		/// Request debug Message.
		/// </summary>
		public string RequestDebugMessage { get; set; }
	}

	/// <summary>
	/// Status of the batch.
	/// </summary>
	public enum BatchStatus
	{
		/// <summary>
		/// Batch is waiting to be run
		/// </summary>
		Waiting = 0,
		/// <summary>
		/// Batch is currently executing
		/// </summary>
		Running,
		/// <summary>
		/// Batch has completed.
		/// </summary>
		Complete
	}
}
