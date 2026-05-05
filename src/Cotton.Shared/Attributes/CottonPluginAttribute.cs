using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Attributes
{
    /// <summary>
    /// Indicates that a class is a plugin for the Cotton Cloud.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CottonPluginAttribute : Attribute
    {
        public string PluginId { get; }

        public CottonPluginAttribute(string pluginId)
        {
            PluginId = pluginId;
        }
    }
}
