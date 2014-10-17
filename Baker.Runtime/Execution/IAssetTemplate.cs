using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker
{
    /// <summary>
    /// Represents a contract for a template.
    /// </summary>
    public interface IAssetTemplate
    {
        /// <summary>
        /// Executes the template and returns a content.
        /// </summary>
        /// <returns>The output content that have been generated.</returns>
        AssetContent Execute();
    }


}
