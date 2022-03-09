namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// PickList Item
    /// </summary>
    public sealed class PickListItem
    {
        /// <summary>
        /// Display label for the PickList Item
        /// </summary>
        public string DisplayLabel { get; set; }
        /// <summary>
        /// ID of the picklist item
        /// </summary>
        public int PickListItemId { get; set; }

        /// <summary>
        /// Default Constructor
        /// </summary>
        public PickListItem()
        {
        }

        /// <summary>
        /// Constructor with data.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="id"></param>
        public PickListItem(string label, int id)
        {
            DisplayLabel = label;
            PickListItemId = id;
        }
    }
}
