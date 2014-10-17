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
    public class AssetInputFile : AssetFile
    {
        private byte[]   CachedContent = null;
        private DateTime CachedTime = DateTime.MinValue;

        /// <summary>
        /// Constructs a new file wrapper around a file info.
        /// </summary>
        /// <param name="file">The file info to wrap.</param>
        /// <param name="root">The virtual directory.</param>
        public AssetInputFile(FileInfo file, DirectoryInfo root) : base(file, root)
        {
        }


        /// <summary>
        /// Gets the content of the file, or null if no content was found.
        /// </summary>
        public override AssetContent Content
        {
            get
            {
                // If there's no such file
                if (!this.Exists)
                    return new AssetContent(Encoding.UTF8, null);

                // Attempt to invalidate 
                this.Refresh();

                // Return the cached
                return new AssetContent(Encoding.UTF8, this.CachedContent);
            }
        }

        /// <summary>
        /// Refreshes the content if necessary.
        /// </summary>
        private void Refresh()
        {
            try
            {
                // Gets the content
                if (File.GetLastWriteTimeUtc(this.Info.FullName) > this.CachedTime)
                {
                    // Content changed
                    this.CachedContent = File.ReadAllBytes(this.FullName);
                    this.CachedTime = File.GetLastWriteTimeUtc(this.FullName);
                }
            }
            catch { }

        }

    }


}
