//===================================================================================
// Microsoft – subject to the terms of the Microsoft EULA and other agreements
// Microsoft.PowerPlatform.Dataverse.WebResourceUtility
// copyright 2003-2012 Microsoft Corp.
//
// Defines the WebResource Types
//
//===================================================================================

namespace Microsoft.PowerPlatform.Dataverse.WebResourceUtility
{
	using System;

	/// <summary>
	/// Format of the WebResource
	/// </summary>
	public enum WebResourceWebResourceType
	{
		/// <summary>
		/// Html type
		/// </summary>
		Webpage_HTML = 1,
		/// <summary>
		/// CSS
		/// </summary>
		StyleSheet_CSS = 2,
		/// <summary>
		/// JScript
		/// </summary>
		Script_JScript = 3,
		/// <summary>
		/// XML
		/// </summary>
		Data_XML = 4,
		/// <summary>
		/// PNG
		/// </summary>
		PNGformat = 5,
		/// <summary>
		/// JPG
		/// </summary>
		JPGformat = 6,
		/// <summary>
		/// GIF
		/// </summary>
		GIFformat = 7,
		/// <summary>
		/// XAP
		/// </summary>
		Silverlight_XAP = 8,
		/// <summary>
		/// XSL
		/// </summary>
		StyleSheet_XSL = 9,
		/// <summary>
		/// ICO
		/// </summary>
		ICOformat = 10,
	}
}
