using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Baker.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Baker
{
    /// <summary>
    /// Site configuration file.
    /// </summary>
    public sealed class SiteConfig : YamlObject
    {
        /// <summary>
        /// Default name of the site configuration file.
        /// </summary>
        public static readonly string Name = "_config.yaml";

        /// <summary>
        /// Constructs a new configuration.
        /// </summary>
        public SiteConfig()
        {
            this.Server = new ServerConfig();
        }

        /// <summary>
        /// The directory where Baker will write files.
        /// </summary>
        [DefaultValue("_site")]
        public string Destination { get; set; } 

        /// <summary>
        /// Exclude directories and/or files from the conversion. These exclusions 
        /// are relative to the site's source directory and cannot be outside the
        /// source directory.
        /// </summary>
        public List<string> Exclude { get; set; }

        /// <summary>
        /// The configuration of the server by default.
        /// </summary>
        public ServerConfig Server { get; set; }

    }


    /// <summary>
    /// Server configuration section.
    /// </summary>
    public sealed class ServerConfig : YamlObject
    {
        /// <summary>
        /// Listen at the given hostname.
        /// </summary>
        [DefaultValue("localhost")]
        public string Host { get; set; }

        /// <summary>
        /// Listen on the given port.
        /// </summary>
        [DefaultValue(8080)]
        public int Port { get; set; }

        /// <summary>
        /// Serve the website from the given base URL
        /// </summary>
        [YamlAlias("baseurl")]
        public string BaseUrl { get; set; }
    }

}
