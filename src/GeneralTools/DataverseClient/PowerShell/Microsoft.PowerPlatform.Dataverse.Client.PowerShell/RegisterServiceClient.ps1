<#
This script will load in a PowerShell command shell and import the module developed in the project. To clean up, exit this shell.
#>

# Load the module.
$env:PSModulePath = (Resolve-Path .).Path + ";" + $env:PSModulePath
Import-Module 'Microsoft.PowerPlatform.Dataverse.Client.PowerShell' -Verbose


