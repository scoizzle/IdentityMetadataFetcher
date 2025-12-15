using System;
using System.Configuration;

namespace IdentityMetadataFetcher.Iis.Configuration
{
    /// <summary>
    /// Configuration section for SAML metadata polling in IIS applications.
    /// 
    /// Usage in Web.config:
    /// <code>
    /// <![CDATA[
    /// <configuration>
    ///   <configSections>
    ///     <section name="samlMetadataPolling" type="IdentityMetadataFetcher.Iis.Configuration.MetadataPollingConfigurationSection, IdentityMetadataFetcher.Iis" />
    ///   </configSections>
    ///   
    ///   <samlMetadataPolling enabled="true" autoApplyIdentityModel="false" pollingIntervalMinutes="60" 
    ///                         httpTimeoutSeconds="30" authFailureRecoveryIntervalMinutes="5">
    ///     <!-- Set autoApplyIdentityModel to true to enable runtime IdentityModel updates -->
    ///     <!-- Default is false (no automatic application). -->
    ///     <!-- authFailureRecoveryIntervalMinutes controls minimum time between forced metadata refreshes on auth failures -->
    ///     <issuers>
    ///       <add id="azure-ad" 
    ///            endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
    ///            name="Azure AD" />
    ///       <add id="auth0" 
    ///            endpoint="https://example.auth0.com/samlp/metadata" 
    ///            name="Auth0" />
    ///     </issuers>
    ///   </samlMetadataPolling>
    /// </configuration>
    /// ]]>
    /// </code>
    /// </summary>
    public class MetadataPollingConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to automatically apply fetched metadata
        /// to System.IdentityModel at runtime. Default is false.
        /// </summary>
        [ConfigurationProperty("autoApplyIdentityModel", DefaultValue = false, IsRequired = false)]
        public bool AutoApplyIdentityModel
        {
            get { return (bool)this["autoApplyIdentityModel"]; }
            set { this["autoApplyIdentityModel"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether metadata polling is enabled.
        /// </summary>
        [ConfigurationProperty("enabled", DefaultValue = true, IsRequired = false)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
            set { this["enabled"] = value; }
        }

        /// <summary>
        /// Gets or sets the polling interval in minutes.
        /// Default is 60 minutes.
        /// </summary>
        [ConfigurationProperty("pollingIntervalMinutes", DefaultValue = 60, IsRequired = false)]
        [IntegerValidator(MinValue = 1, MaxValue = 10080)] // Max 7 days
        public int PollingIntervalMinutes
        {
            get { return (int)this["pollingIntervalMinutes"]; }
            set { this["pollingIntervalMinutes"] = value; }
        }

        /// <summary>
        /// Gets or sets the HTTP timeout in seconds for metadata requests.
        /// Default is 30 seconds.
        /// </summary>
        [ConfigurationProperty("httpTimeoutSeconds", DefaultValue = 30, IsRequired = false)]
        [IntegerValidator(MinValue = 5, MaxValue = 300)]
        public int HttpTimeoutSeconds
        {
            get { return (int)this["httpTimeoutSeconds"]; }
            set { this["httpTimeoutSeconds"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to validate SSL/TLS certificates.
        /// Default is true.
        /// </summary>
        [ConfigurationProperty("validateServerCertificate", DefaultValue = true, IsRequired = false)]
        public bool ValidateServerCertificate
        {
            get { return (bool)this["validateServerCertificate"]; }
            set { this["validateServerCertificate"] = value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of retries for failed metadata requests.
        /// Default is 2.
        /// </summary>
        [ConfigurationProperty("maxRetries", DefaultValue = 2, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = 5)]
        public int MaxRetries
        {
            get { return (int)this["maxRetries"]; }
            set { this["maxRetries"] = value; }
        }

        /// <summary>
        /// Gets or sets the minimum interval in minutes between forced metadata refreshes
        /// triggered by authentication failures. This prevents excessive polling when
        /// authentication failures occur in rapid succession.
        /// Default is 5 minutes.
        /// </summary>
        [ConfigurationProperty("authFailureRecoveryIntervalMinutes", DefaultValue = 5, IsRequired = false)]
        [IntegerValidator(MinValue = 1, MaxValue = 60)]
        public int AuthFailureRecoveryIntervalMinutes
        {
            get { return (int)this["authFailureRecoveryIntervalMinutes"]; }
            set { this["authFailureRecoveryIntervalMinutes"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to attempt synchronous recovery
        /// for authentication failures, allowing the current request to potentially succeed.
        /// When true, the module will wait (up to 10 seconds) for metadata refresh before
        /// failing the request, then redirect to retry authentication.
        /// When false (default), recovery happens asynchronously and only benefits subsequent requests.
        /// Default is false for better performance and to avoid request timeouts.
        /// </summary>
        [ConfigurationProperty("enableSynchronousRecovery", DefaultValue = false, IsRequired = false)]
        public bool EnableSynchronousRecovery
        {
            get { return (bool)this["enableSynchronousRecovery"]; }
            set { this["enableSynchronousRecovery"] = value; }
        }

        /// <summary>
        /// Gets the collection of issuer endpoints to poll.
        /// </summary>
        [ConfigurationProperty("issuers", IsDefaultCollection = false, IsRequired = true)]
        public IssuerElementCollection Issuers
        {
            get { return (IssuerElementCollection)this["issuers"]; }
        }
    }
}
