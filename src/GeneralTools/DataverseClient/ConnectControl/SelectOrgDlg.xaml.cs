using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using Microsoft.PowerPlatform.Dataverse.Ui.Styles;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
	/// <summary>
	/// Provides a UI to allow users to select an Organization to login into, if the user has more than one organization available to login to.
	/// </summary>
	public partial class SelectOrgDlg : Window
	{
		#region vars
		private GridViewColumnHeader _CurSortCol = null;
		private SortAdorner _CurAdorner = null;
		#endregion

		/// <summary>
		/// returns the user selected CrmOrganization
		/// </summary>
		public OrgByServer GetSelectedCrmOrg
		{
			get
			{

				if (lvOrgList.SelectedItem != null && lvOrgList.SelectedItem is OrgByServer)
				{
					// Organization is selected. 
					return (OrgByServer)lvOrgList.SelectedItem;
				}
				return null;
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public SelectOrgDlg()
		{
			this.InitializeComponent();
		}

		/// <summary>
		/// Returns that the user selected a CRM org. 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnSelect_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (lvOrgList.SelectedItem != null && lvOrgList.SelectedItem is OrgByServer)
			{
				// Organization is selected. 
				DialogResult = true;
			}
		}

		/// <summary>
		/// On Window Load, Set the default sort.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// set default sort. 
			SetSort((GridViewColumnHeader)OrgCol.Header);
		}

		/// <summary>
		/// Sort button clicked
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Sort_Click(object sender, RoutedEventArgs e)
		{
			GridViewColumnHeader column = sender as GridViewColumnHeader;
			SetSort(column);
		}

		/// <summary>
		/// Sorts the grid view column
		/// </summary>
		/// <param name="column"></param>
		private void SetSort(GridViewColumnHeader column)
		{
			String field = column.Tag as String;

			if (_CurSortCol != null)
			{
				AdornerLayer.GetAdornerLayer(_CurSortCol).Remove(_CurAdorner);
				lvOrgList.Items.SortDescriptions.Clear();
			}

			ListSortDirection newDir = ListSortDirection.Ascending;
			if (_CurSortCol == column && _CurAdorner.Direction == newDir)
				newDir = ListSortDirection.Descending;

			_CurSortCol = column;
			_CurAdorner = new SortAdorner(_CurSortCol, newDir);
			AdornerLayer.GetAdornerLayer(_CurSortCol).Add(_CurAdorner);
			lvOrgList.Items.SortDescriptions.Add(
				new SortDescription(field, newDir));
		}
	}
	/// <summary>
	/// Creates a Adorner to indicate sort direction.
	/// This class was taken from http://www.switchonthecode.com/tutorials/wpf-tutorial-using-the-listview-part-2-sorting From this link code sort order icons (_DescGeometry,_AscGeometry) are paresed reversely,so it corrected in below. 
	/// </summary>
	public class SortAdorner : Adorner
	{
		private readonly static Geometry _DescGeometry =
		   Geometry.Parse("M 0,0 L 10,0 L 5,5 Z");
		private readonly static Geometry _AscGeometry =
		   Geometry.Parse("M 0,5 L 10,5 L 5,0 Z");

		private string sortAdornerBrushKey;
		/// <summary>
		/// 
		/// </summary>
		private Brush SortAdornerFillBrush
		{
			get
			{
				return (Brush)this.TryFindResource(sortAdornerBrushKey) ?? Brushes.Black;
			}
		}
		///<remarks/>
		public ListSortDirection Direction { get; private set; }

		///<remarks/>
		public SortAdorner(UIElement element, ListSortDirection dir)
			: base(element)
		{

			Direction = dir;
		}
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="element">UI Element type.</param>
		/// <param name="dir">Sorting Direction.</param>
		/// <param name="sortOrderBrushKey">Sort Brush Key</param>
		public SortAdorner(UIElement element, ListSortDirection dir, string sortOrderBrushKey)
			: this(element, dir)
		{
			this.sortAdornerBrushKey = sortOrderBrushKey;
		}
		///<remarks/>
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			if (AdornedElement.RenderSize.Width < 20)
				return;

			drawingContext.PushTransform(
				 new TranslateTransform(
				   AdornedElement.RenderSize.Width - 15,
				  (AdornedElement.RenderSize.Height - 5) / 2));

			drawingContext.DrawGeometry(SortAdornerFillBrush, null,
				Direction == ListSortDirection.Ascending ?
				  _AscGeometry : _DescGeometry);

			drawingContext.Pop();
		}


	}

}