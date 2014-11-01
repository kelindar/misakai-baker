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
    /// Represents a LESS processor.
    /// </summary>
    public class LessProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly LessProcessor Default = new LessProcessor();

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Select the files we should compile
                var compile = input.Project.Configuration.Less;
                if(compile == null || compile.Count == 0 || !compile.Any(c => input.RelativeName.EndsWith(c)))
                {
                    // Return processed output
                    return AssetOutputFile.Create(
                        from: input,
                        content: String.Empty,
                        extension: "css"
                        );
                }

                // Get the content
                var content = input.Content.AsString();

                // Get the LESS engine
                var engine = new LessEngine();
                var importer = (Importer)engine.Parser.Importer;
                var freader  = (FileReader)importer.FileReader;
                freader.PathResolver = new LessPathResolver(input.FullName);

                // Transform to CSS
                var output = engine.TransformToCss(content, input.FullName);

                // Return processed output
                return AssetOutputFile.Create(
                    from: input,
                    content: output,
                    extension: "css"
                    );
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Less", ex);
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
