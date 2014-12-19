using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Baker.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;

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
        /// Constructs a default config.
        /// </summary>
        public SiteConfig()
        {
            this.Usings = new List<string>();
            this.Usings.Add("System.IO");
            this.Usings.Add("System.IO.Compression");
            this.Usings.Add("System.Net");
            this.Usings.Add("System.Web");
            this.Usings.Add("System.Collections.Generic");
            this.Usings.Add("System.Collections.Specialized");
            this.Usings.Add("System.Linq");
            this.Usings.Add("System.Text");
            this.Usings.Add("System.Diagnostics");

            this.Less = new List<string>();
            this.Less.Add("style.less");

            //this.Languages = new List<string>();
            //this.Languages.Add("default");
        }

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
        /// The list of default usings to provide to the view engine
        /// </summary>
        public List<string> Usings { get; set; }

        /// <summary>
        /// The list of less files to compile.
        /// </summary>
        [YamlAlias("less")]
        public List<string> Less { get; set; }

        /// <summary>
        /// The list of languages that the site should be translated to.
        /// </summary>
        [YamlAlias("languages")]
        public List<string> Languages { get; set; }

        /// <summary>
        /// Listen on the given port.
        /// </summary>
        [DefaultValue(8080)]
        public int Port { get; set; }

        #region Loading Members
        /// <summary>
        /// Loads a site configuration from a particular path.
        /// </summary>
        /// <param name="path">The path to look for _config.yaml file.</param>
        /// <returns>The loaded or created configuration file.</returns>
        public static SiteConfig Read(DirectoryInfo path)
        {
            SiteConfig configuration = null;
            try
            {
                // Get the configuration file
                configuration = YamlObject.FromSearch<SiteConfig>(path, SiteConfig.Name, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                // Unable to read
                Tracing.Error("Config", "Configuration file " + SiteConfig.Name + " is invalid.");
            }

            if (configuration == null)
            {
                // Create a new configuration
                Tracing.Info("Project", "Configuration file not found, creating a new one.");
                configuration = new SiteConfig();
                configuration.ToFile(Path.Combine(path.FullName, SiteConfig.Name));
            }

            return configuration;

        }
        #endregion


    }



}
