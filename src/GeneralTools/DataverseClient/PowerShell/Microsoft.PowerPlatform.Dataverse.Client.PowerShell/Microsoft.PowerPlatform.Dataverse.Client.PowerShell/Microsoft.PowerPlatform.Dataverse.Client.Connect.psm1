function Connect-PowerPlatformDataverse {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM( 
        [parameter(Position = 1, Mandatory = $true, ParameterSetName = "connectionstring")]
        [string]$ConnectionString, 
        [parameter(Position = 1, Mandatory = $true, ParameterSetName = "FIC")]
        [ValidatePattern('([\w-]+).crm([0-9]*).(microsoftdynamics|dynamics|crm[\w-]*).(com|de|us|cn)')]
        [string]$ServerUrl, 
        [parameter(Position = 2, Mandatory = $true, ParameterSetName = "FIC")]
        [ValidateScript({
                try {
                    [System.Guid]::Parse($_) | Out-Null
                    $true
                }
                catch {
                    $false
                }
            })]
        [string]$TenantId,
        [parameter(Position = 3, Mandatory = $true, ParameterSetName = "FIC")]
        [ValidateScript({
                try {
                    [System.Guid]::Parse($_) | Out-Null
                    $true
                }
                catch {
                    $false
                }
            })]
        [string]$ClientId,
        [parameter(Position = 4, Mandatory = $true, ParameterSetName = "FIC")]
        [ValidateScript({
                try {
                    [System.Guid]::Parse($_) | Out-Null
                    $true
                }
                catch {
                    $false
                }
            })]
        [string]$ServiceConnectionId,
        [parameter(Position = 4, Mandatory = $false, ParameterSetName = "FIC")]
        [string]$AccessTokenEnvironmentKeyName, 

        [int]$ConnectionTimeoutInSeconds,
#        [string]$LogWriteDirectory, 
        [switch]$BypassTokenCache
    )
    AddTls12Support #make sure tls12 is enabled 

    if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent -eq $true) {
        #            Enable-CrmConnectorVerboseLogging
    }
    
    if (-not [string]::IsNullOrEmpty($ServerUrl) -and $ServerUrl.StartsWith("https://", "CurrentCultureIgnoreCase") -ne $true) {
        Write-Verbose "ServerUrl is missing https, fixing URL: https://$ServerUrl"
        $ServerUrl = "https://" + $ServerUrl
    }
    
    #starting default connection string with require new instance and server url
    $cs = ";Url=$ServerUrl"
    if ($BypassTokenCache) {
        $cs += ";TokenCacheStorePath="
    }
    
    if ($ConnectionTimeoutInSeconds -and $ConnectionTimeoutInSeconds -gt 0) {
        $newTimeout = New-Object System.TimeSpan -ArgumentList 0, 0, $ConnectionTimeoutInSeconds
        Write-Verbose "Setting new connection timeout of $newTimeout"
        #set the timeout on the MaxConnectionTimeout static 
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]::MaxConnectionTimeout = $newTimeout
    }
    
    if ($ConnectionString) {
        if (!$ConnectionString -or $ConnectionString.Length -eq 0) {
            throw "Cannot create the ServiceClient, the connection string is null"
        }
        Write-Verbose "ConnectionString provided - skipping all helpers/known parameters"
            
        $global:conn = Get-PowerPlatformConnection -ConnectionString $ConnectionString
        if ($global:conn) {
            ApplyServiceClientObjectTemplate($global:conn)  #applyObjectTemplateFormat
        }
        return $global:conn
    }
    elseif ($ServiceConnectionId) {
        try {

            $global:conn =  Get-PowerPlatformConnection -ServiceConnectionId $ServiceConnectionId -TenantId $TenantId -ClientId $ClientId -AccessTokenEnvKeyName $AccessTokenEnvironmentKeyName -OrganizationUrl $ServerUrl
                
            ApplyServiceClientObjectTemplate($global:conn)  #applyObjectTemplateFormat
            $global:conn
            return
        }
        catch {
            throw $_
        }   
    }
}

function AddTls12Support {
    #by default PowerShell will show Ssl3, Tls - since SSL3 is not desirable we will drop it and use Tls + Tls12
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls -bor [System.Net.SecurityProtocolType]::Tls12
}

function ApplyServiceClientObjectTemplate {
    [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory = $true)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn
    )
    try {
        $defaultPropsServiceClient = @(
            'IsReady',
            'IsBatchOperationsAvailable',
            'MaxRetryCount',
            'RetryPauseTime', 
            'Authority',
            'ActiveAuthenticationType',
            'OAuthUserId',
            'TenantId',
            'EnvironmentId',
            'ConnectedOrgId',
            'ConnectedOrgUriActual',
            'ConnectedOrgFriendlyName',
            'ConnectedOrgUniqueName',
            'ConnectedOrgVersion',
            'SdkVersionProperty',
            'CallerId',
            'CallerAADObjectId',
            'DisableCrossThreadSafeties',
            'SessionTrackingId',
            'ForceServerMetadataCacheConsistency', 
            'RecommendedDegreesOfParallelism',
            'LastError'
        )
        $defaultPropsSetServiceClient = New-Object System.Management.Automation.PSPropertySet('DefaultDisplayPropertySet', [string[]]$defaultPropsServiceClient)
        $PSStandardMembers = [System.Management.Automation.PSMemberInfo[]]@($defaultPropsSetServiceClient)
        $conn | Add-Member MemberSet PSStandardMembers $PSStandardMembers -Force
    }
    Catch {
        Write-Verbose "Failed to set a new PSStandardMember on connection object"
    }
}