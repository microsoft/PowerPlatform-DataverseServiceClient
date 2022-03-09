using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// PickList data
    /// </summary>
    public sealed class PickListMetaElement
    {
        /// <summary>
        /// Current value of the PickList Item
        /// </summary>
        public string ActualValue { get; set; }
        /// <summary>
        /// Displayed Label
        /// </summary>
        public string PickListLabel { get; set; }
        /// <summary>
        /// Displayed value for the PickList
        /// </summary>
        public string DisplayValue { get; set; }
        /// <summary>
        /// Array of Potential Pick List Items.
        /// </summary>
        public List<PickListItem> Items { get; set; }

        /// <summary>
        /// Default Constructor
        /// </summary>
        public PickListMetaElement()
        {
            Items = new List<PickListItem>();
        }

        /// <summary>
        /// Constructs a PickList item with data.
        /// </summary>
        /// <param name="actualValue"></param>
        /// <param name="displayValue"></param>
        /// <param name="pickListLabel"></param>
        public PickListMetaElement(string actualValue, string displayValue, string pickListLabel)
        {
            Items = new List<PickListItem>();
            ActualValue = actualValue;
            PickListLabel = pickListLabel;
            DisplayValue = displayValue;
        }
    }
}
