using System;
using System.Collections.Generic;
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
        /// The associated file info.
        /// </summary>
        protected readonly DirectoryInfo Root;

        /// <summary>
        /// Constructs a new file wrapper around a file info.
        /// </summary>
        /// <param name="file">The file info to wrap.</param>
        /// <param name="root">The root directory.</param>
        public AssetFile(FileInfo file, DirectoryInfo root)
        {
            this.Info = file;
            this.Root = root;
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
            get { return this.Root; }
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
                    this.Root.FullName, String.Empty
                    ); 
            }
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
