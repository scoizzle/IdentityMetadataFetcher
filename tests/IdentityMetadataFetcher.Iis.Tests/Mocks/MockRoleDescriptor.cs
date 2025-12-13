using System;
using Microsoft.IdentityModel.Protocols.WsFederation.Metadata;

namespace IdentityMetadataFetcher.Iis.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of RoleDescriptor for testing purposes
    /// </summary>
    public class MockRoleDescriptor : RoleDescriptor
    {
        public MockRoleDescriptor() : base()
        {
        }
    }
}
