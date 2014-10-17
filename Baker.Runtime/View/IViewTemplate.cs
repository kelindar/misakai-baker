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
        /// Gets the name of the template
        /// </summary>
        string Name
        {
            get;
        }
    }


}
