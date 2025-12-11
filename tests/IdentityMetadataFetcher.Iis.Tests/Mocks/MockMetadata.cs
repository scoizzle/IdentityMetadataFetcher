using System;
using System.Collections.Generic;
using System.Xml;
using System.IdentityModel.Metadata;

namespace IdentityMetadataFetcher.Iis.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of MetadataBase for testing purposes
    /// </summary>
    public class MockMetadata : MetadataBase
    {
        public override void WriteAsXml(XmlWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            
            writer.WriteStartElement("EntityDescriptor");
            writer.WriteAttributeString("xmlns", "urn:oasis:names:tc:SAML:2.0:metadata");
            writer.WriteAttributeString("ID", Guid.NewGuid().ToString());
            writer.WriteEndElement();
        }

        public override IEnumerable<string> GetSchema()
        {
            return new List<string>();
        }
    }
}
