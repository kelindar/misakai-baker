using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker
{
    /// <summary>
    /// Represents an application-related file.
    /// </summary>
    public abstract class AssetFile : IAssetFile
    {
        /// <summary>
        /// The associated file info.
        /// </summary>
        protected readonly FileInfo Info;

        /// <summary>
        /// The associated project.
        /// </summary>
        protected readonly SiteProject Owner;        

        /// <summary>
        /// Constructs a new file wrapper around a file info.
        /// </summary>
        /// <param name="project">The project to which this file belongs.</param>
        /// <param name="file">The file info to wrap.</param>
        public AssetFile(SiteProject project, FileInfo file)
        {
            this.Info = file;
            this.Owner = project;
        }

        /// <summary>
        /// Gets to which project this file belongs.
        /// </summary>
        public SiteProject Project 
        {
            get { return this.Owner; }
        }

        /// <summary>
        /// Gets an instance of the parent directory.
        /// </summary>
        public DirectoryInfo Directory 
        {
            get { return this.Info.Directory; }
        }

        /// <summary>
        /// Gets the path of the virtual directory.
        /// </summary>
        public DirectoryInfo VirtualDirectory
        {
            get { return this.Owner.Directory; }
        }

        /// <summary>
        /// Gets a value indicating whether a file exists.
        /// </summary>
        public bool Exists
        {
            get { return this.Info.Exists; }
        }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string Name
        {
            get { return this.Info.Name; }
        }

        /// <summary>
        /// Gets the string representing the extension part of the file.
        /// </summary>
        public string Extension
        {
            get { return this.Info.Extension; }
        }

        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        public string FullName
        {
            get { return this.Info.FullName; }
        }

        /// <summary>
        /// Gets the relative name of the file.
        /// </summary>
        public string RelativeName
        {
            get 
            {
                return this.Info.FullName.Replace(
                    this.VirtualDirectory.FullName, String.Empty
                    ); 
            }
        }

        /// <summary>
        /// Gets the header associated with this file;
        /// </summary>
        public AssetHeader Meta
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the content of the file, or null if no content was found.
        /// </summary>
        public abstract AssetContent Content
        {
            get;
        }



    }


}
