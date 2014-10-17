using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Baker.View;

namespace Baker
{

    public static class AssetProviderEx
    {

        /// <summary>
        /// Filters the file collection with a wildcard pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="patterns">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Only(this IEnumerable<IAssetFile> input, params string[] patterns)
        {
            // Wildcard to regex conversion
            var regex = patterns
                .Select(p => "^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".")+ "$")
                .Select(p => new Regex(p, RegexOptions.IgnoreCase))
                .ToArray();

            // Execute the form with regex
            return Only(input, regex);
        }

        /// <summary>
        /// Filters the file collection with a regex pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="patterns">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Only(this IEnumerable<IAssetFile> input, params Regex[] patterns)
        {
            return input.Where(asset => 
                patterns.Any(regex => regex.IsMatch(asset.RelativeName))
                );
        }

        /// <summary>
        /// Filters the file collection with a wildcard pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="patterns">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Except(this IEnumerable<IAssetFile> input, params string[] patterns)
        {
            // Wildcard to regex conversion
            var regex = patterns
                .Select(p => "^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$")
                .Select(p => new Regex(p, RegexOptions.IgnoreCase))
                .ToArray();

            // Execute the form with regex
            return Except(input, regex);
        }

        /// <summary>
        /// Filters the file collection with a regex pattern.
        /// </summary>
        /// <param name="input">The input collection.</param>
        /// <param name="pattern">The pattern for filtering.</param>
        /// <returns>The filtered collection</returns>
        public static IEnumerable<IAssetFile> Except(this IEnumerable<IAssetFile> input, params Regex[] patterns)
        {
            return input.Where(asset =>
                patterns.Any(regex => regex.IsMatch(asset.RelativeName)) == false
                );
        }

        /// <summary>
        /// Writes the files to the file system.
        /// </summary>
        /// <param name="input">The collection of files to write.</param>
        public static void Export(this IEnumerable<IAssetFile> input)
        {
            // We actually need to enumerate now
            foreach (var file in input)
            {
                // If we have an output file, write it
                var output = file as IAssetOutputFile;
                if (output == null)
                    return;

                // Writes adjusted
                output.Export();
            }
        }

        /// <summary>
        /// Writes the files to the file system.
        /// </summary>
        /// <param name="input">The collection of files to write.</param>
        public static void Export(this IEnumerable<IViewTemplate> input)
        {
            // We actually need to enumerate now
            foreach (var file in input)
            {
 
            }
        }

    }
}
