using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.IdentityModel.Protocols.WsFederation.Metadata;

namespace IdentityMetadataFetcher.Iis.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of MetadataBase for testing purposes
    /// </summary>
    public class MockMetadata : EntityDescriptor
    {
        public MockMetadata() : base()
        {
            EntityId = new EntityId("https://example.com/entity");
        }
    }
}
