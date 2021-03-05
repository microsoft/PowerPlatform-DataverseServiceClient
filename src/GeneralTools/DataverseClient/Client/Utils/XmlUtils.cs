namespace Microsoft.PowerPlatform.Dataverse.Client
{
	using System.IO;
	using System.Text;
	using System.Xml;

	/// <summary>
	/// Utility class for XML related operations.
	/// </summary>
	public class XmlUtil
	{
		/// <summary>
		/// Prevent XmlUtil from ever being constructed
		/// </summary>
		protected XmlUtil() { }

		/// <summary>
		/// Creates an XmlReader object with secure default property values.
		/// </summary>
		/// <param name="xml">The string to get the data from.</param>
		/// <returns>the new XmlReader object</returns>
		public static XmlReader CreateXmlReader(string xml)
		{
			return CreateXmlReader(xml, false);
		}

		/// <summary>
		/// Creates an XmlReader object with secure default property values and given whitespace setting.
		/// </summary>
		/// <param name="xml">The string to get the data from.</param>
		/// <param name="preserveWhiteSpace">Whether the whitespaces are to be preserved or not.</param>
		/// <returns>the new XmlReader object</returns>
		public static XmlReader CreateXmlReader(string xml, bool preserveWhiteSpace)
		{
			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = !preserveWhiteSpace;

			return XmlReader.Create(new StringReader(xml), settings);
		}

		/// <summary>
		/// Creates an XmlReader object with secure default property values.
		/// </summary>
		/// <param name="xmlStream">Xml stream.</param>
		/// <returns>The new XmlReader object.</returns>
		public static XmlReader CreateXmlReader(Stream xmlStream)
		{
			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			XmlReader reader = XmlReader.Create(xmlStream, settings);

			return reader;
		}

		/// <summary>
		/// Creates an XmlDocument object with secure default property values.
		/// </summary>
		/// <returns>the new XmlDocument object</returns>
		public static XmlDocument CreateXmlDocument()
		{
			XmlDocument xmlDoc = new XmlDocument();

			// Set XmlResolver to null to prevent usage of the default XmlResolver.
			xmlDoc.XmlResolver = null;

			return xmlDoc;
		}
		/// <summary>
		/// Creates an XmlDocument object with secure default property values.
		/// </summary>
		/// <param name="reader">The XmlTextReader to get the data from.</param>
		/// <returns>the new XmlDocument object</returns>
		public static XmlDocument CreateXmlDocument(XmlReader reader)
		{
			XmlDocument xmlDoc = new XmlDocument();

			// Set XmlResolver to null to prevent usage of the default XmlResolver.
			xmlDoc.XmlResolver = null;
			xmlDoc.Load(reader);

			reader.Close();

			return xmlDoc;
		}

		/// <summary>
		/// Creates an XmlDocument object with secure default property values.
		/// Extracts xml from the given stream and reads it into the XmlDocument.
		/// </summary>
		/// <param name="input">The XML stream to load.</param>
		/// <returns>the new XmlDocument object</returns>
		public static XmlDocument CreateXmlDocument(Stream input)
		{
			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			using (XmlReader xmlReader = XmlReader.Create(input, settings))
			{
				return XmlUtil.CreateXmlDocument(xmlReader);
			}
		}

		/// <summary>
		/// Creates an XmlDocument object with secure default property values.
		/// Loads the given XML into the XmlDocument.
		/// </summary>
		/// <param name="xml">The XML to load.</param>
		/// <returns>the new XmlDocument object</returns>
		public static XmlDocument CreateXmlDocument(string xml)
		{
			//If no xml to load, create an empty XmlDocument.
			if (xml == null || xml.Length == 0)
			{
				XmlDocument xmlDoc = new XmlDocument();

				// Set XmlResolver to null to prevent usage of the default XmlResolver.
				xmlDoc.XmlResolver = null;
				return xmlDoc;
			}

			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			using (StringReader reader = new StringReader(xml))
			{
				using (XmlReader xmlReader = XmlReader.Create(reader, settings))
				{
					return XmlUtil.CreateXmlDocument(xmlReader);
				}
			}
		}

		/// <summary>
		/// Creates an XmlDocument object with secure default property values.
		/// Loads the given XML into the XmlDocument.
		/// This overload is useful when a whitespace only element value is valid content.
		/// </summary>
		/// <param name="xml">The XML to load.</param>
		/// <param name="preserveWhiteSpace">Whether the whitespaces are to be preserved or not.</param>
		/// <returns>the new XmlDocument object</returns>
		public static XmlDocument CreateXmlDocument(string xml, bool preserveWhiteSpace)
		{
			XmlDocument xmlDoc = XmlUtil.CreateXmlDocument();

			//If no xml to load, return an empty XmlDocument.
			if (string.IsNullOrEmpty(xml))
			{
				return xmlDoc;
			}

			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = !preserveWhiteSpace;
			xmlDoc.PreserveWhitespace = preserveWhiteSpace;

			using (StringReader reader = new StringReader(xml))
			{
				using (XmlReader xmlReader = XmlReader.Create(reader, settings))
				{
					xmlDoc.Load(xmlReader);
				}
			}
			return xmlDoc;
		}

		/// <summary>
		/// Creates an XmlWriter on top of the provided TextWriter as per
		/// the .Net Framework guidelines.
		/// </summary>
		/// <param name="textWriter">TextWriter to write into</param>
		/// <param name="indented">True to indent the output</param>
		/// <returns>An XmlWriter</returns>
		public static XmlWriter CreateXmlWriter(TextWriter textWriter, bool indented)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Encoding = Encoding.UTF8;
			settings.Indent = indented;
			settings.OmitXmlDeclaration = true;
			return XmlWriter.Create(textWriter, settings);
		}

		/// <summary>
		/// Creates an XmlWriter which writes to the specified filename using
		/// the specified encoding.
		/// </summary>
		/// <param name="fileName">File to write to</param>
		/// <param name="encoding">Encoding to use</param>
		/// <param name="indented">True to indent the output</param>
		/// <returns>An XmlWriter</returns>
		public static XmlWriter CreateXmlWriter(string fileName, Encoding encoding, bool indented)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Encoding = encoding;
			settings.Indent = indented;
			settings.OmitXmlDeclaration = true;
			return XmlWriter.Create(fileName, settings);
		}
	}
}
