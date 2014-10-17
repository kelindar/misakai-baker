using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baker.Providers
{
    /// <summary>
    /// Fetches data from disk.
    /// </summary>
    public class DiskAssetProvider : AssetProviderBase
    {
        /// <summary>
        /// Constructs a new provider.
        /// </summary>
        /// <param name="directory">The root directory.</param>
        public DiskAssetProvider(DirectoryInfo directory)
        {
            this.Path = directory;
        }

        /// <summary>
        /// Gets or sets the path for this provider.
        /// </summary>
        public DirectoryInfo Path
        {
            get;
            set;
        }

        /// <summary>
        /// Fetches the assets from the data source.
        /// </summary>
        /// <returns>The enumerable set of assets.</returns>
        public override IEnumerable<IAssetFile> Fetch()
        {
            // Fetch and enumerate
            return Directory
                .EnumerateFiles(this.Path.FullName, "*", SearchOption.AllDirectories)
                .Select(f => new AssetInputFile(new FileInfo(f), this.Path))
                .ToList();
        }


    }
}
