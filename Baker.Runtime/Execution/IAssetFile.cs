using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker
{
    /// <summary>
    /// Represents a contract for an application file.
    /// </summary>
    public interface IAssetFile
    {
        /// <summary>
        /// Gets an instance of the parent directory.
        /// </summary>
        DirectoryInfo Directory { get; }

        /// <summary>
        /// Gets the path of the virtual directory.
        /// </summary>
        DirectoryInfo VirtualDirectory { get; }

        /// <summary>
        /// Gets a value indicating whether a file exists.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the string representing the extension part of the file.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets the relative name of the file.
        /// </summary>
        string RelativeName { get; }

        /// <summary>
        /// Gets the content of the file.
        /// </summary>
        AssetContent Content { get; }
        
    }

    /// <summary>
    /// Represents a contract for an output file.
    /// </summary>
    public interface IAssetOutputFile : IAssetFile
    {
        /// <summary>
        /// Actually writes the file on disk, at the specified location.
        /// </summary>
        void Write();
    }

    /// <summary>
    /// Represents an asset content body.
    /// </summary>
    [Serializable]
    public struct AssetContent
    {
        #region Constructors
        /// <summary>
        /// The actual bytes of the content.
        /// </summary>
        private readonly byte[] Bytes;

        /// <summary>
        /// The encoding type
        /// </summary>
        private readonly Encoding Encoding;


        /// <summary>
        /// Constructs a new <see cref="AssetContent"/>.
        /// </summary>
        /// <param name="encoding">The encoding type</param>
        /// <param name="bytes">Raw bytes of the body</param>
        public AssetContent(Encoding encoding, byte[] bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException("Encoding should be specified.");

            this.Encoding = encoding;
            this.Bytes = bytes;
        }
        #endregion

        #region Public Members

        /// <summary>
        /// Gets the raw bytes representation of the request body.
        /// </summary>
        /// <returns>Raw bytes representation of the request body.</returns>
        public byte[] AsBytes()
        {
            return this.Bytes;
        }

        /// <summary>
        /// Gets a readable <see cref="ByteStream"/> representation of the request body.
        /// </summary>
        /// <returns>A readable <see cref="ByteStream"/> representation of the request body.</returns>
        public Stream AsStream()
        {
            var stream = new MemoryStream();
            if(this.Bytes != null)
                stream.Write(this.Bytes, 0, this.Bytes.Length);
            return stream;
        }

        /// <summary>
        /// Gets the string representation of the request body.
        /// </summary>
        /// <returns>String representation of the request body.</returns>
        public string AsString()
        {
            if (this.Bytes == null)
                return String.Empty;
            return this.Encoding.GetString(this.Bytes);
        }
        #endregion

        #region Static Members
        /// <summary>
        /// Creates a content from a string.
        /// </summary>
        /// <param name="original">The original string.</param>
        /// <returns>The content</returns>
        public static AssetContent FromString(string original)
        {
            return new AssetContent(
                Encoding.UTF8,
                Encoding.UTF8.GetBytes(original)
                );
        }

        /// <summary>
        /// Creates a content from a byte array.
        /// </summary>
        /// <param name="original">The original bytes.</param>
        /// <returns>The content</returns>
        public static AssetContent FromBytes(byte[] original)
        {
            return new AssetContent(
                Encoding.UTF8,
                original
                );
        }

        #endregion

    }

}
