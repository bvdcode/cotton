using System;

namespace Cotton.Attributes
{
    /// <summary>
    /// Indicates that a class is a plugin for the Cotton Cloud.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CottonPluginAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique identifier of the plugin, ex. cotton.company.pluginname.
        /// Must be unique across all plugins and follow a reverse domain name notation.
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// Gets the name of the author associated with the content, ex. Vadim Belov.
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// Gets the website URL associated with the entity.
        /// </summary>
        public string Website { get; }

        /// <summary>
        /// Gets the name associated with this instance.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description associated with this instance.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initializes a new instance of the CottonPluginAttribute class with the specified plugin identifier, author,
        /// and website.
        /// </summary>
        /// <param name="pluginId">The unique identifier for the plugin. Cannot be null or empty.</param>
        /// <param name="name">The name of the plugin. Cannot be null or empty.</param>
        /// <param name="description">A brief description of the plugin. Cannot be null or empty.</param>
        /// <param name="author">The name of the author of the plugin. Cannot be null or empty.</param>
        /// <param name="website">The website URL associated with the plugin or its author. Cannot be null or empty.</param>
        public CottonPluginAttribute(
            string pluginId,
            string name,
            string description,
            string author,
            string website)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description cannot be null or empty.", nameof(description));
            }
            if (string.IsNullOrWhiteSpace(author))
            {
                throw new ArgumentException("Author cannot be null or empty.", nameof(author));
            }
            if (string.IsNullOrWhiteSpace(website))
            {
                throw new ArgumentException("Website cannot be null or empty.", nameof(website));
            }
            if (pluginId.Contains(" "))
            {
                throw new ArgumentException("Plugin ID cannot contain spaces.", nameof(pluginId));
            }

            PluginId = pluginId;
            Name = name;
            Description = description;
            Author = author;
            Website = website;
        }
    }
}
