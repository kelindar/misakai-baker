using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a stylesheet processor.
    /// </summary>
    public class StyleProcessor: ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly StyleProcessor Default = new StyleProcessor();

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
                    // Parse LESS stylesheets
                    case ".less": return LessProcessor.Default.Process(input);

                    // Any other file, do nothing
                    default: return input;
                }
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Style", ex);
                return null;
            }
        }

    }
}
