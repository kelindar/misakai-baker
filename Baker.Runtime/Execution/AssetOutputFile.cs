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
    public class AssetOutputFile : AssetFile, IAssetOutputFile
    {
        #region Constructors
        private byte[] CachedContent = null;

        /// <summary>
        /// Constructs a new file wrapper around a file info.
        /// </summary>
        /// <param name="file">The file info to wrap.</param>
        /// <param name="root">The virtual directory.</param>
        /// <param name="content">The content of the output file.</param>
        public AssetOutputFile(FileInfo file, DirectoryInfo root, byte[] content)
            : base(file, root)
        {
            this.CachedContent = content;
        }


        /// <summary>
        /// Gets the content of the file, or null if no content was found.
        /// </summary>
        public override AssetContent Content
        {
            get { return AssetContent.FromBytes(this.CachedContent); }
        }
        #endregion

        #region Public Members

        /// <summary>
        /// Actually writes the file on disk, at the specified location.
        /// </summary>
        public void Write()
        {
            try
            {
                // Everything from underscored folders stays there
                if (this.RelativeName.StartsWith("_"))
                    return;

                // Create the destination
                var destination = new FileInfo(
                    Path.Combine(this.VirtualDirectory.FullName, "_site",this.RelativeName)
                    );

                // Make sure we have the directory
                if (!destination.Directory.Exists)
                    destination.Directory.Create();

                //if (!destination.StartsWith("_site"))
                //    throw new InvalidOperationException("Unable to write the file, can only write to the '_site' directory.");

                

                // Write the file to the appropriate location
                File.WriteAllBytes(destination.FullName, this.CachedContent);
            }
            catch(Exception ex)
            {
                // Trace the error
                Tracing.Error("Output", ex);
            }
        }
        #endregion

        #region Static Members

        public static AssetOutputFile FromString(IAssetFile from, string content, string extension = null)
        {
            var name = extension == null
                ? new FileInfo(from.FullName)
                : new FileInfo(Path.ChangeExtension(from.FullName, extension));

            return new AssetOutputFile(
                name,
                from.VirtualDirectory,
                Encoding.UTF8.GetBytes(content)
                );
        }

        public static AssetOutputFile FromBytes(IAssetFile from, byte[] content, string extension = null)
        {
            var name = extension == null
                ? new FileInfo(from.FullName)
                : new FileInfo(Path.ChangeExtension(from.FullName, extension));

            return new AssetOutputFile(
                name,
                from.VirtualDirectory,
                content
                );
        }
        #endregion

    }


}
