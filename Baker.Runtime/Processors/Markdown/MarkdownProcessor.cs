using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker
{
    /// <summary>
    /// Represents a Razor engine processor.
    /// </summary>
    public class MarkdownProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly MarkdownProcessor Default = new MarkdownProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                var engine  = new Markdown();
                var content = engine.Transform(input.Content.AsString());

                // Compiled successfully
                Tracing.Info("Markdown", "Compiled " + input.RelativeName);

                // Return the output
                return AssetOutputFile.Create(
                    from: input,
                    content: content,
                    extension: "html"
                    );
            }
            catch (Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Markdown", ex);
                return null;
            }
        }

    }
}
