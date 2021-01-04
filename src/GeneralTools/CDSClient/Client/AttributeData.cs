namespace Microsoft.PowerPlatform.Cds.Client
{
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Metadata;
	using System;
	using System.Diagnostics.CodeAnalysis;

	/// <summary>
	/// Summary description for AttributeData
	/// </summary>
	public class AttributeData
	{
		// These three properties apply to all attributes
		private String attributeLabel;
		private AttributeTypeCode attributeType;
		private String schemaName;

		// These two properties apply to attributes that are tied to a record and have data
		private String displayValue;
		private Object actualValue;

		private Boolean isUnsupported;

		/// <summary>
		///
		/// </summary>
		public Object ActualValue
		{
			get { return actualValue; }
			set { actualValue = value; }
		}

		/// <summary>
		///
		/// </summary>
		public String AttributeLabel
		{
			get { return attributeLabel; }
			set { attributeLabel = value; }
		}

		/// <summary>
		///
		/// </summary>
		public AttributeTypeCode AttributeType
		{
			get { return attributeType; }
			set { attributeType = value; }
		}

		/// <summary>
		///
		/// </summary>
		public String DisplayValue
		{
			get { return displayValue; }
			set { displayValue = value; }
		}
		/// <summary>
		///
		/// </summary>
		public Boolean IsUnsupported
		{
			get { return isUnsupported; }
			set { isUnsupported = value; }
		}

		/// <summary>
		///
		/// </summary>
		public String SchemaName
		{
			get { return schemaName; }
			set { schemaName = value; }
		}
	}

	/// <summary>
	///
	/// </summary>
	public sealed class BooleanAttributeData : AttributeData
	{
		/// <summary>
		///
		/// </summary>
		public OptionMetadata[] BooleanOptions { get; set; }
	}
	/// <summary>
	///
	/// </summary>
	public sealed class StringAttributeData : AttributeData
	{
		private int maxLength;
		/// <summary>
		///
		/// </summary>
		public int MaxLength
		{
			get { return maxLength; }
			set { maxLength = value; }
		}
	}
	/// <summary>
	///
	/// </summary>
	[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Picklist")]
	public sealed class PicklistAttributeData : AttributeData
	{
		private OptionMetadata[] picklistOptions;
		/// <summary>
		///
		/// </summary>
		[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Picklist")]
		public OptionMetadata[] PicklistOptions
		{
			get { return picklistOptions; }
			set { picklistOptions = value; }
		}
	}
}