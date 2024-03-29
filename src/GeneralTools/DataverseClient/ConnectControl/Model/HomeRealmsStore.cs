﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by xsd, Version=4.0.30319.1.
// 
namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Model
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.ComponentModel;
	using System.Collections.ObjectModel;


	/// <remarks/>
	[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
	[System.SerializableAttribute()]
	[System.Diagnostics.DebuggerStepThroughAttribute()]
	[System.ComponentModel.DesignerCategoryAttribute("code")]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
	[System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
	public class ClaimsHomeRealmOptions : object, System.ComponentModel.INotifyPropertyChanged
	{

		private ObservableCollection<ClaimsHomeRealmOptionsHomeRealm> itemsField;

		/// <remarks/>
		[System.Xml.Serialization.XmlElementAttribute("HomeRealm", Form = System.Xml.Schema.XmlSchemaForm.Unqualified)]
		public ObservableCollection<ClaimsHomeRealmOptionsHomeRealm> Items
		{
			get
			{
				if (this.itemsField == null) this.itemsField = new ObservableCollection<ClaimsHomeRealmOptionsHomeRealm>();
				return this.itemsField;
			}
			set
			{
				this.itemsField = value;
				this.RaisePropertyChanged("Items");
			}
		}

		/// <summary>
		/// Finds the Home Realm information for the Display Name 
		/// </summary>
		/// <param name="shortName">Display Name of the server you are looking for</param>
		/// <returns>ClaimsHomeRealmOptionsHomeRealm Data or Null</returns>
		public ClaimsHomeRealmOptionsHomeRealm GetServerByDisplayName(string shortName)
		{
			if (itemsField != null)
				return itemsField.FirstOrDefault(i => i.DisplayName.Equals(shortName, StringComparison.CurrentCultureIgnoreCase));
			return null;
		}

		/// <summary>
		/// Default Propertly changed event.
		/// </summary>
		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raised when a property changes
		/// </summary>
		/// <param name="propertyName"></param>
		protected void RaisePropertyChanged(string propertyName)
		{
			System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
			if ((propertyChanged != null))
			{
				propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
			}
		}
	}

	/// <remarks/>
	[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
	[System.SerializableAttribute()]
	[System.Diagnostics.DebuggerStepThroughAttribute()]
	[System.ComponentModel.DesignerCategoryAttribute("code")]
	[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
	public class ClaimsHomeRealmOptionsHomeRealm : object, System.ComponentModel.INotifyPropertyChanged
	{

		private string displayNameField;

		private string uriField;

		/// <remarks/>
		[System.Xml.Serialization.XmlAttributeAttribute()]
		public string DisplayName
		{
			get
			{
				return this.displayNameField;
			}
			set
			{
				this.displayNameField = value;
				this.RaisePropertyChanged("DisplayName");
			}
		}

		/// <remarks/>
		[System.Xml.Serialization.XmlAttributeAttribute()]
		public string Uri
		{
			get
			{
				return this.uriField;
			}
			set
			{
				this.uriField = value;
				this.RaisePropertyChanged("Uri");
			}
		}
        /// <summary>
        /// Default override of string to support Accessibility 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return DisplayName;
        }

        /// <summary/>
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
		/// <summary/>
		protected void RaisePropertyChanged(string propertyName)
		{
			System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
			if ((propertyChanged != null))
			{
				propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
