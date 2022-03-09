//===================================================================================
// <copyright file="ImageResources.cs" company="Microsoft">
//		eService Accelerator V1.0
//		Copyright 2003-2012 Microsoft Corp  All rights reserved.
// </copyright>
// Microsoft – subject to the terms of the Microsoft EULA and other agreements
// Retrieve image resources from CRM for WPF Objects
//===================================================================================

namespace Microsoft.PowerPlatform.Dataverse.WebResourceUtility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Windows.Media.Imaging;
    using Microsoft.PowerPlatform.Dataverse.Client;
    using System.IO;
    using System.Diagnostics;
    using Microsoft.Xrm.Sdk;
    using Microsoft.PowerPlatform.Dataverse.Client.Extensions;

    /// <summary>
    /// Web Resource actions for dealing with Image Resources. 
    /// </summary>
    public class ImageResources
    {
        #region Vars

        /// <summary>
        /// Dataverse Connection
        /// </summary>
        private ServiceClient _serviceClient;

        /// <summary>
        /// Tracer
        /// </summary>
        private TraceLogger _logEntry = null;

        #endregion

        /// <summary>
        /// Constructs a class used to retrieve image resources from CRM..
        /// </summary>
        /// <param name="serviceClient">Initialized copy of a ServiceClient object</param>
        public ImageResources(ServiceClient serviceClient)
        {
            _serviceClient = serviceClient;
            _logEntry = new TraceLogger(string.Empty);
        }

        /// <summary>
        /// Returns BitMap Image Resource from CRM 
        /// </summary>
        /// <param name="webResourceName">Image Resource Name requested</param>
        /// <returns>Returns Null if the Image is not found, or the resource type is not an Image.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is done to support disposing of the stream and its resource by the calling method")]
        public BitmapImage GetImageFromCRMWebResource(string webResourceName)
        {
            #region PreCheck
            _logEntry.ResetLastError();  // Reset Last Error 
            if (_serviceClient == null || string.IsNullOrWhiteSpace(webResourceName))
            {
                return null;
            }
            #endregion

            BitmapImage outImage = null;
            //// CRM Connection 
            //// Get the Web Resources from CRM 
            var SearchFilter = new List<DataverseSearchFilter>();
            var filter1 = new DataverseSearchFilter()
            {
                SearchConditions = new List<DataverseFilterConditionItem>()
                    {
                        new DataverseFilterConditionItem() { FieldName = "name", FieldOperator = Xrm.Sdk.Query.ConditionOperator.Equal , FieldValue=webResourceName }
                    },
                FilterOperator = Xrm.Sdk.Query.LogicalOperator.And
            };

            SearchFilter.Add(filter1);
            var rslts = _serviceClient.GetEntityDataBySearchParams("webresource", SearchFilter, LogicalSearchOperator.None,
                new List<string>() { "content", "webresourcetype" });
            if (rslts != null && rslts.Count > 0)
            {
                // Found it.. Get the first one. 
                var workingWith = rslts.FirstOrDefault().Value;
                // get the resource type. 
                int rsType = -1;
                OptionSetValue rsOsType = _serviceClient.GetDataByKeyFromResultsSet<OptionSetValue>(workingWith, "webresourcetype");
                if (rsOsType != null)
                    rsType = rsOsType.Value;


                switch (rsType)
                {
                    case (int)WebResourceWebResourceType.PNGformat:
                    case (int)WebResourceWebResourceType.GIFformat:
                    case (int)WebResourceWebResourceType.JPGformat:
                    case (int)WebResourceWebResourceType.ICOformat:
                        // Get the content
                        string sData = _serviceClient.GetDataByKeyFromResultsSet<string>(workingWith, "content");

                        if (string.IsNullOrWhiteSpace(sData))
                            return outImage;
                        try
                        {
                            // Convert from Base64 string to byte[]
                            byte[] imageBytes = Convert.FromBase64String(sData);
                            //// need to leave the memory stream active an allow the bitmapImage life to control it..
                            //// worst case the GC will pick it up.
                            MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
                            //// Init the new Image source... 
                            outImage = new BitmapImage();
                            outImage.BeginInit();
                            outImage.StreamSource = ms;
                            outImage.EndInit();
                            return outImage;
                        }
                        catch (Exception ex)
                        {
                            _logEntry.Log(ex);
                        }

                        break;
                    default:
                        _logEntry.Log(string.Format("Web Resource is not an Image file, Name: {0} File Type:{1}", webResourceName, rsType), TraceEventType.Error);
                        return outImage;
                }
            }
            else
            {
                _logEntry.Log(string.Format("Web Resource Image file not found, Looking for : {0}", webResourceName), TraceEventType.Error);
            }

            return outImage;
        }
    }
}
