#region using
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
    /// <summary>
    /// Interaction logic for AdvancedOptions.xaml
    /// </summary>
    public partial class AdvancedOptions : UserControl
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AdvancedOptions()
        {
            InitializeComponent();
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
            }
        }

        /// <summary>
        /// If true domain name textbox is visible
        /// </summary>
        public bool DomainVisible { get; set; } = false;
    }
}
