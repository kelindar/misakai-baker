using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Baker.Providers;
using Baker.Text;
using Baker.View;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Baker
{
    /// <summary>
    /// Represents a website project.
    /// </summary>
    public sealed partial class SiteProject : IDisposable
    {
        /// <summary>
        /// Gets the directory of this project.
        /// </summary>
        public DirectoryInfo Directory
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the site configuration.
        /// </summary>
        public SiteConfig Configuration
        {
            get; 
            private set;
        }

        /// <summary>
        /// Gets the asset provider for this project.
        /// </summary>
        public IAssetProvider Provider
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the view engine used by the project.
        /// </summary>
        public IViewEngine ViewEngine
        {
            get;
            private set;
        }

        #region Static Members
        /// <summary>
        /// Creates a site project from disk.
        /// </summary>
        /// <param name="path">The path to the configuration file.</param>
        /// <returns>The project.</returns>
        public static SiteProject FromDisk(string path)
        {
            return FromDisk(new DirectoryInfo(path));
        }

        /// <summary>
        /// Creates a site project from disk.
        /// </summary>
        /// <param name="path">The path to the configuration file.</param>
        /// <returns>The project.</returns>
        public static SiteProject FromDisk(DirectoryInfo path)
        {
            if (!path.Exists)
                throw new FileNotFoundException("Unable to load the project, as the directory specified does not exist. Directory: " + path.FullName);

            // Prepare a project
            var project = new SiteProject();

            // Set the path where the project lives
            project.Directory = path;

            try
            {
                // Get the configuration file
                project.Configuration = YamlObject.FromSearch<SiteConfig>(path, SiteConfig.Name, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                // Unable to read
                Tracing.Error("Config", "Configuration file " + SiteConfig.Name + " is invalid.");
            }

            if (project.Configuration == null)
            {
                Tracing.Info("Project", "Configuration file not found, creating a new one.");
                project.Configuration = new SiteConfig();
                project.Configuration.ToFile(Path.Combine(path.FullName, SiteConfig.Name));
            }

            // Assign a provider
            project.Provider = new DiskAssetProvider(path);
            project.ViewEngine = new RazorViewEngine();



            // We have a project!
            return project;
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// Cleans up the resources.
        /// </summary>
        /// <param name="disposing">Whether we are disposing or finalizing.</param>
        public void Dispose(bool disposing)
        {

        }

        /// <summary>
        /// Cleans up the resources.
        /// </summary>
        ~SiteProject()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Cleans up the resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        #endregion
    }



}
