namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Used with GetFormIdsForEntity Call
    /// </summary>
    public enum FormTypeId
    {
        /// <summary>
        /// Dashboard form
        /// </summary>
        Dashboard = 0,
        /// <summary>
        /// Appointment book, for service requests.
        /// </summary>
        AppointmentBook = 1,
        /// <summary>
        /// Main or default form
        /// </summary>
        Main = 2,
        //MiniCampaignBo = 3,  // Not used in 2011
        //Preview = 4,          // Not used in 2011
        /// <summary>
        /// Mobile default form
        /// </summary>
        Mobile = 5,
        /// <summary>
        /// User defined forms
        /// </summary>
        Other = 100
    }
}
