function Coalesce {
    foreach ($i in $args) {
        if ($i -ne $null) {
            return $i
        }
    }
}

function LastConnectorException {
    [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory = $true)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn
    )

    return (Coalesce $conn.LastError $conn.LastException) 
}

function VerifyConnectionParam {
	[CmdletBinding()]
    PARAM( 
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory=$false)]
        [bool]$pipelineValue
    )
    #we have a $conn value and we were not given a $conn value so we should try to find one
    if($conn -eq $null -and $pipelineValue -eq $false)
    {
        $connobj = Get-Variable conn -Scope global -ErrorAction SilentlyContinue
        if($connobj.Value -eq $null)
        {
            throw 'A connection to Dataverse is required, use Get-PowerPlatformConnection or one of the other connection functions to connect.'
        }
        else
        {
            $conn = $connobj.Value
        }
    }elseif($conn -eq $null -and $pipelineValue -eq $true){
        throw "Connection object provided is null"
    }
	return $conn
}