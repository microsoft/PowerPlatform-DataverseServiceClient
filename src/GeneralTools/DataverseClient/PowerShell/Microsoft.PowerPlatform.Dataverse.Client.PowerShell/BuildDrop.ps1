#Build Drop Script
#Drops the files in the drop folder for the build. 
[CmdletBinding(PositionalBinding=$true)] 
param(
    [string] $BuildSourcesDirectory , 
	[string] $BuildConfiguration, 
	[string] $StagingDirectory,
	[string] $ProjectRootDirectory,
	[string] $SolutionName,
	[bool] $RunFromVSBuild = $false,
	[bool] $EnableDebug = $false
	
    )

#BuildDrop for Microsoft.Xrm.OnlineManagementAPI solution
Write-Host ">>> ========================= Invoking BuildDrop.ps1 for $SolutionName ======================="
Write-Host ">>> BuildSourcesDirectory = $BuildSourcesDirectory"
Write-Host ">>> ProjectRootDirectory = $ProjectRootDirectory"
Write-Host ">>> SolutionName = $SolutionName"
Write-Host ">>> BuildConfiguration = $BuildConfiguration"
Write-Host ">>> StagingDirectory = $StagingDirectory"
Write-Host ">>> RunFromVSBuild = $RunFromVSBuild"
Write-Host ">>> Write Debug Info = $EnableDebug"


if ( [System.String]::IsNullOrEmpty($StagingDirectory) -eq $true)
{
    #Running local build
    $StagingDirectory = $BuildSourcesDirectory
}

if ( $RunFromVSBuild -eq $false )
{
	$dropFolderName = "Drop"
}
else 
{
	$dropFolderName = "Drop"
}

$SolutionName = "Microsoft.PowerPlatform.Dataverse.Client.PowerShell";
#Create path for drop directory
#format for Local Build: Root/Drop/Buildconfig/SolutionName/Bins. 
#format for Server Build: Root/Buildconfig/SolutionName/Bins. 
if($RunFromVSBuild -eq $false)
{
	#$dropPath = [System.IO.Path]::Combine($dropFolderName , $SolutionName )
	$dropPath = [System.IO.Path]::Combine($dropFolderName  )
}
else
{
	$dropPath = [System.IO.Path]::Combine($StagingDirectory , $dropFolderName , $BuildConfiguration)
}
Write-Host ">>> Output path is $dropPath"

## Assembly Out directory 
if($RunFromVSBuild -eq $false)
{
	$BinsDirectory = $StagingDirectory #[System.IO.Path]::Combine($ProjectRootDirectory , $SolutionName , "bin" , $BuildConfiguration )
}
else
{
	$BinsDirectory = [System.IO.Path]::Combine($BuildSourcesDirectory , "bin" , $BuildConfiguration, "DataverseClient" , "net8.0" )
}
## Copying PowerShell Module out only. 
Write-Host ">>> BINS path is $BinsDirectory"

# Setup Module Drop Directory Key
if($RunFromVSBuild -eq $false)
{
	$PowerShellModuleFilesDirectory = [System.IO.Path]::Combine($dropPath , $SolutionName)
}
else {
	$PowerShellModuleFilesDirectory = [System.IO.Path]::Combine($dropPath , $SolutionName)
}
Write-Host ">>> Module Drop path is $PowerShellModuleFilesDirectory"


## ##############  Project or Solution COPY code here.  ############ ##
#create the Root Drop directory
New-Item -ItemType directory -Force $dropPath 
##create subfolder for Management Powershell
New-Item -ItemType Directory -Force $PowerShellModuleFilesDirectory

if ( [System.IO.Directory]::Exists($PowerShellModuleFilesDirectory) -eq $true )
{
	#copy launcher.
	#Copy-Item -Path "$BinsDirectory\*" -Destination $dropFolderName -Include 'RegisterXrmTooling.ps1' -Force
	Robocopy $BinsDirectory $dropPath 'RegisterServiceClient.ps1' /XX
	if ($lastexitcode -le 7) { 
		Write-Host ">>> ExitCode = " $lastexitcode
		$lastexitcode = 0 
	}

	# remove anything from Target so as to not upset robocopy

	#copy modules. 
	Robocopy ([System.IO.Path]::Combine($BinsDirectory , 'Microsoft.PowerPlatform.Dataverse.Client.PowerShell')) $PowerShellModuleFilesDirectory *.* /XX
	if ($lastexitcode -le 7) { 
		Write-Host ">>> ExitCode = " $lastexitcode
		$lastexitcode = 0 
	}
	#copy DLL's 
	Robocopy $BinsDirectory $PowerShellModuleFilesDirectory *.dll
	if ($lastexitcode -le 7) 
	{ 
		Write-Host ">>> ExitCode1 = " $lastexitcode
		$lastexitcode = 0 
	}
	#copy Help 
	Robocopy $BinsDirectory $PowerShellModuleFilesDirectory *.dll-help.xml
	if ($lastexitcode -le 7) 
	{ 
		Write-Host ">>> ExitCode1 = " $lastexitcode
		$lastexitcode = 0 
		Exit 0
	}


}
