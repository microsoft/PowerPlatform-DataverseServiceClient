Import-Module (Join-Path (Split-Path $script:MyInvocation.MyCommand.Path) "UtilityFunctions.psm1") -NoClobber #-Force


function New-DataverseRecord {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "NameAndFields")]
        [string]$EntityLogicalName,
        [parameter(Mandatory = $true, Position = 2, ParameterSetName = "NameAndFields")]
        [hashtable]$Fields,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "DataverseRecord")]
        [PSObject]$DataverseRecord,
        [parameter(Mandatory = $false, Position = 2, ParameterSetName = "DataverseRecord")]
        [switch]$PreserveDataverseRecordId
    )

    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))

    $newfields = New-Object 'System.Collections.Generic.Dictionary[[String], [Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper]]'
    
    if ($DataverseRecord -ne $null) {
        $EntityLogicalName = $DataverseRecord.ReturnProperty_EntityName
        $atts = Get-DataverseEntityAttributes -conn $conn -EntityLogicalName $EntityLogicalName
        foreach ($DvFieldKey in ($DataverseRecord | Get-Member -MemberType NoteProperty).Name) {
            if ($DvFieldKey.EndsWith("_Property")) {
                if ($DataverseRecord.ReturnProperty_Id -eq $DataverseRecord.$DvFieldKey.Value -and !$PreserveDataverseRecordId) {
                    continue;
                }               
                elseif (($atts | ? logicalname -eq $DataverseRecord.$DvFieldKey.Key).IsValidForCreate) {
                    # Some fields cannot be created even though it is set as IsValidForCreate
                    if ($DataverseRecord.$DvFieldKey.Key.Contains("addressid")) {
                        continue;
                    }
                    else {
                        $newfield = New-Object -TypeName 'Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper'
            
                        $newfield.Type = MapFieldTypeByFieldValue -Value $DataverseRecord.$DvFieldKey.Value                 
                        $newfield.Value = $DataverseRecord.$DvFieldKey.Value
                        $newfields.Add($DataverseRecord.$DvFieldKey.Key, $newfield)
                    }
                }
            }
        }  
    }
    else {
        foreach ($field in $Fields.GetEnumerator()) {  
            $newfield = New-Object -TypeName 'Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper'
            
            $newfield.Type = MapFieldTypeByFieldValue -Value $field.Value
            
            $newfield.Value = $field.Value
            $newfields.Add($field.Key, $newfield)
        }
    }
    try {        
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.CRUDExtentions]::CreateNewRecord($conn, $EntityLogicalName, $newfields, $null, $false, [Guid]::Empty, $false)
        if (!$result -or $result -eq [System.Guid]::Empty) {
            throw LastConnectorException($conn)
        }
    }
    catch {
        Write-Error LastConnectorException($conn)
    }

    return $result
}

function Get-DataverseRecord {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1)]
        [string]$EntityLogicalName,
        [parameter(Mandatory = $true, Position = 2)]
        [guid]$Id,
        [parameter(Mandatory = $true, Position = 3)]
        [string[]]$Fields,
        [parameter(Mandatory = $false, Position = 4)]
        [switch]$IncludeNullValue
    )

    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))

    if ($Fields -eq "*") {
        [Collections.Generic.List[String]]$x = $null
    }
    else {
        [Collections.Generic.List[String]]$x = $Fields
    }

    try {
        $record = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.QueryExtensions]::GetEntityDataById($conn, $EntityLogicalName, $Id, $x, [Guid]::Empty)
    }
    catch {
        throw LastConnectorException($conn)        
    } 
    
    if ($record -eq $null) {        
        throw LastConnectorException($conn)
    }
        
    $psobj = @{ }
    $meta = Get-DataverseEntityMetadata -conn $conn -EntityLogicalName $EntityLogicalName -EntityFilters Attributes
    if ($IncludeNullValue) {
        if ($Fields -eq "*") {
            # Add all fields first
            foreach ($attName in $meta.Attributes) {
                if (-not $attName.IsValidForRead) { continue }
                $psobj[$attName.LogicalName] = $null
                $psobj["$($attName.LogicalName)_Property"] = $null
            }
        }
        else {
            foreach ($attName in $Fields) {
                $psobj[$attName] = $null
                $psobj["$($attName)_Property"] = $null
            }
        }
    }
        
    foreach ($att in $record.GetEnumerator()) {       
        if ($att.Value -is [Microsoft.Xrm.Sdk.EntityReference]) {
            $psobj[$att.Key] = $att.Value.Name
        }
        elseif ($att.Value -is [Microsoft.Xrm.Sdk.AliasedValue]) {
            $psobj[$att.Key] = $att.Value.Value
        }
        else {
            $psobj[$att.Key] = $att.Value
        }                
    }   
    $psobj += @{
        original                  = $record
        logicalname               = $EntityLogicalName
        EntityReference           = New-DataverseEntityReference -EntityLogicalName $EntityLogicalName -Id $Id
        ReturnProperty_EntityName = $EntityLogicalName
        ReturnProperty_Id         = $record.($meta.PrimaryIdAttribute)
    }
    
    [PSCustomObject]$psobj
}

function Set-DataverseRecord {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml

    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "DataverseRecord")]
        [PSObject]$DataverseRecord,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "Fields")]
        [string]$EntityLogicalName,
        [parameter(Mandatory = $true, Position = 2, ParameterSetName = "Fields")]
        [guid]$Id,
        [parameter(Mandatory = $true, Position = 3, ParameterSetName = "Fields")]
        [hashtable]$Fields,
        [parameter(Mandatory = $false)]
        [switch]$Upsert,
        [parameter(Mandatory = $false)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$PrimaryKeyField
    )

    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    
    if ($DataverseRecord -ne $null) { 
        $entityLogicalName = $DataverseRecord.logicalname
    }
    else {
        $entityLogicalName = $EntityLogicalName
    }
    
    # 'PrimaryKeyField' is an options parameter and is used for custom activity entities
    if (-not [string]::IsNullOrEmpty($PrimaryKeyField)) {
        $primaryKeyField = $PrimaryKeyField
    }
    else {
        $primaryKeyField = GuessPrimaryKeyField -EntityLogicalName $entityLogicalName
    }

    # If upsert specified
    if ($Upsert) {
        $retrieveFields = New-Object System.Collections.Generic.List[string]
        if ($DataverseRecord -ne $null) {
            # when DataverseRecord passed, assume this comes from other system.
            $id = $DataverseRecord.$primaryKeyField
            foreach ($DvFieldKey in ($DataverseRecord | Get-Member -MemberType NoteProperty).Name) {
                if ($DvFieldKey.EndsWith("_Property")) {
                    $retrieveFields.Add(($DataverseRecord.$DvFieldKey).Key)
                }
                elseif (($DvFieldKey -eq "original") -or ($DvFieldKey -eq "logicalname") -or ($DvFieldKey -eq "EntityReference")`
                        -or ($DvFieldKey -like "ReturnProperty_*")) {
                    continue
                }
                else {
                    # to have original value, rather than formatted value, replace the value from original record.
                    $DataverseRecord.$DvFieldKey = $DataverseRecord.original[$DvFieldKey + "_Property"].Value
                }
            }            
        }
        else {
            foreach ($DvFieldKey in $Fields.Keys) {
                $retrieveFields.Add($DvFieldKey)
            }           
        }

        $existingRecord = Get-DataverseRecord -conn $conn -EntityLogicalName $entityLogicalName -Id $id -Fields $retrieveFields.ToArray() -ErrorAction SilentlyContinue

        if ($existingRecord.original -eq $null) {
            if ($DataverseRecord -ne $null) {
                $Fields = @{}
                foreach ($DvFieldKey in ($DataverseRecord | Get-Member -MemberType NoteProperty).Name) {
                    if ($DvFieldKey.EndsWith("_Property")) {
                        $Fields.Add(($DataverseRecord.$DvFieldKey).Key, ($DataverseRecord.$DvFieldKey).Value)
                    }
                } 
            }

            if ($Fields[$primaryKeyField] -eq $null) {
                $Fields.Add($primaryKeyField, $Id)
            }
            # if no record exists, then create new
            $result = New-DataverseRecord -conn $conn -EntityLogicalName $entityLogicalName -Fields $Fields

            return $result
        }
        else {   
            if ($DataverseRecord -ne $null) {
                # if record exists, then swap original record so that we can compare updated fields
                $DataverseRecord.original = $existingRecord.original
            }
        }
    }

    $newfields = New-Object 'System.Collections.Generic.Dictionary[[String], [Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper]]'
    
    if ($DataverseRecord -ne $null) {                
        $originalRecord = $DataverseRecord.original        
        $Id = $originalRecord[$primaryKeyField]
        
        foreach ($DvFieldKey in ($DataverseRecord | Get-Member -MemberType NoteProperty).Name) {
            $DvFieldValue = $DataverseRecord.($DvFieldKey)
            if (($DvFieldKey -eq "original") -or ($DvFieldKey -eq "logicalname") -or ($DvFieldKey -eq "EntityReference")`
                    -or ($DvFieldKey -like "*_Property") -or ($DvFieldKey -like "ReturnProperty_*")) {
                continue
            }
            elseif ($originalRecord[$DvFieldKey + "_Property"].Value -is [bool]) {
                if ($DvFieldValue -is [Int32]) {
                    if (($originalRecord[$DvFieldKey + "_Property"].Value -and $DvFieldValue -eq 1) -or `
                        (!$originalRecord[$DvFieldKey + "_Property"].Value -and $DvFieldValue -eq 0)) {
                        continue 
                    }  
                }
                elseif ($DvFieldValue -is [bool]) {
                    if ($DvFieldValue -eq $originalRecord[$DvFieldKey + "_Property"].Value) {
                        continue
                    }
                }
                elseif ($DvFieldValue -eq $originalRecord[$DvFieldKey]) {
                    continue
                }                             
            }
            elseif ($originalRecord[$DvFieldKey + "_Property"].Value -is [Microsoft.Xrm.Sdk.OptionSetValue]) { 
                if ($DvFieldValue -is [Microsoft.Xrm.Sdk.OptionSetValue]) {
                    if ($DvFieldValue.Value -eq $originalRecord[$DvFieldKey + "_Property"].Value.Value) {
                        continue
                    }
                } 
                elseif ($DvFieldValue -is [Int32]) {
                    if ($DvFieldValue -eq $originalRecord[$DvFieldKey + "_Property"].Value.Value) {
                        continue
                    }
                }
                elseif ($DvFieldValue -eq $originalRecord[$DvFieldKey]) {
                    continue
                }
            }            
            elseif ($originalRecord[$DvFieldKey + "_Property"].Value -is [Microsoft.Xrm.Sdk.Money]) { 
                if ($DvFieldValue -is [Microsoft.Xrm.Sdk.Money]) {
                    if ($DvFieldValue.Value -eq $originalRecord[$DvFieldKey + "_Property"].Value.Value) {
                        continue
                    }
                }
                elseif ($DvFieldValue -is [decimal] -or $DvFieldValue -is [Int32]) {
                    if ($DvFieldValue -eq $originalRecord[$DvFieldKey + "_Property"].Value.Value) {
                        continue
                    }
                }
                elseif ($DvFieldValue -eq $originalRecord[$DvFieldKey]) {
                    continue
                }
            }
            elseif ($originalRecord[$DvFieldKey + "_Property"].Value -is [Microsoft.Xrm.Sdk.EntityReference]) { 
                if (($DvFieldValue -is [Microsoft.Xrm.Sdk.EntityReference]) -and ($DvFieldValue.Name -eq $originalRecord[$DvFieldKey].Name)) {
                    continue
                }
                elseif ($DvFieldValue -eq $originalRecord[$DvFieldKey]) {
                    continue
                }
            }
            elseif ($DvFieldValue -eq $originalRecord[$DvFieldKey]) { 
                continue 
            }

            $newfield = New-Object -TypeName 'Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper'
            $value = New-Object psobject
            
            # When value set to null, then just use raw type and set value to $null
            if ($DvFieldValue -eq $null) {
                $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
                $value = $null
            }
            else {
                if ($DataverseRecord.($DvFieldKey + "_Property") -ne $null) {
                    $type = $DataverseRecord.($DvFieldKey + "_Property").Value.GetType().Name
                }
                else {
                    $type = $DvFieldValue.GetType().Name
                }
                switch ($type) {
                    "Boolean" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Boolean
                        if ($DvFieldValue -is [Boolean]) {
                            $value = $DvFieldValue
                        }
                        else {
                            $value = [Int32]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "DateTime" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::DateTime
                        if ($DvFieldValue -is [DateTime]) {
                            $value = $DvFieldValue
                        }
                        else {
                            $value = [DateTime]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "Decimal" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Decimal
                        if ($DvFieldValue -is [Decimal]) {
                            $value = $DvFieldValue
                        }
                        else {
                            $value = [Decimal]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "Single" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Float
                        if ($DvFieldValue -is [Single]) {
                            $value = $DvFieldValue
                        }
                        else {
                            $value = [Single]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "Money" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
                        if ($DvFieldValue -is [Microsoft.Xrm.Sdk.Money]) {                
                            $value = $DvFieldValue
                        }
                        else {                
                            $value = New-Object -TypeName 'Microsoft.Xrm.Sdk.Money'
                            $value.Value = $DvFieldValue
                        }
                        break
                    }
                    "Int32" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Number
                        if ($DvFieldValue -is [Int32]) {
                            $value = $DvFieldValue
                        }
                        else {
                            $value = [Int32]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "EntityReference" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
                        $value = $DvFieldValue
                        break
                    }
                    "OptionSetValue" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
                        if ($DvFieldValue -is [Microsoft.Xrm.Sdk.OptionSetValue]) {
                            $value = $DvFieldValue                        
                        }
                        else {
                            $value = New-Object -TypeName 'Microsoft.Xrm.Sdk.OptionSetValue'
                            $value.Value = [Int32]::Parse($DvFieldValue)
                        }
                        break
                    }
                    "String" {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::String
                        $value = $DvFieldValue
                        break
                    }
                    default {
                        $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
                        $value = $DvFieldValue
                        break
                    }
                }
            }
            $newfield.Value = $value
            $newfields.Add($DvFieldKey, $newfield)
        }
    }
    else {
        foreach ($field in $Fields.GetEnumerator()) {  
            $newfield = New-Object -TypeName 'Microsoft.PowerPlatform.Dataverse.Client.DataverseDataTypeWrapper'
            if ($field.value -eq $null) {
                $newfield.Type = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw
            }
            else {
                $newfield.Type = MapFieldTypeByFieldValue -Value $field.Value
            }
            $newfield.Value = $field.Value
            $newfields.Add($field.Key, $newfield)
        }
    }
    try {
        # if no field has new value, then do nothing.
        if ($newfields.Count -eq 0) {
            return
        }
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.CRUDExtentions]::UpdateEntity($conn, $entityLogicalName, $primaryKeyField, $Id, $newfields, $null, $false, [Guid]::Empty)
        if (!$result) {
            throw LastConnectorException($conn)
        }
    }
    catch {
        #TODO: Throw Exceptions back to user
        throw LastConnectorException($conn)
    }
}

#DeleteEntity 
function Remove-DataverseRecord {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "DataverseRecord", ValueFromPipeline = $True)]
        [PSObject]$DataverseRecord,
        [parameter(Mandatory = $true, Position = 1, ParameterSetName = "Fields")]
        [string]$EntityLogicalName,
        [parameter(Mandatory = $true, Position = 2, ParameterSetName = "Fields")]
        [guid]$Id
    )

    begin {
        $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    }
    process {
        if ($DataverseRecord -ne $null) {
            $EntityLogicalName = $DataverseRecord.logicalname
            $Id = $DataverseRecord.($EntityLogicalName + "id")
        }

        try {
            $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.CRUDExtentions]::DeleteEntity($conn, $EntityLogicalName, $Id, [Guid]::Empty)
            if (!$result) {
                throw LastConnectorException($conn)
            }
        }
        catch {
            throw LastConnectorException($conn)
        }
    }
}

function Get-DataverseEntityMetadata {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1)]
        [string]$EntityLogicalName,
        [parameter(Mandatory = $false, Position = 2)]
        [string]$EntityFilters
    )
    
    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn')) 
    
    switch ($EntityFilters.ToLower()) {
        "all" {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::All
            break             
        }
        "attributes" {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::Attributes
            break 
        } 
        "entity" {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::Entity
            break
        }  
        "privileges" {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::Privileges
            break
        }  
        "relationships" {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::Relationships
            break
        }
        default {
            $filter = [Microsoft.Xrm.Sdk.Metadata.EntityFilters]::Default
            break
        }               
    }
    
    try {
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.MetadataExtensions]::GetEntityMetadata($conn, $EntityLogicalName, $filter)
        if ($result -eq $null) {
            throw LastConnectorException($conn)
        }
    }
    catch {
        throw LastConnectorException($conn)
    }    
    
    return $result
}

function Get-DataverseEntityAttributes {
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory = $false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory = $true, Position = 1)]
        [string]$EntityLogicalName
    )
        
    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn')) 
               
    try {
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.MetadataExtensions]::GetAllAttributesForEntity($conn, $EntityLogicalName)
        if ($result -eq $null) {
            throw LastConnectorException($conn)
        }
    }
    catch {
        throw LastConnectorException($conn)
    }    
        
    return $result
}

function New-DataverseEntityReference{
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
     [CmdletBinding()]
        PARAM(        
            [parameter(Mandatory=$true, Position=0)]
            [string]$EntityLogicalName,
            [parameter(Mandatory=$true, Position=1)]
            [guid]$Id
        )
        $DataverseEntityReference = [Microsoft.Xrm.Sdk.EntityReference]::new()
        $DataverseEntityReference.LogicalName = $EntityLogicalName
        $DataverseEntityReference.Id = $Id
        $DataverseEntityReference
        return
    }

#GetMyUserId
function Get-DataverseMyUserId{
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn
    )

	$conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))

    try
    {
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.GeneralExtensions]::GetMyUserId($conn)
		if($result -eq $null) 
        {
            throw LastConnectorException($conn)
        }
    }
    catch
    {
        throw LastConnectorException($conn)
    }    

    return $result
}

#GetEntityDataByFetchSearch
function Get-DataverseRecordsByFetch{
    # .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory=$true, Position=1)]
        [string]$Fetch,
        [parameter(Mandatory=$false, Position=2)]
        [int]$TopCount,
        [parameter(Mandatory=$false, Position=3)]
        [int]$PageNumber,
        [parameter(Mandatory=$false, Position=4)]
        [string]$PageCookie,
        [parameter(Mandatory=$false, Position=5)]
        [switch]$AllRows
    )
    $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    #default page number to 1 if not supplied
    if($PageNumber -eq 0)
    {
        $PageNumber = 1
    }
    $PagingCookie = ""
    $NextPage = $false
    if($PageCookie -eq "")
    {
        $PageCookie = $null
    }

    $recordslist = New-Object "System.Collections.Generic.List[System.Management.Automation.PSObject]"
    $fetchQueryTime = New-TimeSpan -Seconds 0
    $crmFetchTimer = [System.Diagnostics.Stopwatch]::StartNew()
    try 
    {
        $xml = [xml]$Fetch
        if($xml.fetch.count -ne 0 -and $TopCount -eq 0)
        {
            $TopCount = $xml.fetch.count
        }

        $logicalName = $xml.SelectSingleNode("/fetch/entity").Name

        do {
            Write-Debug "Fetching Page $PageNumber" 
            $crmFetchTimer.Restart()
            $records = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.QueryExtensions]::GetEntityDataByFetchSearch($conn,$Fetch, $TopCount, $PageNumber, $PageCookie, [ref]$PagingCookie, [ref]$NextPage, [Guid]::Empty)
            $fetchQueryTime += $crmFetchTimer.Elapsed

            if($conn.LastException)
            {
                throw LastConnectorException($conn)
            }

            $recordsList.AddRange([System.Collections.Generic.List[System.Management.Automation.PSObject]](parseRecordsPage -records $records -logicalname $logicalName -xml $xml -Verbose))

            $PageNumber = $PageNumber + 1
        } while ($NextPage -and $AllRows)
    }
    catch
    {
        Write-Error $_.Exception
        throw LastConnectorException($conn)
    }
    
    $resultSet = New-Object 'System.Collections.Generic.Dictionary[[System.String],[System.Management.Automation.PSObject]]'
    $resultSet.Add("Records", $recordslist)
    $resultSet.Add("Count", $recordslist.Count)
    $resultSet.Add("PagingCookie",$PagingCookie)
    $resultSet.Add("NextPage",$NextPage)
    $resultSet.Add("FetchXml", $Fetch)
    $resultSet.Add("FetchQueryTime", $fetchQueryTime) 
    Write-Verbose "FetchQueryTime:$fetchQueryTime" 
    $resultSet
}

function Get-DataverseOrgDbOrgSettings{
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml

    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn
    )

	$conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    
    $fetch = @"
    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" no-lock="true">
      <entity name="organization">
        <attribute name="orgdborgsettings" />
      </entity>
    </fetch>
"@
    $result = Get-DataverseRecordsByFetch -conn $conn -Fetch  $fetch
    $record = $result.Records[0]

    if($record.orgdborgsettings -eq $null)
    {
        Write-Warning 'No settings found.'
    }
    else
    {
        $xml = [xml]$record.orgdborgsettings
        return $xml.SelectSingleNode("/OrgSettings")
    }
}

function Invoke-DataverseAction {
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [OutputType([hashtable])]
    [OutputType([Microsoft.Xrm.Sdk.OrganizationResponse], ParameterSetName="Raw")]
    param (
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]
        $conn,

        [Parameter(
            Position=1,
            Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $Name,

        [Parameter(Position=2)]
        [hashtable]
        $Parameters,

        [Parameter(ValueFromPipeline, Position=3)]
        [ValidateNotNullOrEmpty()]
        [Microsoft.Xrm.Sdk.EntityReference]
        $Target,

        [Parameter(ParameterSetName="Raw")]
        [switch]
        $Raw
    )
    begin
    {
        $conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    }
    process
    {
		$request = new-object Microsoft.Xrm.Sdk.OrganizationRequest
		$request.RequestName = $Name
        if($Target) {
            $request.Parameters.Add("Target", $Target) 
        }

        if($Parameters) {
            foreach($parameter in $Parameters.GetEnumerator()) {
                $request.Parameters.Add($parameter.Name, $parameter.Value)
            }
        }

        try {
            $response = $conn.Execute($request)
        
            if($Raw) {
                Write-Output $response
            } elseif ($response.Results -and $response.Results.Count -gt 0) {
                $outputArguments = @{}
                foreach($outputArgument in $response.Results) {
                    $outputArguments.Add($outputArgument.Key, $outputArgument.Value)
                }
                Write-Output $outputArguments
            } else {
                Write-Output $null
            }
        }
        catch {
            Write-Error $_
        }
    }
}

function Get-DataverseSystemSettings{
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml

    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory=$false)]
        [switch]$ShowDisplayName
    )

	$conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
  
    $fetch = @"
    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" no-lock="true">
        <entity name="organization">
            <all-attributes />
        </entity>
    </fetch>
"@
   
    $record = (Get-DataverseRecordsByFetch -conn $conn -Fetch $fetch).Records[0]

    $attributes = Get-DataverseEntityAttributes -conn $conn -EntityLogicalName organization

    $psobj = New-Object -TypeName System.Management.Automation.PSObject
        
    foreach($att in $record.original.GetEnumerator())
    {
        if(($att.Key.Contains("Property")) -or ($att.Key -eq "organizationid") -or ($att.Key.StartsWith("ReturnProperty_")) -or ($att.Key -eq "logicalname") -or ($att.Key -eq "original"))
        {
            continue
        }
        if($att.Key -eq "defaultemailsettings")
        {
            if($ShowDisplayName)
            {
                $name = ($attributes | where {$_.LogicalName -eq $att.Key}).Displayname.UserLocalizedLabel.Label + ":" +((Get-DataverseEntityOptionSet -conn $conn mailbox incomingemaildeliverymethod).DisplayValue) 
            }
            else
            {
                $name = "defaultemailsettings:incomingemaildeliverymethod"
            }
            Add-Member -InputObject $psobj -MemberType NoteProperty -Name $name -Value ((Get-DataverseEntityOptionSet -conn $conn mailbox incomingemaildeliverymethod).Items.DisplayLabel)[([xml]$att.Value).FirstChild.IncomingEmailDeliveryMethod]
            if($ShowDisplayName)
            {
                $name = ($attributes | where {$_.LogicalName -eq $att.Key}).Displayname.UserLocalizedLabel.Label + ":" +((Get-DataverseEntityOptionSet -conn $conn mailbox outgoingemaildeliverymethod).DisplayValue) 
            }
            else
            {
                $name = "defaultemailsettings:outgoingemaildeliverymethod"
            }
            Add-Member -InputObject $psobj -MemberType NoteProperty -Name $name -Value ((Get-DataverseEntityOptionSet -conn $conn mailbox outgoingemaildeliverymethod).Items.DisplayLabel)[([xml]$att.Value).FirstChild.OutgoingEmailDeliveryMethod]
            if($ShowDisplayName)
            {
                $name = ($attributes | where {$_.LogicalName -eq $att.Key}).Displayname.UserLocalizedLabel.Label + ":" +((Get-DataverseEntityOptionSet -conn $conn mailbox actdeliverymethod).DisplayValue) 
            }
            else
            {
                $name = "defaultemailsettings:actdeliverymethod"
            }
            Add-Member -InputObject $psobj -MemberType NoteProperty -Name $name -Value ((Get-DataverseEntityOptionSet -conn $conn mailbox actdeliverymethod).Items.DisplayLabel)[([xml]$att.Value).FirstChild.ACTDeliveryMethod]
            continue
        }
        
        if($ShowDisplayName)
        {
            $name = ($attributes | where {$_.LogicalName -eq $att.Key}).Displayname.UserLocalizedLabel.Label 
            if($name -eq $null)
            {
                $name = ($attributes | where {$_.LogicalName -eq $att.Key}).SchemaName
            }
        }
        else
        {
            $name = ($attributes | where {$_.LogicalName -eq $att.Key}).SchemaName
        }

		if($name -eq $null){
			Write-Warning "SKIPPING Property: $($att.Key)"
		}
		else{
			Add-Member -InputObject $psobj -MemberType NoteProperty -Name $name -Value $record.($att.Key) -Force
		}
    }

    return $psobj
}

function Set-DataverseSystemSettings {
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
    [CmdletBinding()]
    PARAM(
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
		[parameter(Mandatory=$false)]
		[guid]$AcknowledgementTemplateId,
        [parameter(Mandatory=$false)]
        [int]$ACTDeliveryMethod,
        [parameter(Mandatory=$false)]
        [bool]$AllowAddressBookSyncs,
        [parameter(Mandatory=$false)]
        [bool]$AllowAutoResponseCreation,
        [parameter(Mandatory=$false)]
        [bool]$AllowAutoUnsubscribe,
        [parameter(Mandatory=$false)]
        [bool]$AllowAutoUnsubscribeAcknowledgement,
        [parameter(Mandatory=$false)]
        [bool]$AllowClientMessageBarAd,
        [parameter(Mandatory=$false)]
        [bool]$AllowEntityOnlyAudit,
        [parameter(Mandatory=$false)]
        [bool]$AllowMarketingEmailExecution,
        [parameter(Mandatory=$false)]
        [bool]$AllowOfflineScheduledSyncs,
        [parameter(Mandatory=$false)]
        [bool]$AllowOutlookScheduledSyncs,
        [parameter(Mandatory=$false)]
        [bool]$AllowUnresolvedPartiesOnEmailSend,
        [parameter(Mandatory=$false)]
		[bool]$AllowUserFormModePreference,
        [parameter(Mandatory=$false)]
        [bool]$AllowUsersSeeAppdownloadMessage,
        [parameter(Mandatory=$false)]
        [bool]$AllowWebExcelExport,
		[parameter(Mandatory=$false)]
        [string]$AMDesignator,
		[parameter(Mandatory=$false)]
        [bool]$AutoApplyDefaultonCaseCreate,
		[parameter(Mandatory=$false)]
        [bool]$AutoApplyDefaultonCaseUpdate,
		[parameter(Mandatory=$false)]
        [bool]$AutoApplySLA,
		[parameter(Mandatory=$false)]
        [string]$BingMapsApiKey,
        [parameter(Mandatory=$false)]
        [string]$BlockedAttachments,
		[parameter(Mandatory=$false)]
        [guid]$BusinessClosureCalendarId,
        [parameter(Mandatory=$false)]
        [string]$CampaignPrefix,
		[parameter(Mandatory=$false)]
        [bool]$CascadeStatusUpdate,
        [parameter(Mandatory=$false)]
        [string]$CasePrefix,
        [parameter(Mandatory=$false)]
        [string]$ContractPrefix,
		[parameter(Mandatory=$false)]
        [bool]$CortanaProactiveExperienceEnabled,
		[parameter(Mandatory=$false)]
        [bool]$CreateProductsWithoutParentInActiveState,
		[parameter(Mandatory=$false)]
        [int]$CurrencyDecimalPrecision,
        [parameter(Mandatory=$false)]
        [int]$CurrencyDisplayOption,
        [parameter(Mandatory=$false)]
        [int]$CurrentCampaignNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentCaseNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentContractNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentInvoiceNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentKbNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentOrderNumber,
        [parameter(Mandatory=$false)]
        [int]$CurrentQuoteNumber,
        [parameter(Mandatory=$false)]
        [ValidatePattern('\+{1}\d{1,}')]
        [string]$DefaultCountryCode,
        [parameter(Mandatory=$false)]
        [guid]$DefaultEmailServerProfileId,
        [parameter(Mandatory=$false)]
        [bool]$DisableSocialCare,
        [parameter(Mandatory=$false)]
        [bool]$DisplayNavigationTour,
        [parameter(Mandatory=$false)]
        [int]$EmailConnectionChannel, 
        [parameter(Mandatory=$false)]
        [int]$EmailCorrelationEnabled, 
        [parameter(Mandatory=$false)]
        [bool]$EnableBingMapsIntegration,
        [parameter(Mandatory=$false)]
        [bool]$EnableSmartMatching,
        [parameter(Mandatory=$false)]
        [int]$FullNameConventionCode,
        [parameter(Mandatory=$false)]
        [bool]$GenerateAlertsForErrors,
        [parameter(Mandatory=$false)]
        [bool]$GenerateAlertsForWarnings,
        [parameter(Mandatory=$false)]
        [bool]$GenerateAlertsForInformation,
        [parameter(Mandatory=$false)]
        [bool]$GlobalAppendUrlParametersEnabled,
        [parameter(Mandatory=$false)]
        #[ValidatePattern('http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?')]
        [string]$GlobalHelpUrl,
        [parameter(Mandatory=$false)]
        [bool]$GlobalHelpUrlEnabled,
        [parameter(Mandatory=$false)]
        [int]$HashDeltaSubjectCount,
        [parameter(Mandatory=$false)]
        [string]$HashFilterKeywords,
        [parameter(Mandatory=$false)]
        [int]$HashMaxCount,
        [parameter(Mandatory=$false)]
        [int]$HashMinAddressCount,
        [parameter(Mandatory=$false)]
        [bool]$IgnoreInternalEmail,
        [parameter(Mandatory=$false)]
        [int]$IncomingEmailDeliveryMethod,
        [parameter(Mandatory=$false)]
        [string]$InvoicePrefix,
        [parameter(Mandatory=$false)]
        [bool]$IsAutoSaveEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsDefaultCountryCodeCheckEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsDuplicateDetectionEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsDuplicateDetectionEnabledForImport,
        [parameter(Mandatory=$false)]
        [bool]$IsDuplicateDetectionEnabledForOfflineSync,
        [parameter(Mandatory=$false)]
        [bool]$IsDuplicateDetectionEnabledForOnlineCreateUpdate,
        [parameter(Mandatory=$false)]
        [bool]$isenabledforallroles,
        [parameter(Mandatory=$false)]
        [bool]$IsFolderBasedTrackingEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsFullTextSearchEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsHierarchicalSecurityModelEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsPresenceEnabled,
        [parameter(Mandatory=$false)]
        [bool]$IsUserAccessAuditEnabled,
        [parameter(Mandatory=$false)]
        [string]$KbPrefix,
        [parameter(Mandatory=$false)]
        [int]$MaxAppointmentDurationDays,
        [parameter(Mandatory=$false)]
        [int]$MaxDepthForHierarchicalSecurityModel,
        [parameter(Mandatory=$false)]
        [int]$MaximumActiveBusinessProcessFlowsAllowedPerEntity,
        [parameter(Mandatory=$false)]
        [int]$MaximumDynamicPropertiesAllowed,
        [parameter(Mandatory=$false)]
        [int]$MaximumTrackingNumber,
        [parameter(Mandatory=$false)]
        [int]$MaxProductsInBundle,
        [parameter(Mandatory=$false)]
        [int]$MaxRecordsForExportToExcel,
        [parameter(Mandatory=$false)]
        [int]$MaxRecordsForLookupFilters,
        [parameter(Mandatory=$false)]
        [int]$MaxUploadFileSize,
        [parameter(Mandatory=$false)]
        [int]$MinAddressBookSyncInterval,
        [parameter(Mandatory=$false)]
        [int]$MinOfflineSyncInterval,
        [parameter(Mandatory=$false)]
        [int]$MinOutlookSyncInterval,
        [parameter(Mandatory=$false)]
        [bool]$NotifyMailboxOwnerOfEmailServerLevelAlerts,
        [parameter(Mandatory=$false)]
        [string]$OrderPrefix,
        [parameter(Mandatory=$false)]
        [int]$OutgoingEmailDeliveryMethod,
        [parameter(Mandatory=$false)]
        [ValidateSet(0,1,2)]
        [int]$PluginTraceLogSetting,
        [parameter(Mandatory=$false)]
        [ValidateSet(0,1,2,3,4)]
        [int]$PricingDecimalPrecision,
        [parameter(Mandatory=$false)]
        [bool]$QuickFindRecordLimitEnabled,
        [parameter(Mandatory=$false)]
        [string]$QuotePrefix,
        [parameter(Mandatory=$false)]
        [bool]$RequireApprovalForUserEmail,
        [parameter(Mandatory=$false)]
        [bool]$RequireApprovalForQueueEmail,
        [parameter(Mandatory=$false)]
        [bool]$ShareToPreviousOwnerOnAssign,
        [parameter(Mandatory=$false)]
        [string]$TrackingPrefix,
        [parameter(Mandatory=$false)]
        [int]$TrackingTokenIdBase,
        [parameter(Mandatory=$false)]
        [int]$TrackingTokenIdDigits,
        [parameter(Mandatory=$false)]
        [int]$UniqueSpecifierLength,
        [parameter(Mandatory=$false)]
        [bool]$UseLegacyRendering,
        [parameter(Mandatory=$false)]
        [bool]$UsePositionHierarchy,
        [parameter(Mandatory=$false)]
        [bool]$UseSkypeProtocol,
		[parameter(Mandatory=$false)]
		[bool]$UseAllowUsersSeeAppdownloadMessage,
		[parameter(Mandatory=$false)]
		[string]$DefaultCrmCustomName,
		[parameter(Mandatory=$false)]
		[bool]$SuppressSLA,
		[parameter(Mandatory=$false)]
		[bool]$IsAuditEnabled,
		[parameter(Mandatory=$false)]
		[bool]$AllowLegacyClientExperience
    )

	$conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    
    $updateFields = @{}

    $attributesMetadata = Get-DataverseEntityAttributes -conn $conn -EntityLogicalName organization
        
    $defaultEmailSettings = @{}        

    foreach($parameter in $MyInvocation.BoundParameters.GetEnumerator())
    {   
        $attributeMetadata = $attributesMetadata | ? {$_.SchemaName -eq $parameter.Key}

        if($parameter.Key -in ("IncomingEmailDeliveryMethod","OutgoingEmailDeliveryMethod","ACTDeliveryMethod"))
        {
            $defaultEmailSettings.Add($parameter.Key,$parameter.Value)
        }
        elseif($attributeMetadata -eq $null)
        {
            continue
        }
        elseif($attributeMetadata.AttributeType -eq "Picklist")
        {
            $updateFields.Add($parameter.Key.ToLower(), (New-DataverseOptionSetValue $parameter.Value))
        }
        elseif($attributeMetadata.AttributeType -eq "Lookup")
        {
            $updateFields.Add($parameter.Key.ToLower(), (New-DataverseEntityReference emailserverprofile $parameter.Value))
        }
        else
        {
            $updateFields.Add($parameter.Key.ToLower(), $parameter.Value)
        }
    }
    
    $fetch = @"
    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" no-lock="true">
        <entity name="organization">
            <attribute name="organizationid" />
            <attribute name="defaultemailsettings" />
        </entity>
    </fetch>
"@

    $systemSettings = (Get-DataverseRecordsByFetch -conn $conn -Fetch $fetch).Records[0]
    $recordid = $systemSettings.organizationid

    if($defaultEmailSettings.Count -ne 0)
    {        
        $emailSettings = [xml]$systemSettings.defaultemailsettings
        if($defaultEmailSettings.ContainsKey("IncomingEmailDeliveryMethod"))
        {
            $emailSettings.EmailSettings.IncomingEmailDeliveryMethod = [string]$defaultEmailSettings["IncomingEmailDeliveryMethod"]
        }
        if($defaultEmailSettings.ContainsKey("OutgoingEmailDeliveryMethod"))
        {
            $emailSettings.EmailSettings.OutgoingEmailDeliveryMethod = [string]$defaultEmailSettings["OutgoingEmailDeliveryMethod"]
        }
        if($defaultEmailSettings.ContainsKey("ACTDeliveryMethod"))
        {
            $emailSettings.EmailSettings.ACTDeliveryMethod = [string]$defaultEmailSettings["ACTDeliveryMethod"]
        }

        $updateFields.Add("defaultemailsettings",$emailSettings.OuterXml)
    }

    Set-DataverseRecord -conn $conn -EntityLogicalName organization -Id $recordid -Fields $updateFields
}

#GetPickListElementFromMetadataEntity   
function Get-DataverseEntityOptionSet{
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
 [CmdletBinding()]
    PARAM( 
        [parameter(Mandatory=$false)]
        [Microsoft.PowerPlatform.Dataverse.Client.ServiceClient]$conn,
        [parameter(Mandatory=$true, Position=1)]
        [string]$EntityLogicalName,
        [parameter(Mandatory=$true, Position=2)]
        [string]$FieldLogicalName
    )
	$conn = VerifyConnectionParam -conn $conn -pipelineValue ($PSBoundParameters.ContainsKey('conn'))
    try
    {
        $result = [Microsoft.PowerPlatform.Dataverse.Client.Extensions.MetadataExtensions]::GetPickListElementFromMetadataEntity($conn, $EntityLogicalName, $FieldLogicalName)
		if($result -eq $null)
        {
            throw LastConnectorException($conn)
        }
    }
    catch
    {
        throw LastConnectorException($conn)
    }

    return $result
}


#UtilityFunctions
function MapFieldTypeByFieldValue {
    PARAM(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    $valueTypeToDvTypeMapping = @{
        "Boolean"         = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Boolean;
        "DateTime"        = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::DateTime;
        "Decimal"         = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Decimal;
        "Single"          = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Float;
        "Money"           = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw;
        "Int32"           = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Number;
        "EntityReference" = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw;
        "OptionSetValue"  = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw;
        "String"          = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::String;
        "Guid"            = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::UniqueIdentifier;
    }

    # default is RAW
    $DvDataType = [Microsoft.PowerPlatform.Dataverse.Client.DataverseFieldType]::Raw

    if ($Value -ne $null) {

        $valueType = $Value.GetType().Name
        
        if ($valueTypeToDvTypeMapping.ContainsKey($valueType)) {
            $DvDataType = $valueTypeToDvTypeMapping[$valueType]
        }   
    }

    return $DvDatatype
}

function New-DataverseOptionSetValue{
# .ExternalHelp Microsoft.Xrm.Data.PowerShell.Help.xml
 [CmdletBinding()]
    PARAM(        
        [parameter(Mandatory=$true, Position=0)]
        [int]$Value
    )
    $crmOptionSetValue = [Microsoft.Xrm.Sdk.OptionSetValue]::new()
    $crmOptionSetValue.Value = $Value
    $crmOptionSetValue
    return
}

function GuessPrimaryKeyField() {
    PARAM(
        [Parameter(Mandatory = $true)]
        [object]$EntityLogicalName
    )

    $standardActivityEntities = @(
        "opportunityclose",
        "socialactivity",
        "campaignresponse",
        "letter", "orderclose",
        "appointment",
        "recurringappointmentmaster",
        "fax",
        "email",
        "activitypointer",
        "incidentresolution",
        "bulkoperation",
        "quoteclose",
        "task",
        "campaignactivity",
        "serviceappointment",
        "phonecall"
    )
    # Some Entity has different pattern for id name.
    if ($EntityLogicalName -eq "usersettings") {
        $primaryKeyField = "systemuserid"
    }
    elseif ($EntityLogicalName -eq "systemform") {
        $primaryKeyField = "formid"
    }
    elseif ($EntityLogicalName -in $standardActivityEntities) {
        $primaryKeyField = "activityid"
    }
    else {
        # default
        $primaryKeyField = $EntityLogicalName + "id"
    }
    
    $primaryKeyField
}

function parseRecordsPage {
    PARAM( 
        [parameter(Mandatory=$true)]
        [object]$records,
        [parameter(Mandatory=$true)]
        [string] $logicalname,
        [parameter(Mandatory=$true)]
        [xml] $xml
    )
    $recordslist = New-Object 'System.Collections.Generic.List[System.Management.Automation.PSObject]'
    foreach($record in $records.Values){   
        $null = $record.Add("original",$record)
        $null = $record.Add("logicalname",$logicalname)
        if($record.ContainsKey("ReturnProperty_Id "))
        {
            $null = $record.Add("ReturnProperty_Id",$record.'ReturnProperty_Id ')
            $null = $record.Remove("ReturnProperty_Id ")
        }
        #add entityReferences values as values 
        ForEach($attribute in $record.Keys|Select)
        {
            if(-not $attribute.EndsWith("_Property")) { continue }
            
            #if aliased value BUT if it's an EntityRef... then ignore it 
            if($record[$attribute].Value -is [Microsoft.Xrm.Sdk.AliasedValue])
            {
                if($record[$attribute].Value.Value -isnot [Microsoft.Xrm.Sdk.EntityReference])
                {
                    $attName = $attribute.Replace("_Property","")
                    $record[$attName] = $record[$attribute].Value.Value
                }
            }

            if($record[$attribute].Value -is [Microsoft.Xrm.Sdk.EntityReference])
            {
                $attName = $attribute.Replace("_Property","")
                $record[$attName] = $record[$attribute].Value.Name
            }
        }
      
        $hashtable = $record -as [Hashtable]

        #adding Dynamic EntityReference
        if ($hashtable.ReturnProperty_Id -and $hashtable.ReturnProperty_EntityName) {
            $hashtable.EntityReference = New-DataverseEntityReference -EntityLogicalName $hashtable.ReturnProperty_EntityName -Id $hashtable.ReturnProperty_Id
        }

        $recordslist.Add([pscustomobject]$hashtable)
    }
    $recordslist
}

function Coalesce {
	foreach($i in $args){
		if($i -ne $null){
			return $i
		}
	}
}



