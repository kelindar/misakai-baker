using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Baker
{
    /// <summary>
    /// Represents a contract for site data providers.
    /// </summary>
    public interface IAssetProvider 
    {
        /// <summary>
        /// Fetches the assets from the data source.
        /// </summary>
        /// <param name="project">The project to fetch the data for.</param>
        /// <returns>The enumerable set of assets.</returns>
        IEnumerable<IAssetFile> Fetch(SiteProject project);

    }

    public abstract class AssetProviderBase : IAssetProvider
    {
        /// <summary>
        /// Fetches the assets from the data source.
        /// </summary>
        /// <param name="project">The project to fetch the data for.</param>
        /// <returns>The enumerable set of assets.</returns>
        public abstract IEnumerable<IAssetFile> Fetch(SiteProject project);
    }

}
