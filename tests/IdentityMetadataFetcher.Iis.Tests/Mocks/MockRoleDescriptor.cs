using System;
using Microsoft.IdentityModel.Tokens;

namespace IdentityMetadataFetcher.Iis.Tests.Mocks
{
    /// <summary>
    /// Mock security key for testing purposes (replaces RoleDescriptor)
    /// </summary>
    public class MockSecurityKey : SecurityKey
    {
        private const int DefaultKeySize = 2048;

        public MockSecurityKey()
        {
            KeyId = "mock-key-id";
        }

        public override int KeySize => DefaultKeySize;
    }
}
