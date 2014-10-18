using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        // Fields
        private readonly SiteProject Project;
        private readonly ConcurrentDictionary<string, string> Cache =
            new ConcurrentDictionary<string, string>();

        // Constants
        private const string LayoutPattern = @"@\{Layout=""([_a-zA-Z\/\-\.\%0-9].*)"";\}";
        private const string IncludePattern = @"@Include\(""([_a-zA-Z\/\-\.\%0-9].*)""\)";
        private const string InputPattern = @"@Input\(""([_a-zA-Z\/\-\.\%0-9]*)""\)";
        private readonly string[] DefaultUsings = new string[]{
            "System",
            "Baker",
            "Baker.Text"
        };

        /// <summary>
        /// Constructs a view engine.
        /// </summary>
        /// <param name="project">The project that the view engine belongs to.</param>
        public RazorViewEngine(SiteProject project)
        {
            this.Project = project;
        }


        /// <summary>
        /// Gets or adds a view from the cache.
        /// </summary>
        /// <param name="input">The input asset.</param>
        /// <param name="valueFactory">A function that produces the value that should be added to the cache in case it does not already exist.</param>
        /// <returns></returns>
        public IViewTemplate Update(IAssetFile input)
        {
            // Prepare the cache key
            var cacheName = input
                .RelativeName
                .Replace(input.Extension, String.Empty)
                .Replace(@"\", @"/");

            // Attach usings too
            var content = this.GetUsings() + Environment.NewLine + 
                input.Content.AsString();

            Tracing.Info("Template", cacheName);

            // Update the map
            Cache.AddOrUpdate(cacheName,
                content, (k, v) => content
                );

            return new ViewTemplate(cacheName);
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
            var headers = input.Meta;
            var content =
                this.GetUsings() + Environment.NewLine +
                "@{Layout=\"" + layout + "\";}" + Environment.NewLine +
                input.Content.AsString();

            // Using a new scope, avoiding state sharing problems that way
            using (var service = new TemplateService())
            {

                // Process the content
                this.ProcessContent(content, service, headers);

                // Parse without caching
                return AssetContent.FromString(
                    service.Parse(content, headers, null, null)
                    );
            }
        }

        /// <summary>
        /// Gets the complete usings list.
        /// </summary>
        /// <returns>The complete usings list.</returns>
        private string GetUsings()
        {
            // Create usings
            var usings = DefaultUsings
                .Union(Project.Configuration.Usings)
                .Select(a => "@using " + a)
                .Aggregate((a, b) => a + Environment.NewLine + b);
            return usings;
        }

        private void ProcessContent(string content, TemplateService service, dynamic model)
        {
            // recursively process the Layout
            foreach (Match match in Regex.Matches(content, LayoutPattern, RegexOptions.IgnoreCase))
                ProcessSubContent(match, service, model);

            // recursively process the @Includes
            foreach (Match match in Regex.Matches(content, IncludePattern, RegexOptions.IgnoreCase))
                ProcessSubContent(match, service, model);
        }


        private void ProcessSubContent(Match match, TemplateService service, dynamic model)
        {
            var subName = match.Groups[1].Value; 
            string subContent;
            if (this.Cache.TryGetValue(subName, out subContent))
            {
                // Process inputs
                subContent = this.ProcessInputs(subContent);

                // Recursively process it and add to the service
                ProcessContent(subContent, service, model);

                // Compile the template
                service.Compile(subContent, typeof(AssetHeader), subName);
            }
        }

        private string ProcessInputs(string content)
        {
            // recursively process the @Inputs
            foreach (Match match in Regex.Matches(content, InputPattern, RegexOptions.IgnoreCase))
            {
                // Get from the cache
                var subName = match.Groups[1].Value; 
                var subContent = String.Empty;
                this.Cache.TryGetValue(subName, out subContent);

                // Replace anyway
                content = content.Replace(match.ToString(), subContent);
            }

            return content;

        }

    }



}
