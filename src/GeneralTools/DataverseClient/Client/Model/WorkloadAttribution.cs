using System;
using System.Text;

namespace Microsoft.PowerPlatform.Dataverse.Client.Model
{
    /// <summary>
    /// Immutable, caller-declared diagnostic identity for a Dataverse workload.
    /// IDs must be persisted by the caller, not generated for each request or retry.
    /// </summary>
    public sealed class WorkloadAttribution
    {
        private readonly string _headerValue;

        /// <summary>Readable product or integration name.</summary>
        public string OriginName { get; }
        /// <summary>Caller-maintained, nonzero logical origin ID.</summary>
        public Guid OriginId { get; }
        /// <summary>Optional logical application or service name.</summary>
        public string ScopeName { get; }
        /// <summary>Optional caller-maintained, nonzero logical scope ID.</summary>
        public Guid? ScopeId { get; }
        /// <summary>Optional feature or logical entry point.</summary>
        public string Component { get; }

        /// <summary>Creates and validates workload attribution.</summary>
        /// <param name="originName">A 3-63 character ASCII name starting with a letter, followed by letters, digits, or underscores.</param>
        /// <param name="originId">A stable, nonzero origin GUID.</param>
        /// <param name="scopeName">An optional name with the same format as originName. Requires scopeId.</param>
        /// <param name="scopeId">An optional stable, nonzero scope GUID. Requires scopeName.</param>
        /// <param name="component">An independently optional name with the same format as originName.</param>
        /// <exception cref="ArgumentException">A name, ID, or scope pair is invalid.</exception>
        /// <exception cref="ArgumentNullException">originName is null.</exception>
        public WorkloadAttribution(string originName, Guid originId, string scopeName = null, Guid? scopeId = null, string component = null)
        {
            ValidateName(originName, nameof(originName));
            if (originId == Guid.Empty)
                throw new ArgumentException("The origin ID must not be empty.", nameof(originId));
            if ((scopeName != null) != scopeId.HasValue)
                throw new ArgumentException("Supply both scopeName and scopeId, or omit both.", nameof(scopeName));
            if (scopeName != null)
                ValidateName(scopeName, nameof(scopeName));
            if (scopeId == Guid.Empty)
                throw new ArgumentException("The scope ID must not be empty.", nameof(scopeId));
            if (component != null)
                ValidateName(component, nameof(component));

            OriginName = originName;
            OriginId = originId;
            ScopeName = scopeName;
            ScopeId = scopeId;
            Component = component;
            _headerValue = $"origin.name={originName}, origin.id={originId:D}";
            if (scopeId.HasValue)
                _headerValue += $", scope.name={scopeName}, scope.id={scopeId.Value:D}";
            if (component != null)
                _headerValue += $", component={component}";
            if (Encoding.UTF8.GetByteCount(_headerValue) > 1024)
                throw new ArgumentException("The workload header value must not exceed 1,024 bytes.");
        }

        /// <summary>Returns the validated x-ms-workload header value.</summary>
        public override string ToString() => _headerValue;

        private static void ValidateName(string value, string parameterName)
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);
            if (value.Length < 3 || value.Length > 63 || !IsLetter(value[0]))
                throw new ArgumentException("Names must contain 3-63 ASCII letters, digits, or underscores and start with a letter.", parameterName);
            foreach (char character in value)
                if (!IsLetter(character) && !(character >= '0' && character <= '9') && character != '_')
                    throw new ArgumentException("Names must contain only ASCII letters, digits, or underscores.", parameterName);
        }

        private static bool IsLetter(char value) =>
            (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
    }
}
