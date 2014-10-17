using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a layout processor that applies a template on an asset.
    /// </summary>
    public class LayoutProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly LayoutProcessor Default = new LayoutProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Do we have some metadata?
                if (input.Meta == null)
                    return input;
                
                // Get the headers
                var headers = (dynamic)input.Meta;
                var layout = (string)headers.layout;
                if(String.IsNullOrWhiteSpace(layout))
                    return input;

                
                // Minified successfully
                Tracing.Info("Layout", "Render " + input.RelativeName);

                // Return processed output
                return AssetOutputFile.Create(input,
                    input.Project.ViewEngine.RenderPage(input, layout).AsBytes()
                    );
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Layout", ex);
                return null;
            }
        }

    }
}
