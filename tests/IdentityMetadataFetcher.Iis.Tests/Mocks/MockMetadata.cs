using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;

namespace IdentityMetadataFetcher.Iis.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of WsFederationConfiguration for testing purposes
    /// </summary>
    public class MockMetadata : WsFederationConfiguration
    {
        public MockMetadata()
        {
            Issuer = "https://example.com/entity";
            TokenEndpoint = "https://example.com/token";
            // SigningKeys is readonly, items are added via .Add() method
        }
    }
}
