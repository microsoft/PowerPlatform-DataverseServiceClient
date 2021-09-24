using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
    /// <summary>
    /// Interaction logic for InstanceUrlCapture.xaml
    /// </summary>
    public partial class InstanceUrlCapture : Window
    {
        /// <summary>
        /// constructor to window used to capture URL for direct connect.
        /// </summary>
        public InstanceUrlCapture()
        {
            InitializeComponent();
            tbConnectUrl.Text = string.Empty;
        }

        /// <summary>
        /// Click response. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbConnectUrl.Text))
            {
                MessageBox.Show("Instance URL is required.", Properties.Resources.INSTANCEURLCAPTURE_URL_Error, MessageBoxButton.OK, MessageBoxImage.Hand);
                return;
                
            }

            if (!Uri.IsWellFormedUriString(tbConnectUrl.Text, UriKind.Absolute))
            {
                // Not a valid URL. 
                MessageBox.Show(string.Format("{0} is not a well formed URL", tbConnectUrl.Text), Properties.Resources.INSTANCEURLCAPTURE_URL_Error, MessageBoxButton.OK, MessageBoxImage.Hand);
                return; 
            }
            else
            {
                // stamp in orgURL
                // XRMServices/2011/Organization.svc
                Uri hold = new Uri(tbConnectUrl.Text);
                tbConnectUrl.Text = string.Format("{0}://{1}", hold.Scheme, hold.DnsSafeHost);
                DialogResult = true;
                Close(); 
            }
        }
    }
}
