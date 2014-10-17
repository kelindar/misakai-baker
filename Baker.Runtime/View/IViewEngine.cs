using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baker.View
{

    /// <summary>
    /// Defines the functionality of a view engine.
    /// </summary>
    public interface IViewEngine
    {
        /// <summary>
        /// Registers a template.
        /// </summary>
        /// <param name="input">The input asset.</param>
        /// <param name="valueFactory">A function that produces the value that should be added to the cache in case it does not already exist.</param>
        /// <returns></returns>
        IViewTemplate RegisterTemplate(IAssetFile input);

        /// <summary>
        /// Gets a template for a particular layout.
        /// </summary>
        /// <param name="layout">The input layout name.</param>
        /// <returns>The template</returns>
        IViewTemplate GetTemplate(string layout);

        /// <summary>
        /// Renders a single page using the input asset and a layout.
        /// </summary>
        /// <param name="input">The input file to render.</param>
        /// <param name="layout">The layout to use for the render.</param>
        /// <returns>The content generated.</returns>
        AssetContent RenderPage(IAssetFile input, string layout);
    }
    
}
