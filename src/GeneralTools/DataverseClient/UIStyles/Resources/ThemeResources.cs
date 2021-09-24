using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Microsoft.PowerPlatform.Dataverse.Ui.Styles.Resources
{
	/// <summary>
	/// Extends ResourceDictionary class to load theme specific resource dictionary
	/// </summary>
	public class ThemeResources : ResourceDictionary
	{

		private string normalModeResourcesPath;
		private string hcModeResourcesPath;

		/// <summary>
		/// Path for resource dictionary containing normal theme solid color brushes
		/// </summary>
		public virtual string NormalModeResourcesPath
		{
			get
			{
				return normalModeResourcesPath;
			}
			set
			{
				if (normalModeResourcesPath != value)
				{
					normalModeResourcesPath = value;
					AddOrOverrideResources();
				}
			}
		}

		/// <summary>
		/// Path for resource dictionary containing high contrast theme solid color brushes
		/// </summary>
		public virtual string HCModeResourcesPath
		{
			get
			{
				return hcModeResourcesPath;
			}
			set
			{
				if (hcModeResourcesPath != value)
				{
					hcModeResourcesPath = value;
					AddOrOverrideResources();
				}
			}

		}

		/// <summary>
		/// Extends ResourceDictionary class to load theme specific resource dictionary
		/// </summary>
		public ThemeResources()
		{
			try
			{
				HighContrastSettingsEventManager.AddHandler(SystemParameters_StaticPropertyChanged);
			}
			catch
			{
			}
		}

		/// <summary>
		/// Handler to dynamically override resources according to current theme
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "HighContrast")
			{
				AddOrOverrideResources();
			}
		}

		/// <summary>
		/// Adds or override resources to the dictionary according to current theme
		/// </summary>
		public virtual void AddOrOverrideResources()
		{
			string dictionaryName = (SystemParameters.HighContrast) ? HCModeResourcesPath : NormalModeResourcesPath;
			if (!string.IsNullOrWhiteSpace(dictionaryName))
			{
				try
				{
					ResourceDictionary res = Application.LoadComponent(new Uri(dictionaryName, UriKind.RelativeOrAbsolute)) as ResourceDictionary;
					this.MergedDictionaries.Add(res);
				}
				catch
				{
				}
			}
		}
	}
}
