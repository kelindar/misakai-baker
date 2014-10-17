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
        /// <param name="files">The files that should be processed.</param>
        public void Process(IEnumerable<IAssetFile> files)
        {
            // Load all templates
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

            // Optimize & copy everything
            FileOptimizer.Default
                .On(files.Except(DefaultExclude))
                .Export();

        }

        /// <summary>
        /// The main pipeline for project processing.
        /// </summary>
        /// <param name="files">The files that should be processed.</param>
        public void Update()
        {
            // Rescan files
            var files = this.Provider.Fetch(this);

            // Load all templates
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

            // Copy everything
            FileCopier.Default
                .On(files.Except(DefaultExclude))
                .Export();

            // Last update changed
            this.LastUpdate = DateTime.Now.Ticks;
        }


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
                Tracing.Info("Bake", "Building the website...");
                var output = new DirectoryInfo(
                    Path.Combine(project.Directory.FullName, project.Configuration.Destination)
                    );
                if (output.Exists)
                    output.Delete(true);

                // Fetch the files on disk
                var files = project
                    .Provider
                    .Fetch(project);

                // Load all templates
                project.Process(files);
            }
        }

        /// <summary>
        /// Spins a in-process webserver.
        /// </summary>
        /// <param name="path"></param>
        public static void Taste(string path)
        {
            // Load the project and fetch the files
            using (var project = SiteProject.FromDisk(path))
            {
                // Rebuid everything first
                SiteProject.Bake(path);

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
