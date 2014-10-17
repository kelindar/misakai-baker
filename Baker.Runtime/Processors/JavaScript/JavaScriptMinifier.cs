using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;
using RazorEngine;

namespace Baker
{
    /// <summary>
    /// Represents a javascript minifier.
    /// </summary>
    public class JavaScriptMinifier : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly JavaScriptMinifier Default = new JavaScriptMinifier();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Minify javascript
                var minifier = new Minifier();
                var content = minifier.MinifyJavaScript(input.Content.AsString());

                // Compiled successfully
                Tracing.Info("JS", "Minified " + input.RelativeName);

                // Return processed output
                return AssetOutputFile.Create(input, content);
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("JS", ex);
                return null;
            }
        }

    }
}
