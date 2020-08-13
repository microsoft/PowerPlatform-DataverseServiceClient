## Current Release Notes
Current release notes:

[Microsoft.Powerplatform.Cds.Client](src/nuspecs/Microsoft.Powerplatform.Cds.Client.ReleaseNotes.txt)

[Microsoft.Powerplatform.Cds.Client.Dynamics](src/nuspecs/Microsoft.Powerplatform.Cds.Client.Dynamics.ReleaseNotes.txt)

[Microsoft.Dynamics.Sdk.Messages](src/nuspecs/Microsoft.Dynamics.Sdk.Messages.ReleaseNotes.txt)

## Overview
This repository contains the code for the Microsoft.PowerPlatform.Cds.Client and its supporting assemblies and classes. 

**IMPORTANT NOTE**

**The CdsServiceClient cannot be built outside of Microsoft** 
This is due to a set of dependencies on nuget packages that are internally available only.  At some point in the future, we will expose the supporting nuget packages when we have updated our server infrastructure to support plugin development on .net core.


This encompases the contents of the following nuget packages:

[Microsoft.PowerPlatform.Cds.Client](https://www.nuget.org/packages/Microsoft.PowerPlatform.Cds.Client)
-
[Microsoft.PowerPlatform.Cds.Client.Dynamics](https://www.nuget.org/packages/Microsoft.PowerPlatform.Cds.Client.Dynamics)
-
[Microsoft.Dynamics.Sdk.Messages](https://www.nuget.org/packages/Microsoft.Dynamics.Sdk.Messages)


This library is and its supporting assemblies are a revision and update of the Microsoft.Xrm.Tooling.Connector.CrmServiceClient and the underlying Microsoft.Xrm.Sdk.Client libraries. 

We are using this effort to for a few key things we have wanted to get done for a number of years, 
1. Refactor and update our client libraries to allow us to spit up Powerplatform Common Data Service SDK support from Microsoft Dynamics 365.
2. Provide multi targeted library build that targets our supported .net client platforms.
3. Update connection patterns and behaviors to be consistent with many of the broadly accepted patterns.
4. Create a pattern to allow developers focus on the use of Common Data Service, or CDS + Dynamics as they need. 

<b>We are currently in ALPHA.</b> 
What does this mean? 
Alpha means that we can and will change the api surface, namepaces and behavior from time to time. this is mostly driven by feedback from users of the client (you).   During this process we are also working though ports of many internal tools and will adjust  things based on the needs of those tools were it make sense. 

We encourage you to read the release notes we provide with each nuget packages. As with most of our Nuget packages that are intended as tools or for developer consumption, we extensively comment in release notes. 

At this time: 
The alpha version of the SDK libs supports the following and has the following notices: 

* .net full framework 4.6.2, 4.7.2, 4.8 and .net core 3.0, 3.1 
* We are supporting only Client Secret / Cert flows for .net core.  On .net framework additionally we support OAuth User flows
* We are shipping the initial drop using ADAL.net for the Auth Lib.   We MAY change to use MSAL shortly 
* MSAL would enabled us to support User Interactive flows on .net core. 
* The Namespaces of areas WILL change.  We do not know what they will change too just yet but they will change before we “release” this.
* The Assembly Names Will likely change at least once more. 
* The Message types that are part of the client have been reduced to CDS Core server messages only.  Things like “QualifyLeadRequest” have been removed to their own Nuget package ( Microsoft.Dynamics.Sdk.Messages ) 
* We will likely ship more extension packages that will contain the “CRM” messages,  though over time, we will likely split the namespaces of those messages up based on service line,  think Field Service or Sales or Customer Service, etc..
* Plugin Development using this Client is NOT supported at this time. 

From a scenario point of view,  we are particularity interested in any issues or challenges when using these library in either Asp.net Core or Functions scenarios. 
 
We believe forthe vast majorty of applications working against the older dynamices sdk libs, you should only need Microsoft.Powerplatform.Cds.Client and Microsoft.Dynamics.Sdk.Messages.

If your working against CDS Only and or custom entities and sdk messages, you should only need Microsoft.Powerplatform.Cds.Client.  If you do not experience that, or find missing messages in the CDS only scenarios, please let us know. 

 
<b>Note: Please be aware that during the Alpha \ Beta phases, these nuget packages are not supported via official channels. 
Support is aware of them, however you will likely be directed back to the community forums for support during the Alpha \ Beta phases.  A number of our dev's and PM's do monitor this channel and can respond to questions and feedback. 

While we are monitoring the community forums,  you are encouraged to open issue [here](https://github.com/microsoft/PowerPlatform-CdsServiceClient/issues) 
</b>

## Samples / Docs
Samples and such will be updated overtime on the PowerApps Samples GitHub Site as we move forward with the evolution of this capability. That said, any of the existing CrmServiceClient samples can be used as a base to start. You can find those here: [Samples](https://github.com/microsoft/PowerApps-Samples/tree/master/cds/orgsvc/C%23)

For connections strings, docs on supported patterns are here: [Connection String Docs](https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/xrm-tooling/use-connection-strings-xrm-tooling-connect)

For General docs on the CrmServiceClient, which is what CdsServiceClient is modeled on: [CrmServiceClient Docs](https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/xrm-tooling/build-windows-client-applications-xrm-tools)

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
