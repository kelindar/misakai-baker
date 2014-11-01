using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;
using dotless.Core;
using System.IO;

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
            var directory = Directory.GetCurrentDirectory();
            try
            {
                // Get the content
                var content = input.Content.AsString();

                // Set the directory path for our less processor, so we can
                // use relative paths in the .less files 
                Directory.SetCurrentDirectory(input.Directory.FullName);

                // Return processed output
                return AssetOutputFile.Create(
                    from: input,
                    content: Less.Parse(content),
                    extension: "css"
                    );
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Less", ex);
                return null;
            }
            finally
            {
                // Set the directory to whatever it was previously
                Directory.SetCurrentDirectory(directory);
            }
        }

    }
}
