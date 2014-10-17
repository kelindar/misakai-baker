using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Dynamic;

namespace Baker
{
    /// <summary>
    /// Represents an asset header.
    /// </summary>
    public class AssetHeader : DynamicYaml
    {
        /// <summary>
        /// The delimiters to check.
        /// </summary>
        private readonly static Regex HeaderDelimiter = new Regex("(?<=---)(.*)(?=---)", RegexOptions.Compiled);

        /// <summary>
        /// Constructs a header from the YAML string.
        /// </summary>
        /// <param name="yaml">The yaml string to deserialize from.</param>
        private AssetHeader(string yaml)
            : base(yaml)
        {

        }

        /// <summary>
        /// Parses the header from a string.
        /// </summary>
        /// <param name="content">The content to parse</param>
        /// <returns>The asset header</returns>
        public static AssetHeader FromString(string content, out int endOffset)
        {
            endOffset = 0;
            if (!content.StartsWith("---"))
                return null;

            // Find the end and create
            endOffset = content.IndexOf("---", 3) + 3 ;
            return new AssetHeader(
                content.Substring(3, endOffset - 6).Trim()
                );

        }

    }
}
