using System;
using System.Linq;
using System.Windows;

namespace Microsoft.PowerPlatform.Dataverse.Ui.Styles
{
	/// <summary>
	/// WindowResourceDictionary
	/// </summary>
	public partial class WindowResourceDictionary : ResourceDictionary
	{
		/// <summary>
		/// Mouse down event handler for the window top bar
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void WindowTopBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			try
			{
				Window activeWindow = this.GetCurrentActiveWindow();
				if (activeWindow != null)
				{
					if (activeWindow.ResizeMode == ResizeMode.CanResize && e.ClickCount == 2)
					{
						activeWindow.WindowState = activeWindow.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
					}
					else
					{
						activeWindow.DragMove();
					}
				}
			}
			catch(Exception){}
		}

		/// <summary>
		/// This method can be used to get the handle to the currently active window
		/// </summary>
		/// <returns>Currently active window.</returns>
		private Window GetCurrentActiveWindow()
		{
			var activeWindow = Application.Current.Windows.OfType<Window>().Where(win => win.IsActive);
			return activeWindow.FirstOrDefault();
		}
	}
}
