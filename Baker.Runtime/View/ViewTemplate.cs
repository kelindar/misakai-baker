using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Baker.View
{
    /// <summary>
    /// Represents an asset template.
    /// </summary>
    public class ViewTemplate : IViewTemplate
    {
        /// <summary>
        /// Constructs a new view template.
        /// </summary>
        /// <param name="name">The name of the template.</param>
        public ViewTemplate(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the template
        /// </summary>
        public string Name
        {
            get;
            private set;
        }
    }

}
