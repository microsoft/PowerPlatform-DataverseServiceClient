//===============================================================================
// MICROSOFT TEMPLATE CODE
// Microsoft Dynamics CRM 2010
// Project: Dynamics CRM Connect Control
// FILES:   ErrorWindow.cs                   
// PURPOSE: Error Display Window
//===============================================================================
// Copyright 2012 Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

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
using System.Globalization;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
	/// <summary>
	/// Interaction logic for ErrorWindow.xaml
	/// </summary>
	public partial class ErrorWindow : Window
	{
		/// <summary>
		/// Error window constructor
		/// </summary>
		/// <param name="e"></param>
		public ErrorWindow(Exception e)
		{
			InitializeComponent();
			if (CultureUtils.UICulture.TextInfo.IsRightToLeft)
			{
				this.FlowDirection = System.Windows.FlowDirection.RightToLeft;
			}
			if (e != null)
			{
				ErrorTextBox.Text = e.Message + Environment.NewLine + Environment.NewLine + e.StackTrace;
			}
		}

		/// <summary>
		/// Error window constructor
		/// </summary>
		/// <param name="uri"></param>
		public ErrorWindow(Uri uri)
		{
			InitializeComponent();
			if (CultureUtils.UICulture.TextInfo.IsRightToLeft)
			{
				this.FlowDirection = System.Windows.FlowDirection.RightToLeft;
			}
			if (uri != null)
			{
				ErrorTextBox.Text = "Page not found: \"" + uri.ToString() + "\"";
			}
		}

		/// <summary>
		/// Error window constructor
		/// </summary>
		/// <param name="message"></param>
		/// <param name="details"></param>
		public ErrorWindow(string message, string details)
		{
			InitializeComponent();
			if (CultureUtils.UICulture.TextInfo.IsRightToLeft)
			{
				this.FlowDirection = System.Windows.FlowDirection.RightToLeft;
			}
			ErrorTextBox.Text = message + Environment.NewLine + Environment.NewLine + details;
		}

		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}
	}
}