using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a file optimizer.
    /// </summary>
    public class FileOptimizer : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly FileOptimizer Default = new FileOptimizer();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                switch(input.Extension)
                {
                    // Optimize PNG
                    case ".png": return PngOptimizer.Default.Process(input);

                    // Minify JavaScript
                    case ".js": return JavaScriptMinifier.Default.Process(input);

                    // Minify CSS
                    case ".css": return CssMinifier.Default.Process(input);

                    // Minify HTML
                    case ".htm":
                    case ".html":
                        return HtmlMinifier.Default.Process(input);

                    // Any other file, simply copy
                    default: return FileCopier.Default.Process(input);
                }
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("File", ex);
                return null;
            }
        }

    }
}
