using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Baker.Processors;
using Baker.View;
using Spike;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Baker
{
    /// <summary>
    /// Represents a website project.
    /// </summary>
    public sealed partial class SiteProject
    {
        // Default extensions to exclude from being copied to the content
        private readonly string[] DefaultExclude = new string[]{
            "*.tmp", "*.cshtml", "*.md", "*_config.yaml", "*~*"
        };

        /// <summary>
        /// The main pipeline for project processing.
        /// </summary>
        public void Bake(BakeMode mode)
        {
            try
            {
                // Since we bake, clean up the directory
                Tracing.Info("Bake", "Building the website (" + this.Language + ") ...");
                if (mode == BakeMode.Optimized)
                {
                    // If we're baking, make sure everything is removed first
                    var output = new DirectoryInfo(
                        Path.Combine(this.Directory.FullName, this.Configuration.Destination)
                        );
                    if (output.Exists)
                        output.Delete(true);
                }

                // Fetch the files on disk
                var files = this.Assets.Fetch(this);

                // Load all templates
                TranslationProcessor.Default
                    .Next(RazorProcessor.Default)
                    .On(files.Only("*.cshtml"))
                    .Export();

                // Handle markup stuff
                TranslationProcessor.Default
                    .Next(HeaderProcessor.Default)
                    .Next(MarkdownProcessor.Default)
                    .Next(LayoutProcessor.Default)
                    .Next(HtmlMinifier.Default)
                    .On(files.Only("*.md"))
                    .Export();

                // Optimize & copy everything
                StyleProcessor.Default
                    .Next(mode == BakeMode.Fast
                        ? (IProcessor<IAssetFile, IAssetFile>)FileCopier.Default
                        : FileOptimizer.Default)
                    .On(files.Except(DefaultExclude))
                    .Export();

            }
            catch (Exception ex)
            {
                // Catch all errors during bake
                Tracing.Error("Bake", ex);
            }
        }

        /// <summary>
        /// The main pipeline for project processing.
        /// </summary>
        /// <param name="files">The files that should be processed.</param>
        public void Update()
        {
            // Bake in fast mode
            this.Bake(BakeMode.Fast);

            // Last update changed
            this.LastUpdate = DateTime.Now.Ticks;
        }


        /// <summary>
        /// Builds the project.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        public static void Bake(DirectoryInfo path, BakeMode mode)
        {
            // Read the configuration file at destination
            var config = SiteConfig.Read(path);
            if (config.Languages.Count == 0)
                config.Languages.Add("default");

            Tracing.Info("Bake", "Baking: " + path.FullName);

            foreach(var language in config.Languages)
            {
                // Load the project and fetch the files
                using (var project = SiteProject.FromDisk(path, language))
                {
                    // Bake the project
                    project.Bake(mode);
                }
            }

        }

        /// <summary>
        /// Spins a in-process webserver.
        /// </summary>
        /// <param name="path"></param>
        public static void Serve(DirectoryInfo path)
        {
            // Load the project and fetch the files
            using (var project = SiteProject.FromDisk(path))
            {
                // Rebuid everything first
                SiteProject.Bake(path, BakeMode.Fast);

                // Register the handler
                Service.Http.Register(new SiteHandler(project));

                // Spin Spike Engine on this thread
                Service.Listen(
                    new TcpBinding(IPAddress.Any, project.Configuration.Port)
                    );
            }
        }

    }



}
