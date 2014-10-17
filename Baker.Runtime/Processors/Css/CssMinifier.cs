using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;
using RazorEngine;

namespace Baker
{
    /// <summary>
    /// Represents a css minifier.
    /// </summary>
    public class CssMinifier : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly CssMinifier Default = new CssMinifier();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Minify css
                var minifier = new Minifier();
                var content = minifier.MinifyStyleSheet(input.Content.AsString());

                // Minified successfully
                Tracing.Info("CSS", "Minified " + input.RelativeName);

                // Return processed output
                return AssetOutputFile.FromString(input, content);
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("CSS", ex);
                return null;
            }
        }

    }
}
