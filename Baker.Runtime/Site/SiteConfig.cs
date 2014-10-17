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
        /// The directory where Baker will write files.
        /// </summary>
        [DefaultValue("_site")]
        public string Destination { get; set; }

        /// <summary>
        /// The default index document for the web handler.
        /// </summary>
        [DefaultValue("index.html")]
        [YamlAlias("index-document")]
        public string IndexDocument { get; set; } 

        /// <summary>
        /// Exclude directories and/or files from the conversion. These exclusions 
        /// are relative to the site's source directory and cannot be outside the
        /// source directory.
        /// </summary>
        public List<string> Exclude { get; set; }

        /// <summary>
        /// Listen on the given port.
        /// </summary>
        [DefaultValue(8080)]
        public int Port { get; set; }



    }



}
