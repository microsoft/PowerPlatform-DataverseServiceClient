namespace Microsoft.PowerPlatform.Cds.Client.Auth
{
    /// <summary>
    /// ORGINAL CODE FROM https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/blob/68d7dea3643e075be85abbca1ab88ba614465541/src/Microsoft.IdentityModel.Clients.ActiveDirectory/Features/NonWinCommon/PromptBehavior.cs
    /// PORTED TO THIS LIB TO ACT AS A BRIDGE BETWEEN ADAL.NET AND MSAL.
    /// Indicates whether AcquireToken should automatically prompt only if necessary or whether
    /// it should prompt regardless of whether there is a cached token.
    /// </summary>
    public enum PromptBehavior
    {
        /// <summary>
        /// Acquire token will prompt the user for credentials only when necessary.  If a token
        /// that meets the requirements is already cached then the user will not be prompted.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// The user will be prompted for credentials even if there is a token that meets the requirements
        /// already in the cache.
        /// </summary>
        Always = 1,

        /// <summary>
        /// Re-authorizes (through displaying webview) the resource usage, making sure that the resulting access
        /// token contains updated claims. If user logon cookies are available, the user will not be asked for
        /// credentials again and the logon dialog will dismiss automatically.
        /// </summary>
        RefreshSession = 2,

        /// <summary>
        /// Prompt the user to select a user account even if there is a token that meets the requirements
        /// already in the cache. This enables an user who has multiple accounts at the Authorization Server to select amongst
        /// the multiple accounts that they might have current sessions for.
        /// </summary>
        SelectAccount = 3,

        /// <summary>
        /// Never Prompt
        /// </summary>
        Never = 4
    }
}
