using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Allowed typelist of entities
	/// </summary>
	public enum CdsFieldType
	{
		/// <summary>
		/// Bool - Converts from bool
		/// </summary>
		Boolean,
		/// <summary>
		/// DateTime - Converts from a DataTime Object
		/// </summary>
		DateTime,
		/// <summary>
		/// Decimal, for money, use CrmMoney - Converts from a decimal type
		/// </summary>
		Decimal,
		/// <summary>
		/// Double type, while CRM calls this a float, it is actually a double for money, use CrmMoney - Converts from a double type
		/// </summary>
		Float,
		/// <summary>
		/// Money Type - Converts from a decimal type
		/// </summary>
		Money,
		/// <summary>
		/// CRM whole number - Converts from a Int type
		/// </summary>
		Number,
		/// <summary>
		/// Ref Type for CDS,  Creates an EntityReference
		/// You need to provide a Guid as a value, and a the name of an entity for the lookup key
		/// </summary>
		Customer,
		/// <summary>
		/// Primary Key - Converts from a Guid Type
		/// </summary>
		Key,
		/// <summary>
		/// Ref Type for CDS,  Creates an EntityReference
		/// You need to provide a Guid as a value, and a the name of an entity for the lookup key
		/// </summary>
		Lookup,
		/// <summary>
		/// Pick List value - Converts from a Int type
		/// </summary>
		[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="Picklist")]
		Picklist,
		/// <summary>
		/// String type - Converts from a string type.
		/// </summary>
		String,
		/// <summary>
		/// Guid Type - Converts from a Guid Type
		/// </summary>
		UniqueIdentifier,
		/// <summary>
		/// User Specified type... will be appended directly. This type must be one of the valid CRM 2011 types
		/// </summary>
		Raw


	}

	/// <summary>
	/// Contains a variable definition.
	/// </summary>
	public class CdsDataTypeWrapper
	{
		/// <summary>
		/// Value to set
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// Value Type.
		/// </summary>
		public CdsFieldType Type { get; set; }

		/// <summary>
		/// Name of the entity that a Lookup or Related Customer Entity
		/// </summary>
		public string ReferencedEntity { get; set; }

		/// <summary>
		/// Create a new CRM Data Type
		/// Default Constructor
		/// </summary>
		public CdsDataTypeWrapper()
		{ }

		/// <summary>
		/// Create a new CDS Data Type
		/// </summary>
		/// <param name="data">Data to Set</param>
		/// <param name="CdsFieldType">Type of Data to Set</param>
		public CdsDataTypeWrapper(object data, CdsFieldType CdsFieldType)
		{
			Value = data;
			Type = CdsFieldType;
			ReferencedEntity = string.Empty;
		}

		/// <summary>
		/// Create a new CDS Data Type
		/// </summary>
		/// <param name="data">Data to Set</param>
		/// <param name="CdsFieldType">Type of Data to Set</param>
		/// <param name="relatedEntityName">Name of the related entity, applies to the Field Types: Customer and Lookup</param>
		public CdsDataTypeWrapper(object data, CdsFieldType CdsFieldType, string relatedEntityName)
		{
			Value = data;
			Type = CdsFieldType;
			ReferencedEntity = relatedEntityName;
		}
	}


}
