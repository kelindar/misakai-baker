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
        /// <param name="meta">The metadata associated with the output file.</param>
        /// <param name="root">The virtual directory.</param>
        /// <param name="content">The content of the output file.</param>
        public AssetOutputFile(FileInfo file, DirectoryInfo root, AssetHeader meta, byte[] content)
            : base(file, root)
        {
            this.CachedContent = content;
            this.Meta = meta;
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

        public static AssetOutputFile Create(IAssetFile from, string content, AssetHeader meta = null, string extension = null)
        {
            return Create(from, Encoding.UTF8.GetBytes(content), meta, extension);
        }

        public static AssetOutputFile Create(IAssetFile from, MemoryStream content, AssetHeader meta = null, string extension = null)
        {
            return Create(from, content.ToArray(), meta, extension);
        }

        public static AssetOutputFile Create(IAssetFile from, byte[] content, AssetHeader meta = null, string extension = null)
        {
            var name = extension == null
                ? new FileInfo(from.FullName)
                : new FileInfo(Path.ChangeExtension(from.FullName, extension));

            var head = meta == null
                ? from.Meta
                : meta;

            return new AssetOutputFile(
                name,
                from.VirtualDirectory,
                head,
                content
                );
        }
        #endregion

    }


}
