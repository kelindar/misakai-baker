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
    public class HeaderProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly HeaderProcessor Default = new HeaderProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Get the content
                var content = input.Content.AsString();

                // Attempt to extract headers, if none simply return the input
                int endOffset;
                var header = AssetHeader.FromString(content, out endOffset);
                if (header == null)
                    return input;

                //dynamic head = header;
                //Console.WriteLine("layout: " + (string)head.layout);
                //Console.WriteLine("title: " + (string)head.title);

                // Return processed output
                return AssetOutputFile.Create(
                    from: input,
                    content: content.Remove(0, endOffset).TrimStart(),
                    meta: header
                    );
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Header", ex);
                return null;
            }
        }

    }
}
