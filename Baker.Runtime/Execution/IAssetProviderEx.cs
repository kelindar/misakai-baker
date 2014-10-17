using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Baker
{

    public static class AssetProviderEx
    {

        /// <summary>
        /// Filters the file collection with a wildcard pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="pattern">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Only(this IEnumerable<IAssetFile> input, string pattern)
        {
            // Wildcard to regex conversion
            var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")+ "$";

            // Execute the form with regex
            return Only(input, new Regex(regex, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Filters the file collection with a regex pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="pattern">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Only(this IEnumerable<IAssetFile> input, Regex regex)
        {
            return input.Where(asset => regex.IsMatch(asset.RelativeName));
        }

        /// <summary>
        /// Filters the file collection with a wildcard pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="pattern">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Except(this IEnumerable<IAssetFile> input, string pattern)
        {
            // Wildcard to regex conversion
            var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

            // Execute the form with regex
            return Except(input, new Regex(regex, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Filters the file collection with a regex pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="pattern">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Except(this IEnumerable<IAssetFile> input, Regex regex)
        {
            return input.Where(asset => !regex.IsMatch(asset.RelativeName));
        }

        /// <summary>
        /// Writes the files to the file system.
        /// </summary>
        /// <param name="input">The collection of files to write.</param>
        public static void Write(this IEnumerable<IAssetFile> input)
        {
            // We actually need to enumerate now
            foreach (var file in input)
            {
                // If we have an output file, write it
                var output = file as IAssetOutputFile;
                if (output == null)
                    return;

                // Writes adjusted
                output.Write();
            }
        }

        /// <summary>
        /// Writes the files to the file system.
        /// </summary>
        /// <param name="input">The collection of files to write.</param>
        public static void Write(this IEnumerable<IAssetTemplate> input)
        {
            // We actually need to enumerate now
            foreach (var file in input)
            {
 
            }
        }

    }
}
