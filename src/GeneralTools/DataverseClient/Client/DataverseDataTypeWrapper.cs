using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	/// <summary>
	/// Allowed typelist of entities
	/// </summary>
	public enum DataverseFieldType
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
		/// Decimal, for money, use Money - Converts from a decimal type
		/// </summary>
		Decimal,
		/// <summary>
		/// Double type, while Dataverse calls this a float, it is actually a double for money, use Money - Converts from a double type
		/// </summary>
		Float,
		/// <summary>
		/// Money Type - Converts from a decimal type
		/// </summary>
		Money,
		/// <summary>
		/// Whole number - Converts from a Int type
		/// </summary>
		Number,
		/// <summary>
		/// Ref Type for Dataverse,  Creates an EntityReference 
		/// You need to provide a Guid as a value, and a the name of an entity for the lookup key 
		/// </summary>
		Customer,
		/// <summary>
		/// Primary Key - Converts from a Guid Type
		/// </summary>
		Key,
		/// <summary>
		/// Ref Type for Dataverse,  Creates an EntityReference 
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
        /// Use image columns to display a single image per row in the application
        /// </summary>
        Image,
        /// <summary>
        /// The File column is used for storing binary data
        /// </summary>
        File,
        /// <summary>
        /// User Specified type... will be appended directly. This type must be one of the valid Dataverse types
        /// </summary>
        Raw
    }

	/// <summary>
	/// Contains a variable definition. 
	/// </summary>
	public class DataverseDataTypeWrapper
	{
		/// <summary>
		/// Value to set
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// Value Type.
		/// </summary>
		public DataverseFieldType Type { get; set; }

		/// <summary>
		/// Name of the entity that a Lookup or Related Customer Entity
		/// </summary>
		public string ReferencedEntity { get; set; }

		/// <summary>
		/// Create a new Data Type
		/// Default Constructor
		/// </summary>
		public DataverseDataTypeWrapper()
		{ }

		/// <summary>
		/// Create a new Data Type 
		/// </summary>
		/// <param name="data">Data to Set</param>
		/// <param name="CdsFieldType">Type of Data to Set</param>
		public DataverseDataTypeWrapper(object data, DataverseFieldType CdsFieldType)
		{
			Value = data;
			Type = CdsFieldType;
			ReferencedEntity = string.Empty;
		}

		/// <summary>
		/// Create a new Data Type 
		/// </summary>
		/// <param name="data">Data to Set</param>
		/// <param name="CdsFieldType">Type of Data to Set</param>
		/// <param name="relatedEntityName">Name of the related entity, applies to the Field Types: Customer and Lookup</param>
		public DataverseDataTypeWrapper(object data, DataverseFieldType CdsFieldType, string relatedEntityName)
		{
			Value = data;
			Type = CdsFieldType;
			ReferencedEntity = relatedEntityName;
		}
	}


}
