using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    internal sealed class WorkloadAttributionScope : IDisposable
    {
        internal const string HeaderName = "x-ms-workload";
        internal const string ParameterName = "DataverseServiceClient.WorkloadAttribution";
        private static readonly AsyncLocal<WorkloadAttributionScope> _current = new AsyncLocal<WorkloadAttributionScope>();
        private readonly WorkloadAttributionScope _previous;
        private readonly ServiceClient _owner;
        private readonly WorkloadAttribution _workload;

        internal static WorkloadAttribution Current => _current.Value?._workload;

        private WorkloadAttributionScope(ServiceClient owner, WorkloadAttribution workload)
        {
            _previous = _current.Value;
            _owner = owner;
            _workload = workload;
            _current.Value = this;
        }

        internal static WorkloadAttributionScope Create(ServiceClient client, OrganizationRequest request = null, bool inheritWorkload = false)
        {
            // Only SDK-internal work on this client inherits an operation, including explicit omission.
            var workload = inheritWorkload && _current.Value != null && ReferenceEquals(_current.Value._owner, client)
                ? Current : client.WorkloadAttribution;
            if (request != null && request.Parameters.Contains(ParameterName))
            {
                object value = request.Parameters[ParameterName];
                if (value != null && !(value is WorkloadAttribution))
                    throw new ArgumentException("The workload request parameter must be a WorkloadAttribution instance.", nameof(request));
                workload = value as WorkloadAttribution;
                request.Parameters.Remove(ParameterName);
            }
            return new WorkloadAttributionScope(client, workload);
        }

        internal static void ApplyTo(IDictionary<string, List<string>> headers, WorkloadAttribution workload)
        {
            if (workload == null)
                return;
            foreach (string key in headers.Keys.Where(key => string.Equals(key, HeaderName, StringComparison.OrdinalIgnoreCase)).ToArray())
                headers.Remove(key);
            headers.Add(HeaderName, new List<string> { workload.ToString() });
        }

        internal static void ApplyTo(WebHeaderCollection headers)
        {
            if (Current != null)
                headers[HeaderName] = Current.ToString();
        }

        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}
