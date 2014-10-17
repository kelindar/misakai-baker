using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a HTML minifier.
    /// </summary>
    public class HtmlMinifier : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly HtmlMinifier Default = new HtmlMinifier();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Minify HTML
                var minifier = new HtmlCompressor();
                var content = minifier.Minify(input.Content.AsString());

                // Minified successfully
                Tracing.Info("HTML", "Minified " + input.RelativeName);

                // Return processed output
                return AssetOutputFile.Create(input, content);
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("HTML", ex);
                return null;
            }
        }

    }
}
