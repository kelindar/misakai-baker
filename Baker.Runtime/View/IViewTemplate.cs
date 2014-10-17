using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker.View
{
    /// <summary>
    /// Represents a contract for a template.
    /// </summary>
    public interface IViewTemplate
    {
        /// <summary>
        /// Executes the template and returns a content.
        /// </summary>
        /// <param name="model">The model to execute the template on.</param>
        /// <returns>The output content that have been generated.</returns>
        AssetContent Execute(AssetHeader model);
    }


}
