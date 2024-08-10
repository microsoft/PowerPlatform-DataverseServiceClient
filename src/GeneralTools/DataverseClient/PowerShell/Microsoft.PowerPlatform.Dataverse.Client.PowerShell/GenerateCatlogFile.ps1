<#
This script will create the catalog file from the signed assembly in a PowerShell command shell and drops the files in the drop folder for the signing. .
#>

[CmdletBinding(PositionalBinding=$true)]
param(
    [string] $BuildSourcesDirectory ,
	[string] $BuildConfiguration,
	[string] $StagingDirectory,
	[bool] $RunFromVSBuild = $false,
	[bool] $EnableDebug = $false
    )

$SolutionName = "Microsoft.PowerPlatform.Dataverse.Client.PowerShell"
Write-Host ">>> ========================= Invoking GenerateCatalogFile.ps1 for $SolutionName ======================="
Write-Host ">>> BuildSourcesDirectory = $BuildSourcesDirectory"
Write-Host ">>> SolutionName = $SolutionName"
Write-Host ">>> BuildConfiguration = $BuildConfiguration"
Write-Host ">>> StagingDirectory = $StagingDirectory"
Write-Host ">>> RunFromVSBuild = $RunFromVSBuild"
Write-Host ">>> Write Debug Info = $EnableDebug"

Write-Host ">>> VERSION INFO:"
$PSVersionTable

Write-Host ">>> LOADING Microsoft.Powershell.Security"
Import-Module Microsoft.Powershell.Security -Verbose

if ( [System.String]::IsNullOrEmpty($StagingDirectory) -eq $true)
{
    #Running local build
    $StagingDirectory = $BuildSourcesDirectory
}

#Create path for drop directory
$dropFolderName = "Drop"
$dropPath = [System.IO.Path]::Combine($StagingDirectory , $dropFolderName, $SolutionName)
Write-Host ">>> Output path is $dropPath"

$catalogFilePath = "$dropPath\Microsoft.PowerPlatform.Dataverse.Client.PowerShell.cat"

Write-Host ">>> CatalogFile path is $catalogFilePath"

$isExists = Get-Item -LiteralPath "$catalogFilePath" -ErrorAction SilentlyContinue
if ($null -ne $isExists)
{
    Remove-Item $catalogFilePath -Force
}

New-FileCatalog -Path "$dropPath" -CatalogFilePath $catalogFilePath -CatalogVersion 1.0