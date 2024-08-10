Push-Location $PSScriptRoot

$PackageRoot = $PSScriptRoot

$LoadingModule = $true

dir *.ps1 | % Name | Resolve-Path | Import-Module 

$LoadingModule = $false

Pop-Location
