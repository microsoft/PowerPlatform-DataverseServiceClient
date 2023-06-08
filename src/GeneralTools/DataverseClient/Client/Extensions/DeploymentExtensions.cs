using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Extensions to support deploying solutions and data to Dataverse. 
    /// </summary>
    public static class DeploymentExtensions
    {


        /// <summary>
        /// Starts an Import request for CDS.
        /// <para>Supports a single file per Import request.</para>
        /// </summary>
        /// <param name="delayUntil">Delays the import jobs till specified time - Use DateTime.MinValue to Run immediately </param>
        /// <param name="importRequest">Import Data Request</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Guid of the Import Request, or Guid.Empty.  If Guid.Empty then request failed.</returns>
        public static Guid SubmitImportRequest(this ServiceClient serviceClient, ImportRequest importRequest, DateTime delayUntil)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            // Error checking
            if (importRequest == null)
            {
                serviceClient._logEntry.Log("************ Exception on SubmitImportRequest, importRequest is required", TraceEventType.Error);
                return Guid.Empty;
            }

            if (importRequest.Files == null || (importRequest.Files != null && importRequest.Files.Count == 0))
            {
                serviceClient._logEntry.Log("************ Exception on SubmitImportRequest, importRequest.Files is required and must have at least one file listed to import.", TraceEventType.Error);
                return Guid.Empty;
            }

            // Done error checking
            if (string.IsNullOrWhiteSpace(importRequest.ImportName))
                importRequest.ImportName = "User Requested Import";


            Guid ImportId = Guid.Empty;
            Guid ImportMap = Guid.Empty;
            Guid ImportFile = Guid.Empty;
            List<Guid> ImportFileIds = new List<Guid>();

            // Create Import Object
            // The Import Object is the anchor for the Import job in Dataverse.
            Dictionary<string, DataverseDataTypeWrapper> importFields = new Dictionary<string, DataverseDataTypeWrapper>();
            importFields.Add("name", new DataverseDataTypeWrapper(importRequest.ImportName, DataverseFieldType.String));
            importFields.Add("modecode", new DataverseDataTypeWrapper(importRequest.Mode, DataverseFieldType.Picklist));  // 0 == Create , 1 = Update..
            ImportId = serviceClient.CreateNewRecord("import", importFields);

            if (ImportId == Guid.Empty)
                // Error here;
                return Guid.Empty;

            #region Determin Map to Use
            //Guid guDataMapId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(importRequest.DataMapFileName) && importRequest.DataMapFileId == Guid.Empty)
                // User Requesting to use System Mapping here.
                importRequest.UseSystemMap = true;  // Override whatever setting they had here.
            else
            {
                // User providing information on a map to use.
                // Query to get the map from the system
                List<string> fldList = new List<string>();
                fldList.Add("name");
                fldList.Add("source");
                fldList.Add("importmapid");
                Dictionary<string, object> MapData = null;
                if (importRequest.DataMapFileId != Guid.Empty)
                {
                    // Have the id here... get the map based on the ID.
                    MapData = serviceClient.GetEntityDataById("importmap", importRequest.DataMapFileId, fldList);
                }
                else
                {
                    // Search by name... exact match required.
                    List<DataverseSearchFilter> filters = new List<DataverseSearchFilter>();
                    DataverseSearchFilter filter = new DataverseSearchFilter();
                    filter.FilterOperator = Microsoft.Xrm.Sdk.Query.LogicalOperator.And;
                    filter.SearchConditions.Add(new DataverseFilterConditionItem() { FieldName = "name", FieldOperator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, FieldValue = importRequest.DataMapFileName });
                    filters.Add(filter);

                    // Search by Name..
                    Dictionary<string, Dictionary<string, object>> rslts = serviceClient.GetEntityDataBySearchParams("importmap", filters, LogicalSearchOperator.None, fldList);
                    if (rslts != null && rslts.Count > 0)
                    {
                        // if there is more then one record returned.. throw an error ( should not happen )
                        if (rslts.Count > 1)
                        {
                            // log error here.
                            serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on SubmitImportRequest, More then one mapping file was found for {0}, Specifiy the ID of the Mapfile to use", importRequest.DataMapFileName), TraceEventType.Error);
                            return Guid.Empty;
                        }
                        else
                        {
                            // Get my single record and move on..
                            MapData = rslts.First().Value;
                            // Update the Guid for the mapID.
                            importRequest.DataMapFileId = serviceClient.GetDataByKeyFromResultsSet<Guid>(MapData, "importmapid");
                        }
                    }
                }
                ImportMap = importRequest.DataMapFileId;


                // Now get the entity import mapping info,  We need this to get the source entity name from the map XML file.
                if (ImportMap != Guid.Empty)
                {
                    // Iterate over the import files and update the entity names.

                    fldList.Clear();
                    fldList.Add("sourceentityname");
                    List<DataverseSearchFilter> filters = new List<DataverseSearchFilter>();
                    DataverseSearchFilter filter = new DataverseSearchFilter();
                    filter.FilterOperator = Microsoft.Xrm.Sdk.Query.LogicalOperator.And;
                    filter.SearchConditions.Add(new DataverseFilterConditionItem() { FieldName = "importmapid", FieldOperator = ConditionOperator.Equal, FieldValue = ImportMap });
                    filters.Add(filter);
                    Dictionary<string, Dictionary<string, object>> al = serviceClient.GetEntityDataBySearchParams("importentitymapping", filters, LogicalSearchOperator.None, null);
                    if (al != null && al.Count > 0)
                    {
                        foreach (var row in al.Values)
                        {
                            importRequest.Files.ForEach(fi =>
                            {
                                if (fi.TargetEntityName.Equals(serviceClient.GetDataByKeyFromResultsSet<string>(row, "targetentityname"), StringComparison.OrdinalIgnoreCase))
                                    fi.SourceEntityName = serviceClient.GetDataByKeyFromResultsSet<string>(row, "sourceentityname");
                            });
                        }
                    }
                    else
                    {
                        if (ImportId != Guid.Empty)
                            serviceClient.DeleteEntity("import", ImportId);

                        // Failed to find mapping entry error , Map not imported properly
                        serviceClient._logEntry.Log("************ Exception on SubmitImportRequest, Cannot find mapping file information found MapFile Provided.", TraceEventType.Error);
                        return Guid.Empty;
                    }
                }
                else
                {
                    if (ImportId != Guid.Empty)
                        serviceClient.DeleteEntity("import", ImportId);

                    // Failed to find mapping entry error , Map not imported properly
                    serviceClient._logEntry.Log("************ Exception on SubmitImportRequest, Cannot find ImportMappingsFile Provided.", TraceEventType.Error);
                    return Guid.Empty;
                }

            }
            #endregion

            #region Create Import File for each File in array
            bool continueImport = true;
            Dictionary<string, DataverseDataTypeWrapper> importFileFields = new Dictionary<string, DataverseDataTypeWrapper>();
            foreach (var FileItem in importRequest.Files)
            {
                // Create the Import File Object - Loop though file objects and create as many as necessary.
                // This is the row that has the data being imported as well as the status of the import file.
                importFileFields.Add("name", new DataverseDataTypeWrapper(FileItem.FileName, DataverseFieldType.String));
                importFileFields.Add("source", new DataverseDataTypeWrapper(FileItem.FileName, DataverseFieldType.String));
                importFileFields.Add("filetypecode", new DataverseDataTypeWrapper(FileItem.FileType, DataverseFieldType.Picklist)); // File Type is either : 0 = CSV , 1 = XML , 2 = Attachment
                importFileFields.Add("content", new DataverseDataTypeWrapper(FileItem.FileContentToImport, DataverseFieldType.String));
                importFileFields.Add("enableduplicatedetection", new DataverseDataTypeWrapper(FileItem.EnableDuplicateDetection, DataverseFieldType.Boolean));
                importFileFields.Add("usesystemmap", new DataverseDataTypeWrapper(importRequest.UseSystemMap, DataverseFieldType.Boolean)); // Use the System Map to get somthing done.
                importFileFields.Add("sourceentityname", new DataverseDataTypeWrapper(FileItem.SourceEntityName, DataverseFieldType.String));
                importFileFields.Add("targetentityname", new DataverseDataTypeWrapper(FileItem.TargetEntityName, DataverseFieldType.String));
                importFileFields.Add("datadelimitercode", new DataverseDataTypeWrapper(FileItem.DataDelimiter, DataverseFieldType.Picklist));   // 1 = " | 2 =   | 3 = '
                importFileFields.Add("fielddelimitercode", new DataverseDataTypeWrapper(FileItem.FieldDelimiter, DataverseFieldType.Picklist));  // 1 = : | 2 = , | 3 = '
                importFileFields.Add("isfirstrowheader", new DataverseDataTypeWrapper(FileItem.IsFirstRowHeader, DataverseFieldType.Boolean));
                importFileFields.Add("processcode", new DataverseDataTypeWrapper(1, DataverseFieldType.Picklist));
                if (FileItem.IsRecordOwnerATeam)
                    importFileFields.Add("recordsownerid", new DataverseDataTypeWrapper(FileItem.RecordOwner, DataverseFieldType.Lookup, "team"));
                else
                    importFileFields.Add("recordsownerid", new DataverseDataTypeWrapper(FileItem.RecordOwner, DataverseFieldType.Lookup, "systemuser"));

                importFileFields.Add("importid", new DataverseDataTypeWrapper(ImportId, DataverseFieldType.Lookup, "import"));
                if (ImportMap != Guid.Empty)
                    importFileFields.Add("importmapid", new DataverseDataTypeWrapper(ImportMap, DataverseFieldType.Lookup, "importmap"));

                ImportFile = serviceClient.CreateNewRecord("importfile", importFileFields);
                if (ImportFile == Guid.Empty)
                {
                    continueImport = false;
                    break;
                }
                ImportFileIds.Add(ImportFile);
                importFileFields.Clear();
            }

            #endregion


            // if We have an Import File... Activate Import.
            if (continueImport)
            {
                ParseImportResponse parseResp = (ParseImportResponse)serviceClient.Command_Execute(new ParseImportRequest() { ImportId = ImportId },
                    string.Format(CultureInfo.InvariantCulture, "************ Exception Executing ParseImportRequest for ImportJob ({0})", importRequest.ImportName));
                if (parseResp.AsyncOperationId != Guid.Empty)
                {
                    if (delayUntil != DateTime.MinValue)
                    {
                        importFileFields.Clear();
                        importFileFields.Add("postponeuntil", new DataverseDataTypeWrapper(delayUntil, DataverseFieldType.DateTime));
                        serviceClient.UpdateEntity("asyncoperation", "asyncoperationid", parseResp.AsyncOperationId, importFileFields);
                    }

                    TransformImportResponse transformResp = (TransformImportResponse)serviceClient.Command_Execute(new TransformImportRequest() { ImportId = ImportId },
                        string.Format(CultureInfo.InvariantCulture, "************ Exception Executing TransformImportRequest for ImportJob ({0})", importRequest.ImportName));
                    if (transformResp != null)
                    {
                        if (delayUntil != DateTime.MinValue)
                        {
                            importFileFields.Clear();
                            importFileFields.Add("postponeuntil", new DataverseDataTypeWrapper(delayUntil.AddSeconds(1), DataverseFieldType.DateTime));
                            serviceClient.UpdateEntity("asyncoperation", "asyncoperationid", transformResp.AsyncOperationId, importFileFields);
                        }

                        ImportRecordsImportResponse importResp = (ImportRecordsImportResponse)serviceClient.Command_Execute(new ImportRecordsImportRequest() { ImportId = ImportId },
                            string.Format(CultureInfo.InvariantCulture, "************ Exception Executing ImportRecordsImportRequest for ImportJob ({0})", importRequest.ImportName));
                        if (importResp != null)
                        {
                            if (delayUntil != DateTime.MinValue)
                            {
                                importFileFields.Clear();
                                importFileFields.Add("postponeuntil", new DataverseDataTypeWrapper(delayUntil.AddSeconds(2), DataverseFieldType.DateTime));
                                serviceClient.UpdateEntity("asyncoperation", "asyncoperationid", importResp.AsyncOperationId, importFileFields);
                            }

                            return ImportId;
                        }
                    }
                }
            }
            else
            {
                // Error.. Clean up the other records.
                string err = serviceClient.LastError;
                Exception ex = serviceClient.LastException;

                if (ImportFileIds.Count > 0)
                {
                    ImportFileIds.ForEach(i =>
                    {
                        serviceClient.DeleteEntity("importfile", i);
                    });
                    ImportFileIds.Clear();
                }

                if (ImportId != Guid.Empty)
                    serviceClient.DeleteEntity("import", ImportId);

                // This is done to allow the error to be available to the user after the class cleans things up.
                if (ex != null)
                    serviceClient._logEntry.Log(err, TraceEventType.Error, ex);
                else
                    serviceClient._logEntry.Log(err, TraceEventType.Error);

                return Guid.Empty;
            }
            return ImportId;
        }

        /// <summary>
        /// Used to upload a data map to the Dataverse
        /// </summary>
        /// <param name="dataMapXml">XML of the datamap in string form</param>
        /// <param name="replaceIds">True to have Dataverse replace ID's on inbound data, False to have inbound data retain its ID's</param>
        /// <param name="dataMapXmlIsFilePath">if true, dataMapXml is expected to be a File name and path to load.</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Returns ID of the datamap or Guid.Empty</returns>
        public static Guid ImportDataMap(this ServiceClient serviceClient, string dataMapXml, bool replaceIds = true, bool dataMapXmlIsFilePath = false)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (string.IsNullOrWhiteSpace(dataMapXml))
            {
                serviceClient._logEntry.Log("************ Exception on ImportDataMap, dataMapXml is required", TraceEventType.Error);
                return Guid.Empty;
            }

            if (dataMapXmlIsFilePath)
            {
                // try to load the file from the file system
                if (File.Exists(dataMapXml))
                {
                    try
                    {
                        string sContent = "";
                        using (var a = File.OpenText(dataMapXml))
                        {
                            sContent = a.ReadToEnd();
                        }

                        dataMapXml = sContent;
                    }
                    #region Exception handlers for files
                    catch (UnauthorizedAccessException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, Unauthorized Access to file: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (ArgumentNullException ex)
                    {
                        serviceClient._logEntry.Log("************ Exception on ImportDataMap, File path not specified", TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (ArgumentException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File path is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (PathTooLongException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File path is too long. Paths must be less than 248 characters, and file names must be less than 260 characters\n{0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File path is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (FileNotFoundException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File Not Found: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    catch (NotSupportedException ex)
                    {
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File path or name is invalid: {0}", dataMapXml), TraceEventType.Error, ex);
                        return Guid.Empty;
                    }
                    #endregion
                }
                else
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportDataMap, File path specified in dataMapXml is not found: {0}", dataMapXml), TraceEventType.Error);
                    return Guid.Empty;
                }

            }

            ImportMappingsImportMapResponse resp = (ImportMappingsImportMapResponse)serviceClient.Command_Execute(new ImportMappingsImportMapRequest() { MappingsXml = dataMapXml, ReplaceIds = replaceIds },
                "************ Exception Executing ImportMappingsImportMapResponse for ImportDataMap");
            if (resp != null)
            {
                if (resp.ImportMapId != Guid.Empty)
                {
                    return resp.ImportMapId;
                }
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Import Solution Async used Execute Async pattern to run a solution import.
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on Dataverse organizations version 7.2 or higher.  This imports the Dataverse solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="extraParameters">Extra parameters</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Returns the Async Job ID.  To find the status of the job, query the AsyncOperation Entity using GetEntityDataByID using the returned value of this method</returns>
        public static Guid ImportSolutionAsync(this ServiceClient serviceClient, string solutionPath, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return serviceClient.ImportSolutionToImpl(solutionPath, Guid.Empty, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, true, extraParameters);
        }

        /// <summary>
        /// Import Solution Async used Execute Async pattern to run a solution import.
        /// </summary>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="StageSolutionUploadId">Staged Solution Upload Id, created from Stage Solution.</param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on Dataverse organizations version 7.2 or higher.  This imports the Dataverse solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="extraParameters">Extra parameters</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Returns the Async Job ID.  To find the status of the job, query the AsyncOperation Entity using GetEntityDataByID using the returned value of this method</returns>

        public static Guid ImportSolutionAsync(this ServiceClient serviceClient, Guid StageSolutionUploadId, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return serviceClient.ImportSolutionToImpl(string.Empty, StageSolutionUploadId, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, true, extraParameters);
        }

        /// <summary>
        /// Stages a solution for Import. <see href="https://learn.microsoft.com/power-platform/alm/solution-async#staging-a-solution"/>
        /// A solution path or stream containing a solution is required to use this. 
        /// </summary>
        /// <param name="serviceClient"></param>
        /// <param name="solutionPath">Path to the solution</param>
        /// <param name="solutionStream">memory stream containing the solution file to be staged.</param>
        /// <returns>StageSolutionResults, <see cref="StageSolutionResults"/></returns>
        public static async Task<StageSolutionResults> StageSolution(this ServiceClient serviceClient, string solutionPath, MemoryStream solutionStream = null)
        {
            if (serviceClient.DataverseService == null)
            {
                var Error = new DataverseOperationException("Dataverse Service not initialized", ErrorCodes.DataverseServiceClientNotIntialized, string.Empty, null);
                serviceClient._logEntry.Log(Error);
                throw Error;
            }

            if (solutionStream == null && string.IsNullOrWhiteSpace(solutionPath))
            {
                var Error = new DataverseOperationException("SolutionPath or Solution File Stream is required", ErrorCodes.SolutionFilePathNull, string.Empty, null);
                serviceClient._logEntry.Log(Error);
                throw Error;
            }

            // determine if the system is connected to OnPrem
            if (serviceClient._connectionSvc.ConnectedOrganizationDetail != null && string.IsNullOrEmpty(serviceClient._connectionSvc.ConnectedOrganizationDetail.Geo))
            {
                var Error = new DataverseOperationException("StageSolution is not valid for OnPremise deployments", ErrorCodes.OperationInvalidOnPrem, string.Empty, null);
                serviceClient._logEntry.Log(Error);
                throw Error;
            }

            bool streamLocalyCreated = false;
            try
            {
                if (solutionStream == null && File.Exists(solutionPath))
                {
                    solutionStream = new MemoryStream(File.ReadAllBytes(solutionPath));
                    streamLocalyCreated = true;
                }
            }
            catch (Exception ex)
            {
                var Error = new DataverseOperationException("Read Solution Failed", ex);
                serviceClient._logEntry.Log(Error);
                throw Error;

            }

            //SolutionParameters
            StageSolutionRequest stageSolutionRequest = new StageSolutionRequest()
            {
                CustomizationFile = solutionStream.ToArray()
            };
            if (streamLocalyCreated)
            {
                solutionStream.Close();
                solutionStream.Dispose();
            }

            // submit request. 
            var solutionStageResp = (StageSolutionResponse)await serviceClient.ExecuteAsync(stageSolutionRequest).ConfigureAwait(false);
            return solutionStageResp.StageSolutionResults;
        }

        /// <summary>
        /// <para>
        /// Imports a Dataverse solution to the Dataverse Server currently connected.
        /// <para>*** Note: this is a blocking call and will take time to Import to Dataverse ***</para>
        /// </para>
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on Dataverse organizations version 7.2 or higher.  This imports the Dataverse solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="extraParameters">Extra parameters</param>
        public static Guid ImportSolution(this ServiceClient serviceClient, string solutionPath, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return serviceClient.ImportSolutionToImpl(solutionPath, Guid.Empty, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, false, extraParameters);
        }

        /// <summary>
        /// <para>
        /// Imports a Dataverse solution to the Dataverse Server currently connected.
        /// <para>*** Note: this is a blocking call and will take time to Import to Dataverse ***</para>
        /// </para>
        /// </summary>
        /// <param name="StageSolutionUploadId">Staged Solution Upload Id, created from Stage Solution.</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on Dataverse organizations version 7.2 or higher.  This imports the Dataverse solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="extraParameters">Extra parameters</param>
        public static Guid ImportSolution(this ServiceClient serviceClient, Guid StageSolutionUploadId, out Guid importId, bool activatePlugIns = true, bool overwriteUnManagedCustomizations = false, bool skipDependancyOnProductUpdateCheckOnInstall = false, bool importAsHoldingSolution = false, bool isInternalUpgrade = false, Dictionary<string, object> extraParameters = null)
        {
            return serviceClient.ImportSolutionToImpl(string.Empty, StageSolutionUploadId, out importId, activatePlugIns, overwriteUnManagedCustomizations, skipDependancyOnProductUpdateCheckOnInstall, importAsHoldingSolution, isInternalUpgrade, false, extraParameters);
        }

        /// <summary>
        /// Executes a Delete and Propmote Request against Dataverse using the Async Pattern.
        /// </summary>
        /// <param name="uniqueName">Unique Name of solution to be upgraded</param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>Returns the Async Job ID.  To find the status of the job, query the AsyncOperation Entity using GetEntityDataByID using the returned value of this method</returns>
        public static Guid DeleteAndPromoteSolutionAsync(this ServiceClient serviceClient, string uniqueName)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }
            // Test for non blank unique name.
            if (string.IsNullOrEmpty(uniqueName))
            {
                serviceClient._logEntry.Log("Solution UniqueName is required.", TraceEventType.Error);
                return Guid.Empty;
            }

            DeleteAndPromoteRequest delReq = new DeleteAndPromoteRequest()
            {
                UniqueName = uniqueName
            };

            // Assign Tracking ID
            Guid requestTrackingId = Guid.NewGuid();
            delReq.RequestId = requestTrackingId;

            // Execute Async here
            ExecuteAsyncRequest req = new ExecuteAsyncRequest() { Request = delReq };
            serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1} - Created Async DeleteAndPromoteSolutionRequest : RequestID={0} ",
            requestTrackingId.ToString(), uniqueName), TraceEventType.Verbose);
            ExecuteAsyncResponse resp = (ExecuteAsyncResponse)serviceClient.Command_Execute(req, "Submitting DeleteAndPromoteSolution Async Request");
            if (resp != null)
            {
                if (resp.AsyncJobId != Guid.Empty)
                {
                    serviceClient._logEntry.Log(string.Format("{1} - AsyncJobID for DeleteAndPromoteSolution {0}.", resp.AsyncJobId, uniqueName), TraceEventType.Verbose);
                    return resp.AsyncJobId;
                }
            }

            serviceClient._logEntry.Log(string.Format("{0} - Failed execute Async Job for DeleteAndPromoteSolution.", uniqueName), TraceEventType.Error);
            return Guid.Empty;
        }

        /// <summary>
        /// <para>
        /// Request Dataverse to install sample data shipped with Dataverse. Note this is process will take a few moments to execute.
        /// <para>This method will return once the request has been submitted.</para>
        /// </para>
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>ID of the Async job executing the request</returns>
        public static Guid InstallSampleData(this ServiceClient serviceClient)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (ImportStatus.NotImported != serviceClient.IsSampleDataInstalled())
            {
                serviceClient._logEntry.Log("************ InstallSampleData failed, sample data is already installed on Dataverse", TraceEventType.Error);
                return Guid.Empty;
            }

            // Create Request to Install Sample data.
            InstallSampleDataRequest loadSampledataRequest = new InstallSampleDataRequest() { RequestId = Guid.NewGuid() };
            InstallSampleDataResponse resp = (InstallSampleDataResponse)serviceClient.Command_Execute(loadSampledataRequest, "Executing InstallSampleDataRequest for InstallSampleData");
            if (resp == null)
                return Guid.Empty;
            else
                return loadSampledataRequest.RequestId.Value;
        }

        /// <summary>
        /// <para>
        /// Request Dataverse to remove sample data shipped with Dataverse. Note this is process will take a few moments to execute.
        /// This method will return once the request has been submitted.
        /// </para>
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>ID of the Async job executing the request</returns>
        public static Guid UninstallSampleData(this ServiceClient serviceClient)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (ImportStatus.NotImported == serviceClient.IsSampleDataInstalled())
            {
                serviceClient._logEntry.Log("************ UninstallSampleData failed, sample data is not installed on Dataverse", TraceEventType.Error);
                return Guid.Empty;
            }

            UninstallSampleDataRequest removeSampledataRequest = new UninstallSampleDataRequest() { RequestId = Guid.NewGuid() };
            UninstallSampleDataResponse resp = (UninstallSampleDataResponse)serviceClient.Command_Execute(removeSampledataRequest, "Executing UninstallSampleDataRequest for UninstallSampleData");
            if (resp == null)
                return Guid.Empty;
            else
                return removeSampledataRequest.RequestId.Value;
        }

        /// <summary>
        /// Determines if the Dataverse sample data has been installed
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <returns>True if the sample data is installed, False if not. </returns>
        public static ImportStatus IsSampleDataInstalled(this ServiceClient serviceClient)
        {
            try
            {
                // Query the Org I'm connected to to get the sample data import info.
                Dictionary<string, Dictionary<string, object>> theOrg =
                serviceClient.GetEntityDataBySearchParams("organization",
                    new Dictionary<string, string>(), LogicalSearchOperator.None, new List<string>() { "sampledataimportid" });

                if (theOrg != null && theOrg.Count > 0)
                {
                    var v = theOrg.FirstOrDefault();
                    if (v.Value != null && v.Value.Count > 0)
                    {
                        if (serviceClient.GetDataByKeyFromResultsSet<Guid>(v.Value, "sampledataimportid") != Guid.Empty)
                        {
                            string sampledataimportid = serviceClient.GetDataByKeyFromResultsSet<Guid>(v.Value, "sampledataimportid").ToString();
                            serviceClient._logEntry.Log(string.Format("sampledataimportid = {0}", sampledataimportid), TraceEventType.Verbose);
                            Dictionary<string, string> basicSearch = new Dictionary<string, string>();
                            basicSearch.Add("importid", sampledataimportid);
                            Dictionary<string, Dictionary<string, object>> importSampleData = serviceClient.GetEntityDataBySearchParams("import", basicSearch, LogicalSearchOperator.None, new List<string>() { "statuscode" });

                            if (importSampleData != null && importSampleData.Count > 0)
                            {
                                var import = importSampleData.FirstOrDefault();
                                if (import.Value != null)
                                {
                                    OptionSetValue ImportStatusResult = serviceClient.GetDataByKeyFromResultsSet<OptionSetValue>(import.Value, "statuscode");
                                    if (ImportStatusResult != null)
                                    {
                                        serviceClient._logEntry.Log(string.Format("sampledata import job result = {0}", ImportStatusResult.Value), TraceEventType.Verbose);
                                        //This Switch Case needs to be in Sync with the Dataverse Import StatusCode.
                                        switch (ImportStatusResult.Value)
                                        {
                                            // 4 is the Import Status Code for Complete Import
                                            case 4: return ImportStatus.Completed;
                                            // 5 is the Import Status Code for the Failed Import
                                            case 5: return ImportStatus.Failed;
                                            // Rest (Submitted, Parsing, Transforming, Importing) are different stages of Inprogress Import hence putting them under same case.
                                            default: return ImportStatus.InProgress;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return ImportStatus.NotImported;
            //return false;
        }

        /// <summary>
        /// Retrieve solution import result from Dataverse
        /// </summary>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="importJobId">Import job Id</param>
        /// <param name="includeFormattedResults">Check if the result need to be formatted</param>
        /// <returns>Solution import result. </returns>
        public static SolutionOperationResult RetrieveSolutionImportResultAsync(this ServiceClient serviceClient, Guid importJobId, bool includeFormattedResults = false)
        {
            var res = new SolutionOperationResult();

            if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult))
            {
                // Not supported on this version of Dataverse
                serviceClient._logEntry.Log($"RetrieveSolutionImportResultAsync request is calling RetrieveSolutionImportResult API. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowRetrieveSolutionImportResult.ToString()} or above. The current Dataverse version is {serviceClient._connectionSvc?.OrganizationVersion}. This request cannot be made", TraceEventType.Warning);
                return null;
            }

            if (!includeFormattedResults)
            {
                // Retrieve import job from Dataverse, doesn't include formatted results
                var columnSet = new ColumnSet();
                columnSet.AddColumns(new string[] { "importjobid", "data" });
                var query = new QueryByAttribute();
                query.ColumnSet = columnSet;
                query.EntityName = "importjob";
                query.AddAttributeValue("importjobid", importJobId);
                var importJobs = serviceClient.RetrieveMultipleAsync(query);

                // If the importJobId is wrong, would return a entity collection with 0 record
                if (importJobs.Result.Entities.Count == 0)
                {
                    serviceClient._logEntry.Log($"The solution import Job with id {importJobId} is not found.", TraceEventType.Error);
                    return null;
                }

                var importJob = importJobs.Result.Entities[0];

                // Initialize result data member
                res.Type = SolutionOperationType.Import;
                res.ErrorMessages = new List<string>();
                res.WarningMessages = new List<string>();
                res.ActionLink = new ActionLink();

                // Parse the Xml file
                XmlDocument doc = XmlUtil.CreateXmlDocument((string)importJob["data"]);
                var root = doc.DocumentElement;
                if (root.Attributes != null && root.Attributes["succeeded"] != null)
                {
                    res.Status = root.Attributes["succeeded"].Value == "failure" ? SolutionOperationStatus.Failed : SolutionOperationStatus.Passed;
                }

                if (res.Status == SolutionOperationStatus.Failed)
                {
                    // Add error message
                    if (root.Attributes != null && root.Attributes["status"] != null)
                    {
                        res.ErrorMessages.Add(root.Attributes["status"].Value);
                    }
                }
                else
                {
                    // Add warning message
                    using (var warningNodes = doc.SelectNodes("//*[@result='warning']"))
                    {
                        if (warningNodes != null && warningNodes.Count > 0)
                        {
                            foreach (XmlNode node in warningNodes)
                            {
                                if (node.Attributes != null && node.Attributes["errortext"] != null)
                                {
                                    res.WarningMessages.Add(node.Attributes["errortext"].Value);
                                }
                            }
                        }
                    }
                }

                // Add action link
                var actionlinkNode = doc.SelectSingleNode("/importexportxml/actionlink");
                if (actionlinkNode != null && actionlinkNode.Attributes != null)
                {
                    var label = actionlinkNode.Attributes["label"];
                    var target = actionlinkNode.Attributes["target"];

                    if (label != null)
                    {
                        res.ActionLink.Label = label.Value;
                    }

                    if (target != null)
                    {
                        res.ActionLink.Target = target.Value;
                    }
                }
            }
            else
            {
                // Retrieve import job from Dataverse by RetrieveSolutionImportResult API include formatted results
                var req = new OrganizationRequest("RetrieveSolutionImportResult");
                req.Parameters.Add(new KeyValuePair<string, object>("ImportJobId", importJobId));
                var importJob = serviceClient.Command_Execute(req, "Executing Request for RetrieveSolutionImportResult");
                if (importJob.Results.Contains("SolutionOperationResult"))
                {
                    res = (SolutionOperationResult)importJob.Results["SolutionOperationResult"];
                }
            }
            return res;
        }

        /// <summary>
        /// Requests status on an Async Operation. 
        /// </summary>
        /// <param name="serviceClient"></param>
        /// <param name="asyncOperationId"></param>
        /// <returns></returns>
        public static async Task<AsyncStatusResponse> GetAsyncOperationStatus(this ServiceClient serviceClient, Guid asyncOperationId)
        {
            var AsyncQuery = new QueryExpression("asyncoperation")
            {
                TopCount = 1
            };
            // Add columns necessary for a client to know if the system is acting. 
            AsyncQuery.ColumnSet.AddColumns(
                "asyncoperationid",
                "name",
                "operationtype",
                "breadcrumbid",
                "friendlymessage",
                "message",
                "statecode",
                "statuscode",
                "correlationid",
                "correlationupdatedtime"
            );
            AsyncQuery.Criteria.AddCondition("asyncoperationid", ConditionOperator.Equal, asyncOperationId);

            try
            {
                var asyncOpResult = await serviceClient.RetrieveMultipleAsync(AsyncQuery).ConfigureAwait(false);
                return new AsyncStatusResponse(asyncOpResult);
            }
            catch (Exception ex)
            {
                throw new DataverseOperationException("Failed to Get AsyncOperation Status", ex);
            }
        }

        #region SupportClasses
        /// <summary>
        /// ImportStatus Reasons
        /// </summary>
        public enum ImportStatus
        {
            /// <summary> Not Yet Imported </summary>
            NotImported = 0,
            /// <summary> Import is in Progress </summary>
            InProgress = 1,
            /// <summary> Import has Completed </summary>
            Completed = 2,
            /// <summary> Import has Failed </summary>
            Failed = 3
        };

        /// <summary>
        /// Describes an import request for Dataverse
        /// </summary>
        public sealed class ImportRequest
        {
            #region Vars
            // Import Items..
            /// <summary>
            /// Name of the Import Request.  this Name will appear in Dataverse
            /// </summary>
            public string ImportName { get; set; }
            /// <summary>
            /// Sets or gets the Import Mode.
            /// </summary>
            public ImportMode Mode { get; set; }

            // Import Map Items.
            /// <summary>
            /// ID of the DataMap to use
            /// </summary>
            public Guid DataMapFileId { get; set; }
            /// <summary>
            /// Name of the DataMap File to use
            /// ID or Name is required
            /// </summary>
            public string DataMapFileName { get; set; }

            /// <summary>
            /// if True, infers the map from the type of entity requested..
            /// </summary>
            public bool UseSystemMap { get; set; }

            /// <summary>
            /// List of files to import in this job,  there must be at least one.
            /// </summary>
            public List<ImportFileItem> Files { get; set; }


            #endregion

            /// <summary>
            /// Mode of the Import, Update or Create
            /// </summary>
            public enum ImportMode
            {
                /// <summary>
                /// Create a new Import
                /// </summary>
                Create = 0,
                /// <summary>
                /// Update to Imported Items
                /// </summary>
                Update = 1
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            public ImportRequest()
            {
                Files = new List<ImportFileItem>();
            }

        }

        /// <summary>
        /// Describes an Individual Import Item.
        /// </summary>
        public class ImportFileItem
        {
            /// <summary>
            /// File Name of Individual file
            /// </summary>
            public string FileName { get; set; }
            /// <summary>
            /// Type of Import file.. XML or CSV
            /// </summary>
            public FileTypeCode FileType { get; set; }
            /// <summary>
            /// This is the CSV file you wish to import,
            /// </summary>
            public string FileContentToImport { get; set; }
            /// <summary>
            /// This enabled duplicate detection rules
            /// </summary>
            public bool EnableDuplicateDetection { get; set; }
            /// <summary>
            /// Name of the entity that Originated the data.
            /// </summary>
            public string SourceEntityName { get; set; }
            /// <summary>
            /// Name of the entity that Target Entity the data.
            /// </summary>
            public string TargetEntityName { get; set; }
            /// <summary>
            /// This is the delimiter for the Data,
            /// </summary>
            public DataDelimiterCode DataDelimiter { get; set; }
            /// <summary>
            /// this is the field separator
            /// </summary>
            public FieldDelimiterCode FieldDelimiter { get; set; }
            /// <summary>
            /// Is the first row of the CSV the RowHeader?
            /// </summary>
            public bool IsFirstRowHeader { get; set; }
            /// <summary>
            /// UserID or Team ID of the Record Owner ( from systemuser )
            /// </summary>
            public Guid RecordOwner { get; set; }
            /// <summary>
            /// Set true if the Record Owner is a Team
            /// </summary>
            public bool IsRecordOwnerATeam { get; set; }

            /// <summary>
            /// Key used to delimit data in the import file
            /// </summary>
            public enum DataDelimiterCode
            {
                /// <summary>
                /// Specifies "
                /// </summary>
                DoubleQuotes = 1,   // "
                /// <summary>
                /// Specifies no delimiter
                /// </summary>
                None = 2,           //
                /// <summary>
                /// Specifies '
                /// </summary>
                SingleQuote = 3     // '
            }

            /// <summary>
            /// Key used to delimit fields in the import file
            /// </summary>
            public enum FieldDelimiterCode
            {
                /// <summary>
                /// Specifies :
                /// </summary>
                Colon = 1,
                /// <summary>
                /// Specifies ,
                /// </summary>
                Comma = 2,
                /// <summary>
                /// Specifies '
                /// </summary>
                SingleQuote = 3
            }

            /// <summary>
            /// Type if file described in the FileContentToImport
            /// </summary>
            public enum FileTypeCode
            {
                /// <summary>
                /// CSV File Type
                /// </summary>
                CSV = 0,
                /// <summary>
                /// XML File type
                /// </summary>
                XML = 1
            }

        }

        #endregion


        #region Private

        /// <summary>
        /// <para>
        /// Imports a Dataverse solution to the Dataverse Server currently connected.
        /// <para>*** Note: this is a blocking call and will take time to Import to Dataverse ***</para>
        /// </para>
        /// </summary>
        /// <param name="solutionPath">Path to the Solution File</param>
        /// <param name="activatePlugIns">Activate Plugin's and workflows on the Solution </param>
        /// <param name="importId"><para>This will populate with the Import ID even if the request failed.
        /// You can use this ID to request status on the import via a request to the ImportJob entity.</para></param>
        /// <param name="overwriteUnManagedCustomizations">Forces an overwrite of unmanaged customizations of the managed solution you are installing, defaults to false</param>
        /// <param name="skipDependancyOnProductUpdateCheckOnInstall">Skips dependency against dependencies flagged as product update, defaults to false</param>
        /// <param name="importAsHoldingSolution">Applies only on Dataverse organizations version 7.2 or higher.  This imports the Dataverse solution as a holding solution utilizing the “As Holding” capability of ImportSolution </param>
        /// <param name="isInternalUpgrade">Internal Microsoft use only</param>
        /// <param name="useAsync">Requires the use of an Async Job to do the import. </param>
        /// <param name="serviceClient">ServiceClient</param>
        /// <param name="stageSolutionUploadId">Upload ID for Solution that has been staged</param>
        /// <param name="extraParameters">Extra parameters</param>
        /// <returns>Returns the Import Solution Job ID.  To find the status of the job, query the ImportJob Entity using GetEntityDataByID using the returned value of this method</returns>
        internal static Guid ImportSolutionToImpl(this ServiceClient serviceClient, string solutionPath, Guid stageSolutionUploadId, out Guid importId, bool activatePlugIns, bool overwriteUnManagedCustomizations, bool skipDependancyOnProductUpdateCheckOnInstall, bool importAsHoldingSolution, bool isInternalUpgrade, bool useAsync, Dictionary<string, object> extraParameters)
        {
            serviceClient._logEntry.ResetLastError();  // Reset Last Error
            importId = Guid.Empty;
            if (serviceClient.DataverseService == null)
            {
                serviceClient._logEntry.Log("Dataverse Service not initialized", TraceEventType.Error);
                return Guid.Empty;
            }

            if (stageSolutionUploadId == Guid.Empty && string.IsNullOrWhiteSpace(solutionPath))
            {
                serviceClient._logEntry.Log("************ Exception on ImportSolutionToImpl, SolutionPath is required", TraceEventType.Error);
                return Guid.Empty;
            }

            // determine if the system is connected to OnPrem
            bool isConnectedToOnPrem = (serviceClient._connectionSvc.ConnectedOrganizationDetail != null && string.IsNullOrEmpty(serviceClient._connectionSvc.ConnectedOrganizationDetail.Geo));

            //Extract extra parameters if they exist
            string solutionName = string.Empty;
            LayerDesiredOrder desiredLayerOrder = null;
            bool? asyncRibbonProcessing = null;
            EntityCollection componetsToProcess = null;
            bool? convertToManaged = null;
            bool? isTemplateModeImport = null;
            string templateSuffix = null;
            bool? useStageSolutionProcess = null;

            if (extraParameters != null)
            {
                solutionName = extraParameters.ContainsKey(ImportSolutionProperties.SOLUTIONNAMEPARAM) ? extraParameters[ImportSolutionProperties.SOLUTIONNAMEPARAM].ToString() : string.Empty;
                desiredLayerOrder = extraParameters.ContainsKey(ImportSolutionProperties.DESIREDLAYERORDERPARAM) ? extraParameters[ImportSolutionProperties.DESIREDLAYERORDERPARAM] as LayerDesiredOrder : null;
                componetsToProcess = extraParameters.ContainsKey(ImportSolutionProperties.COMPONENTPARAMETERSPARAM) ? extraParameters[ImportSolutionProperties.COMPONENTPARAMETERSPARAM] as EntityCollection : null;
                convertToManaged = extraParameters.ContainsKey(ImportSolutionProperties.CONVERTTOMANAGED) ? extraParameters[ImportSolutionProperties.CONVERTTOMANAGED] as bool? : null;
                isTemplateModeImport = extraParameters.ContainsKey(ImportSolutionProperties.ISTEMPLATEMODE) ? extraParameters[ImportSolutionProperties.ISTEMPLATEMODE] as bool? : null;
                templateSuffix = extraParameters.ContainsKey(ImportSolutionProperties.TEMPLATESUFFIX) ? extraParameters[ImportSolutionProperties.TEMPLATESUFFIX].ToString() : string.Empty;
                useStageSolutionProcess = extraParameters.ContainsKey(ImportSolutionProperties.USESTAGEANDUPGRADEMODE) ? extraParameters[ImportSolutionProperties.USESTAGEANDUPGRADEMODE] as bool? : null;

                // Pick up the data from the request,  if the request has the AsyncRibbonProcessing flag, pick up the value of it.
                asyncRibbonProcessing = extraParameters.ContainsKey(ImportSolutionProperties.ASYNCRIBBONPROCESSING) ? extraParameters[ImportSolutionProperties.ASYNCRIBBONPROCESSING] as bool? : null;
                // If the value is populated, and t
                if (asyncRibbonProcessing != null && asyncRibbonProcessing.HasValue)
                {
                    if (isConnectedToOnPrem)
                    {
                        // Not supported for OnPrem.
                        // reset the asyncRibbonProcess to Null.
                        serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.ASYNCRIBBONPROCESSING} property.  This is not valid for OnPremise deployments and will be removed", TraceEventType.Warning);
                        asyncRibbonProcessing = null;
                    }
                    else
                    {
                        if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowAsyncRibbonProcessing))
                        {
                            // Not supported on this version of Dataverse
                            serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.ASYNCRIBBONPROCESSING} property. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowAsyncRibbonProcessing.ToString()} or above. Current Dataverse version is {serviceClient._connectionSvc?.OrganizationVersion}. This property will be removed", TraceEventType.Warning);
                            asyncRibbonProcessing = null;
                        }
                    }
                }

                if (componetsToProcess != null)
                {
                    if (isConnectedToOnPrem)
                    {
                        serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.COMPONENTPARAMETERSPARAM} property.  This is not valid for OnPremise deployments and will be removed", TraceEventType.Warning);
                        componetsToProcess = null;
                    }
                    else
                    {
                        if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowComponetInfoProcessing))
                        {
                            // Not supported on this version of Dataverse
                            serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.COMPONENTPARAMETERSPARAM} property. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowComponetInfoProcessing.ToString()} or above. Current Dataverse version is {serviceClient._connectionSvc?.OrganizationVersion}. This property will be removed", TraceEventType.Warning);
                            componetsToProcess = null;
                        }
                    }
                }

                if (isTemplateModeImport != null)
                {
                    if (isConnectedToOnPrem)
                    {
                        serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.ISTEMPLATEMODE} property.  This is not valid for OnPremise deployments and will be removed", TraceEventType.Warning);
                        isTemplateModeImport = null;
                    }
                    else
                    {
                        if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowTemplateSolutionImport))
                        {
                            // Not supported on this version of Dataverse
                            serviceClient._logEntry.Log($"ImportSolution request contains {ImportSolutionProperties.ISTEMPLATEMODE} property. This request requires Dataverse version {Utilities.FeatureVersionMinimums.AllowTemplateSolutionImport.ToString()} or above. Current Dataverse version is {serviceClient._connectionSvc?.OrganizationVersion}. This property will be removed", TraceEventType.Warning);
                            isTemplateModeImport = null;
                        }
                    }
                }

                if (useStageSolutionProcess != null)
                {
                    if (isConnectedToOnPrem)
                    {
                        serviceClient._logEntry.Log($"StageAndUpgrade Mode is not valid for OnPremise deployments. Normal Solution Upgrade behavior will be utilized", TraceEventType.Warning);
                        useStageSolutionProcess = null;
                    }
                    else
                    {
                        if (!Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.AllowStageAndUpgrade))
                        {
                            // Not supported on this version of Dataverse
                            serviceClient._logEntry.Log($"StageAndUpgrade Mode requires Dataverse version {Utilities.FeatureVersionMinimums.AllowStageAndUpgrade.ToString()} or above. Current Dataverse version is {serviceClient._connectionSvc?.OrganizationVersion}. Normal Solution Upgrade behavior will be utilized", TraceEventType.Warning);
                            useStageSolutionProcess = null;
                        }
                    }
                }
            }
            string solutionNameForLogging = string.IsNullOrWhiteSpace(solutionName) ? string.Empty : string.Concat(solutionName, " - ");

            // try to load the file from the file system
            if ((!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
                || stageSolutionUploadId != Guid.Empty)
            {
                try
                {
                    importId = Guid.NewGuid();
                    ImportSolutionRequest SolutionImportRequest = new ImportSolutionRequest()
                    {
                        PublishWorkflows = activatePlugIns,
                        ImportJobId = importId,
                        OverwriteUnmanagedCustomizations = overwriteUnManagedCustomizations,
                    };

                    if (stageSolutionUploadId != Guid.Empty)
                    {
                        SolutionImportRequest.SolutionParameters = new SolutionParameters()
                        {
                            StageSolutionUploadId = stageSolutionUploadId,
                        };
                    }
                    else
                    {
                        SolutionImportRequest.CustomizationFile = File.ReadAllBytes(solutionPath);
                    }

                    //If the desiredLayerOrder is null don't add it to the request. This ensures backward compatibility. It makes old packages work on old builds
                    if (desiredLayerOrder != null)
                    {
                        //If package contains the LayerDesiredOrder hint but the server doesn't support the new message, we want the package to fail
                        //The server will throw - "Unrecognized request parameter: LayerDesiredOrder" - That's the desired behavior
                        //The hint is only enforced on the first time a solution is added to the org. If we allow it to go, the import will succeed, but the desired state won't be achieved
                        SolutionImportRequest.LayerDesiredOrder = desiredLayerOrder;

                        string solutionsInHint = string.Join(",", desiredLayerOrder.Solutions.Select(n => n.Name).ToArray());

                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{0}DesiredLayerOrder clause present: Type: {1}, Solutions: {2}", solutionNameForLogging, desiredLayerOrder.Type, solutionsInHint), TraceEventType.Verbose);
                    }

                    if (asyncRibbonProcessing != null && asyncRibbonProcessing == true)
                    {
                        SolutionImportRequest.AsyncRibbonProcessing = true;
                        SolutionImportRequest.SkipQueueRibbonJob = true;
                        serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{0} AsyncRibbonProcessing: {1}", solutionNameForLogging, true), TraceEventType.Verbose);
                    }

                    if (componetsToProcess != null)
                    {
                        SolutionImportRequest.ComponentParameters = componetsToProcess;
                    }

                    if (convertToManaged != null)
                    {
                        SolutionImportRequest.ConvertToManaged = convertToManaged.Value;
                    }

                    if (isTemplateModeImport != null && isTemplateModeImport.Value)
                    {
                        SolutionImportRequest.Parameters[ImportSolutionProperties.ISTEMPLATEMODE] = isTemplateModeImport.Value;
                        SolutionImportRequest.Parameters[ImportSolutionProperties.TEMPLATESUFFIX] = templateSuffix;
                    }

                    if (serviceClient.IsBatchOperationsAvailable)
                    {
                        // Support for features added in UR12
                        SolutionImportRequest.SkipProductUpdateDependencies = skipDependancyOnProductUpdateCheckOnInstall;
                    }

                    if (importAsHoldingSolution)  // If Import as Holding is set..
                    {
                        // Check for Min version of Dataverse for support of Import as Holding solution.
                        if (Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.ImportHoldingSolution))
                        {
                            // Use Parameters to add the property here to support the underlying Xrm API on the incorrect version.
                            SolutionImportRequest.Parameters.Add("HoldingSolution", importAsHoldingSolution);
                        }
                    }

                    // Set IsInternalUpgrade flag on request only for upgrade scenario for V9 only.
                    if (isInternalUpgrade)
                    {
                        if (Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient._connectionSvc?.OrganizationVersion, Utilities.FeatureVersionMinimums.InternalUpgradeSolution))
                        {
                            SolutionImportRequest.Parameters["IsInternalUpgrade"] = true;
                        }
                    }

                    if (useAsync)
                    {
                        // Assign Tracking ID
                        Guid requestTrackingId = Guid.NewGuid();
                        SolutionImportRequest.RequestId = requestTrackingId;

                        if (!isConnectedToOnPrem && useStageSolutionProcess.HasValue && useStageSolutionProcess.Value)
                        {
                            StageAndUpgradeAsyncRequest stgAndUpgradeReq = new StageAndUpgradeAsyncRequest
                            {
                                Parameters = SolutionImportRequest.Parameters
                            };

                            // remove unsupported parameter from importsolutionasync request.
                            if (stgAndUpgradeReq.Parameters.ContainsKey("ImportJobId"))
                                stgAndUpgradeReq.Parameters.Remove("ImportJobId");

                            serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1}Created Async StageAndUpgradeAsyncRequest : RequestID={0} ", requestTrackingId.ToString(), solutionNameForLogging), TraceEventType.Verbose);
                            StageAndUpgradeAsyncResponse asyncResp = (StageAndUpgradeAsyncResponse)serviceClient.Command_Execute(stgAndUpgradeReq, solutionNameForLogging + "Executing Request for StageAndUpgradeAsyncRequest : ");
                            if (asyncResp == null)
                                return Guid.Empty;
                            else
                            {
                                _ = Guid.TryParse(asyncResp.ImportJobKey, out Guid parsedImportKey);
                                importId = parsedImportKey;
                                return asyncResp.AsyncOperationId;
                            }

                        }
                        else if (!isConnectedToOnPrem && Utilities.FeatureVersionMinimums.IsFeatureValidForEnviroment(serviceClient.ConnectedOrgVersion, Utilities.FeatureVersionMinimums.AllowImportSolutionAsyncV2))
                        {
                            // map import request to Async Model
                            ImportSolutionAsyncRequest asynImportRequest = new ImportSolutionAsyncRequest()
                            {
                                AsyncRibbonProcessing = SolutionImportRequest.AsyncRibbonProcessing,
                                ComponentParameters = SolutionImportRequest.ComponentParameters,
                                ConvertToManaged = SolutionImportRequest.ConvertToManaged,
                                CustomizationFile = SolutionImportRequest.CustomizationFile,
                                HoldingSolution = SolutionImportRequest.HoldingSolution,
                                LayerDesiredOrder = SolutionImportRequest.LayerDesiredOrder,
                                OverwriteUnmanagedCustomizations = SolutionImportRequest.OverwriteUnmanagedCustomizations,
                                Parameters = SolutionImportRequest.Parameters,
                                PublishWorkflows = SolutionImportRequest.PublishWorkflows,
                                RequestId = SolutionImportRequest.RequestId,
                                SkipProductUpdateDependencies = SolutionImportRequest.SkipProductUpdateDependencies,
                                SkipQueueRibbonJob = SolutionImportRequest.SkipQueueRibbonJob
                            };

                            // remove unsupported parameter from importsolutionasync request.
                            if (asynImportRequest.Parameters.ContainsKey("ImportJobId"))
                                asynImportRequest.Parameters.Remove("ImportJobId");

                            serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1}Created Async ImportSolutionAsyncRequest : RequestID={0} ", requestTrackingId.ToString(), solutionNameForLogging), TraceEventType.Verbose);
                            ImportSolutionAsyncResponse asyncResp = (ImportSolutionAsyncResponse)serviceClient.Command_Execute(asynImportRequest, solutionNameForLogging + "Executing Request for ImportSolutionAsyncRequest : ");
                            if (asyncResp == null)
                                return Guid.Empty;
                            else
                            {
                                _ = Guid.TryParse(asyncResp.ImportJobKey, out Guid parsedImportKey);
                                importId = parsedImportKey;

                                return asyncResp.AsyncOperationId;
                            }
                        }
                        else
                        {
                            // Creating Async Solution Import request.
                            ExecuteAsyncRequest req = new ExecuteAsyncRequest() { Request = SolutionImportRequest };
                            serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "{1}Created Async ImportSolutionRequest : RequestID={0} ",
                                requestTrackingId.ToString(), solutionNameForLogging), TraceEventType.Verbose);
                            ExecuteAsyncResponse asyncResp = (ExecuteAsyncResponse)serviceClient.Command_Execute(req, solutionNameForLogging + "Executing Request for ImportSolutionToAsync : ");
                            if (asyncResp == null)
                                return Guid.Empty;
                            else
                                return asyncResp.AsyncJobId;
                        }
                    }
                    else
                    {
                        if (useStageSolutionProcess.HasValue && useStageSolutionProcess.Value)
                        {
                            StageAndUpgradeRequest stageAndUpgrade = new StageAndUpgradeRequest();
                            stageAndUpgrade.Parameters = SolutionImportRequest.Parameters;

                            StageAndUpgradeResponse resp = (StageAndUpgradeResponse)serviceClient.Command_Execute(stageAndUpgrade, solutionNameForLogging + "Executing StageAndUpgradeRequest for ImportSolutionTo");
                            if (resp == null)
                                return Guid.Empty;
                            else
                            {
                                return importId;
                            }
                        }
                        else
                        {
                            ImportSolutionResponse resp = (ImportSolutionResponse)serviceClient.Command_Execute(SolutionImportRequest, solutionNameForLogging + "Executing ImportSolutionRequest for ImportSolution");
                            if (resp == null)
                                return Guid.Empty;
                            else
                                return importId;
                        }
                    }
                }
                #region Exception handlers for files
                catch (UnauthorizedAccessException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, Unauthorized Access to file: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (ArgumentNullException ex)
                {
                    serviceClient._logEntry.Log("************ Exception on ImportSolutionToCds, File path not specified", TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (ArgumentException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File path is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (PathTooLongException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File path is too long. Paths must be less than 248 characters, and file names must be less than 260 characters\n{0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (DirectoryNotFoundException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File path is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (FileNotFoundException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File Not Found: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                catch (NotSupportedException ex)
                {
                    serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File path or name is invalid: {0}", solutionPath), TraceEventType.Error, ex);
                    return Guid.Empty;
                }
                #endregion
            }
            else
            {
                serviceClient._logEntry.Log(string.Format(CultureInfo.InvariantCulture, "************ Exception on ImportSolution, File path specified in dataMapXml is not found: {0}", solutionPath), TraceEventType.Error);
                return Guid.Empty;
            }
        }
        #endregion
    }
}
