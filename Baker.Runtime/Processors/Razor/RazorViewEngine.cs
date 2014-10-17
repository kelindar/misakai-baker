using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RazorEngine;
using RazorEngine.Templating;

namespace Baker.View
{
    /// <summary>
    /// Default view engine implementation.
    /// </summary>
    public class RazorViewEngine: IViewEngine
    {
        /// <summary>
        /// The service for templates.
        /// </summary>
        private readonly TemplateService Service 
            = new TemplateService();


        /// <summary>
        /// Gets or adds a view from the cache.
        /// </summary>
        /// <param name="input">The input asset.</param>
        /// <param name="valueFactory">A function that produces the value that should be added to the cache in case it does not already exist.</param>
        /// <returns></returns>
        public IViewTemplate RegisterTemplate(IAssetFile input)
        {
            // Prepare the cache key
            var cacheName = input
                .Name
                .Replace(input.Extension, String.Empty);
            
            // Compile the input and use Name as the cache key
            this.Service.Compile(
                input.Content.AsString(), 
                typeof(AssetHeader), 
                cacheName);

            return new RazorTemplate(cacheName, this.Service);
        }

        /// <summary>
        /// Gets a template for a particular layout.
        /// </summary>
        /// <param name="layout">The input layout name.</param>
        /// <returns>The template</returns>
        public IViewTemplate GetTemplate(string layout)
        {
            return new RazorTemplate(layout, this.Service);
        }

        /// <summary>
        /// Renders a single page using the input asset and a layout.
        /// </summary>
        /// <param name="input">The input file to render.</param>
        /// <param name="layout">The layout to use for the render.</param>
        /// <returns>The content generated.</returns>
        public AssetContent RenderPage(IAssetFile input, string layout)
        {
            // Create the template with the specified layout
            var template = "@{Layout=\"" + layout + "\";}" +
                input.Content.AsString();

            // Parse without caching
            return AssetContent.FromString(
                this.Service.Parse(template, input.Meta, null, null)
                );
        }

    }



    /// <summary>
    /// Represents an asset template.
    /// </summary>
    public class RazorTemplate : ViewTemplate
    {
        private readonly string Name;
        private readonly TemplateService Service;

        /// <summary>
        /// Constructs a template.
        /// </summary>
        public RazorTemplate(string cacheName, TemplateService service)
        {
            this.Name = cacheName;
            this.Service = service;
        }

        /// <summary>
        /// Executes the template and returns a content.
        /// </summary>
        /// <param name="model">The model to execute the template on.</param>
        /// <returns>The output content that have been generated.</returns>
        public override AssetContent Execute(AssetHeader model)
        {
            // Run the template and return the content
            return AssetContent.FromString(
                this.Service.Run(this.Name, model, null)
                );
        }

    }
}
