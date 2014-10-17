using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Baker.Processors;
using Baker.View;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Baker
{
    /// <summary>
    /// Represents a website project.
    /// </summary>
    public sealed partial class SiteProject
    {
        /// <summary>
        /// Builds the project.
        /// </summary>
        /// <param name="path"></param>
        public static void Bake(string path)
        {
            // Load the project and fetch the files
            using (var project = SiteProject.FromDisk(path))
            {
                // Since we bake, clean up the directory
                Tracing.Info("Bake", "Cleaning the output directory...");
                var output = new DirectoryInfo(
                    Path.Combine(project.Directory.FullName, project.Configuration.Destination)
                    );
                if (output.Exists)
                    output.Delete(true);

                // Fetch the files on disk
                Tracing.Info("Bake", "Fetching files...");
                var files = project
                    .Provider
                    .Fetch(project);

                // Load all templates
                Tracing.Info("Bake", "Building the website...");
                RazorProcessor.Default
                    .On(files.Only("*.cshtml"))
                    .Export();

                // Handle markup stuff
                HeaderProcessor.Default
                    .Next(MarkdownProcessor.Default)
                    .Next(LayoutProcessor.Default)
                    .Next(HtmlMinifier.Default)
                    .On(files.Only("*.md"))
                    .Export();

                // Minify CSS
                CssMinifier.Default
                    .On(files.Only("*.css"))
                    .Export();

                // Minify JS
                JavaScriptMinifier.Default
                    .On(files.Only("*.js"))
                    .Export();

                // Optimize PNG
                PngOptimizer.Default
                    .On(files.Only("*.png"))
                    .Export();
            }
        }

    }



}
