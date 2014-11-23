using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;
using dotless.Core;
using System.IO;
using dotless.Core.configuration;
using dotless.Core.Importers;
using dotless.Core.Input;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a processor for translations.
    /// </summary>
    public class LocaleProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly LocaleProcessor Default = new LocaleProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Skip default
                if (input.Name == "all.locale")
                    return null;

                // If we should build all languages, spin new project for 
                // each language in our configuration.
                var language = input.Project.Language;
                if(language == "all")
                {
                    // Get the target language
                    var target = input.Name.Replace(".locale", String.Empty);
                
                    // Bake a new project 
                    using (var project = SiteProject.FromDisk(input.Project.Directory, target))
                    {
                        // Set the destination to the sub-language destination
                        project.Configuration.Destination = Path.Combine(project.Configuration.Destination, target);

                        // Bake the translated site
                        project.Bake(BakeMode.Optimized);
                    }
                }

                return null;
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Locale", ex);
                return null;
            }

        }

        #region LessResolver
        /// <summary>
        /// Represents a path resolver for LESS @import statements.
        /// </summary>
        internal class LessPathResolver : IPathResolver
        {
            private readonly FileInfo FilePath;

            /// <summary>
            /// Constructs a new path resolver for less files.
            /// </summary>
            /// <param name="file">The parent file.</param>
            public LessPathResolver(string file)
            {
                this.FilePath = new FileInfo(file);
            }

            /// <summary>
            /// Gets the full path for an @import statement.
            /// </summary>
            /// <param name="path">The path specified in the import statement.</param>
            /// <returns>The resolved path.</returns>
            public string GetFullPath(string path)
            {
                return Path.GetFullPath(
                    Path.Combine(FilePath.Directory.FullName, path)
                    );
            }
        }
        #endregion

    }

}
