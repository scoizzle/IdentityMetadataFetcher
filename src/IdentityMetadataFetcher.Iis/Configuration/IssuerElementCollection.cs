using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Iis.Configuration
{
    /// <summary>
    /// Collection of issuer configuration elements.
    /// </summary>
    [ConfigurationCollection(typeof(IssuerElement), AddItemName = "add", RemoveItemName = "remove", ClearItemsName = "clear")]
    public class IssuerElementCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Gets the element key for the specified configuration element.
        /// </summary>
        protected override object GetElementKey(ConfigurationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var issuerElement = element as IssuerElement;
            return issuerElement?.Id;
        }

        /// <summary>
        /// Creates a new configuration element when overridden in a derived class.
        /// </summary>
        protected override ConfigurationElement CreateNewElement()
        {
            return new IssuerElement();
        }

        /// <summary>
        /// Gets the issuer element at the specified index.
        /// </summary>
        public IssuerElement this[int index]
        {
            get { return (IssuerElement)BaseGet(index); }
        }

        /// <summary>
        /// Gets the issuer element with the specified id.
        /// </summary>
        public new IssuerElement this[string id]
        {
            get { return (IssuerElement)BaseGet(id); }
        }

        /// <summary>
        /// Adds an issuer element to the collection.
        /// </summary>
        public void Add(IssuerElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            BaseAdd(element);
        }

        /// <summary>
        /// Removes an issuer element from the collection.
        /// </summary>
        public void Remove(IssuerElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            BaseRemove(element.Id);
        }

        /// <summary>
        /// Clears all issuer elements from the collection.
        /// </summary>
        public void Clear()
        {
            BaseClear();
        }

        /// <summary>
        /// Determines whether the collection contains the specified element.
        /// </summary>
        public bool Contains(IssuerElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            return BaseIndexOf(element) >= 0;
        }

        /// <summary>
        /// Converts all configuration elements to IssuerEndpoint objects.
        /// </summary>
        public IEnumerable<IssuerEndpoint> ToIssuerEndpoints()
        {
            var endpoints = new List<IssuerEndpoint>();
            for (int i = 0; i < Count; i++)
            {
                var element = this[i];
                endpoints.Add(element.ToIssuerEndpoint());
            }
            return endpoints;
        }
    }
}
