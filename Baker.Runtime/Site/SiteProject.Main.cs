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
        // Default extensions to exclude from being copied to the content.
        private readonly string[] DefaultExclude = new string[] {
            "*.tmp", "*.cshtml", "*.md", "*_config.yaml", "*~*"
        };

        /// <summary>
        /// The main pipeline for project processing.
        /// </summary>
        public void Bake(BakeMode mode)
        {
            try
            {
                Tracing.Info("Bake", String.Format("Building the website ({0}) ...", this.Language));

				// If we're baking in Optimized mode, make sure the destination and all subdirectories and files are removed first.
                if (mode == BakeMode.Optimized)
                {
                    DirectoryInfo output = new DirectoryInfo(
                        Path.Combine(this.Directory.FullName, this.Configuration.Destination)
                    );

					if (output.Exists)
					{
						output.Delete(true);
					}
                }

                // Fetch all the files from the source directory.
                IEnumerable<IAssetFile> files = this.Assets.Fetch(this);

				// Exclude folders and files listed in the _config.yaml excludes array.
				// Note: These excluded folders and files are only excluded from processing not from being copied.
				string[] excludes = new String[] { };

				List<string> exclude = this.Configuration.Exclude;
				if (exclude != null && exclude.Count > 0)
				{
					excludes = exclude.ToArray();
				}

				IEnumerable<IAssetFile> filesExceptExcludes = files.Except(excludes);

                // Load all templates
                TranslationProcessor.Default
                    .Next(RazorProcessor.Default)
					.On(filesExceptExcludes.Only("*.cshtml"))
                    .Export();

                // Handle markup processing
                TranslationProcessor.Default
                    .Next(HeaderProcessor.Default)
                    .Next(MarkdownProcessor.Default)
                    .Next(LayoutProcessor.Default)
                    .Next(HtmlMinifier.Default)
					.On(filesExceptExcludes.Only("*.md"))
                    .Export();

				// If we're NOT baking in Fast mode, optimize the files.
				if (mode != BakeMode.Fast)
				{
					StyleProcessor.Default
						.Next(FileOptimizer.Default)
						.On(filesExceptExcludes.Except(DefaultExclude))
						.Export();
				}

				// Copy files
				TranslationProcessor.Default
					.Next((IProcessor<IAssetFile, IAssetFile>)FileCopier.Default)
					.On(files.Except(DefaultExclude))
					.Export();
            }
            catch (Exception ex)
            {
                // Catch all errors that occured during bake.
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
            if (config.Languages == null || config.Languages.Count == 0)
            {
                // Make sure we have a default language
                config.Languages = new List<string>();
                config.Languages.Add("default");
            }

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
