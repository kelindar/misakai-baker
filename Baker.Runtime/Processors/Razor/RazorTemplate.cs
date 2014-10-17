using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RazorEngine;

namespace Baker
{

    /// <summary>
    /// Represents an asset template.
    /// </summary>
    public class RazorTemplate : AssetTemplate
    {
        /// <summary>
        /// Constructs a template.
        /// </summary>
        public RazorTemplate(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the template.
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Executes the template and returns a content.
        /// </summary>
        /// <returns>The output content that have been generated.</returns>
        public override AssetContent Execute()
        {
            return AssetContent.FromString(
                Razor.Run(this.Name)
                );
        }

    }
}
