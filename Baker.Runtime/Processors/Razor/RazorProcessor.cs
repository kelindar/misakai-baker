using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using RazorEngine;

namespace Baker
{
    /// <summary>
    /// Represents a Razor engine processor.
    /// </summary>
    public class RazorProcessor : ProcessorBase<IAssetFile, IAssetTemplate>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly RazorProcessor Default = new RazorProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetTemplate Process(IAssetFile input)
        {
            // Prepare the cache key
            var cacheName = input.RelativeName;
            try
            {
                // Compile the input and use Name as the cache key
                Razor.Compile(input.Content.AsString(), typeof(ExpandoObject), cacheName);

                // Compiled successfully
                Tracing.Info("Razor", "Compiled " + cacheName);

                // Return the template that can be used
                return new RazorTemplate(cacheName);
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Razor", ex);
                return null;
            }
        }

    }
}
