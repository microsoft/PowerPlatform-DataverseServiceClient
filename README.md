## NOTICE **Project renamed to Microsoft.PowerPlatform.Dataverse.Client.***
Please see [project rename](https://github.com/microsoft/PowerPlatform-DataverseServiceClient/discussions/103) for more information


## Change Log
Current release notes and change log:

[Microsoft.PowerPlatform.Dataverse.Client](src/nuspecs/Microsoft.PowerPlatform.Dataverse.Client.ReleaseNotes.txt)

[Microsoft.PowerPlatform.Dataverse.Client.Dynamics](src/nuspecs/Microsoft.PowerPlatform.Dataverse.Client.Dynamics.ReleaseNotes.txt)

This nuget package has been deprecated (for now) ~~[Microsoft.Dynamics.Sdk.Messages](src/nuspecs/Microsoft.Dynamics.Sdk.Messages.ReleaseNotes.txt)~~

## Overview
This repository contains the code for the Microsoft.PowerPlatform.Dataverse.Client and its supporting assemblies and classes. 

**IMPORTANT NOTES**

**The Dataverse ServiceClient is in Preview**

**The Dataverse ServiceClient cannot be built outside of Microsoft** 
This is due to a set of dependencies on nuget packages that are internally available only.  At some point in the future, we will expose the supporting nuget packages when we have updated our server infrastructure to support plugin development on .net core.

This encompasses the contents of the following nuget packages:

[Microsoft.PowerPlatform.Dataverse.Client](https://www.nuget.org/packages/Microsoft.PowerPlatform.Dataverse.Client)

[Microsoft.PowerPlatform.Dataverse.Client.Dynamics](https://www.nuget.org/packages/Microsoft.PowerPlatform.Dataverse.Client.Dynamics)

This nuget package has been deprecated (for now) ~~[Microsoft.Dynamics.Sdk.Messages](https://www.nuget.org/packages/Microsoft.Dynamics.Sdk.Messages)~~


This library is and its supporting assemblies are a revision and update of the Microsoft.Xrm.Tooling.Connector.CrmServiceClient and the underlying Microsoft.Xrm.Sdk.Client libraries. 

We are using this effort to for a few key things we have wanted to get done for a number of years, 

1. Refactor and update our client libraries to allow us to spit up PowerPlatform Common Data Service SDK support from Microsoft Dynamics 365.
2. Provide multi targeted library build that targets our supported .net client platforms.
3. Update connection patterns and behaviors to be consistent with many of the broadly accepted patterns.
4. Create a pattern to allow developers focus on the use of Dataverse, or Dataverse + Dynamics as they need. 

We encourage you to read the release notes we provide with each nuget packages. As with most of our Nuget packages that are intended as tools or for developer consumption, we extensively comment in release notes. 

At this time: (03/06/2022)
The Client SDK libs supports the following and has the following notices: 

* 0.6.x is **expected** to be the final preview release, followed by 1.0
* 0.6.x refactors much of the primary ServiceClient interface, narrowing its focus to primary operations against Dataverse. the ballance of the feature set has been moved to Microsoft.PowerPlatform.Dataverse.Client.Extensions. - We are seeking feedback on this refactor. 
* .net full framework 4.6.2, 4.7.2, 4.8 and .net core 3.0, 3.1, 5.0, 6.0
* We now support all authentication types from CrmServiceClient for .net framework, ( Client\Secret, Client\Cert, UID\PW Noninteractive, UID\PW interactive.)
* We support the following authentication types from CrmServiceClient for .net core: Client\Secret, Client\Cert, UID\PW interactive.
* MSAL Port has been completed,  this Lib is now using MSAL 4.35+
* Plugin Development using this Client is NOT supported at this time. 

From a scenario point of view,  we are particularity interested in any issues or challenges when using these library in either Asp.net Core, Azure Functions, and Linux based scenarios. 
 
If your working against Dataverse Only and or custom entities and sdk messages, you should only need Microsoft.PowerPlatform.Dataverse.Client.  If you do not experience that, or find missing messages in the Dataverse only scenarios, please let us know in the issues area. 
 
<b>Note: We are currently providing support for these nuget packages primarily via GitHub and Microsoft Support.  
Github Issues is the preferred venue at this time as the development team is actively working on this library. 
A number of our dev's and PM's do monitor this channel and can respond to questions and feedback. 

While we are monitoring the community forums,  you are encouraged to open issue [here](https://github.com/microsoft/PowerPlatform-CdsServiceClient/issues) 
</b>

## Samples / Docs
Samples and such will be updated overtime on the PowerApps Samples GitHub Site as we move forward with the evolution of this capability. That said, any of the existing CrmServiceClient samples can be used as a base to start. You can find those here: [Samples](https://github.com/microsoft/PowerApps-Samples/tree/master/cds/orgsvc/C%23)

For connections strings, docs on supported patterns are here: [Connection String Docs](https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/xrm-tooling/use-connection-strings-xrm-tooling-connect)

Microsoft Documentation root for Dataverse ServiceClient can be found here: [Dataverse ServiceClient Docs](https://docs.microsoft.com/en-us/dotnet/api/microsoft.powerplatform.dataverse.client?view=dataverse-sdk-latest)

## Contributing
This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## License

[MIT](LICENSE)
