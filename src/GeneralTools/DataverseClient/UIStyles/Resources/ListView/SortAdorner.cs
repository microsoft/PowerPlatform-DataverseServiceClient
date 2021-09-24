using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Microsoft.PowerPlatform.Dataverse.Ui.Styles
{
	/// <summary>
	/// Creates a Adorner to indicate sort direction. 
	/// </summary>
	public class SortAdorner : Adorner
	{
		private readonly static Geometry _AscGeometry = Geometry.Parse("M 0,0 L 10,0 L 5,5 Z");
		private readonly static Geometry _DescGeometry = Geometry.Parse("M 0,5 L 10,5 L 5,0 Z");
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
		/// <summary>
		/// Gets or Sets Sort Direction.
		/// </summary>
		public ListSortDirection Direction { get; private set; }
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="element">UI Element type.</param>
		/// <param name="dir">Sorting Direction.</param>
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
		/// <summary>
		/// // Override the OnRender call to change the Drawing visual type.
		/// </summary>
		/// <param name="drawingContext">visual content drawing type.</param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			if (AdornedElement.RenderSize.Width < 20)
				return;

			drawingContext.PushTransform(new TranslateTransform(AdornedElement.RenderSize.Width - 15, (AdornedElement.RenderSize.Height - 5) / 2));

			drawingContext.DrawGeometry(SortAdornerFillBrush, null, Direction == ListSortDirection.Ascending ? _AscGeometry : _DescGeometry);

			drawingContext.Pop();
		}
	}

}
