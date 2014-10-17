using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using Baker.Text;
using nQuant;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a css minifier.
    /// </summary>
    public class PngOptimizer : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly PngOptimizer Default = new PngOptimizer();

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
                var quantizer = new WuQuantizer();
                using (var bitmap = new Bitmap(Bitmap.FromStream(input.Content.AsStream())))
                {
                    using (var quantized = quantizer.QuantizeImage(bitmap, 10, 70))
                    using( var output = new MemoryStream())
                    {
                        // Save to the output stream
                        quantized.Save(output, ImageFormat.Png);

                        // Minified successfully
                        Tracing.Info("PNG", "Optimized " + input.RelativeName);

                        // Return processed output
                        return AssetOutputFile.Create(input, output);
                    }
                }
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("PNG", ex);
                return null;
            }
        }

    }
}
