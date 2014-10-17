using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using Baker.View;
using RazorEngine;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a Razor engine processor.
    /// </summary>
    public class RazorProcessor : ProcessorBase<IAssetFile, IViewTemplate>
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
        public override IViewTemplate Process(IAssetFile input)
        {
            try
            {
                // Register the template in the viewengine
                return input.Project.ViewEngine.RegisterTemplate(input);
            }
            catch (Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Razor", ex);
                return null;
            }


        }

    }
}
