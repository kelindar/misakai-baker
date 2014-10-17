using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker
{
    /// <summary>
    /// Represents an asset template.
    /// </summary>
    public abstract class AssetTemplate : IAssetTemplate
    {
        /// <summary>
        /// Constructs a template.
        /// </summary>
        public AssetTemplate()
        {
        }

        /// <summary>
        /// Executes the template and returns a content.
        /// </summary>
        /// <returns>The output content that have been generated.</returns>
        public abstract AssetContent Execute();

    }

}
