using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a css minifier.
    /// </summary>
    public class FileCopier : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly FileCopier Default = new FileCopier();

        /// <summary>
        /// Parallel file copier
        /// </summary>
        public FileCopier() : base(32){}

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Simply copy the file
                return AssetOutputFile.Create(input, input.Content.AsBytes());
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("File", ex);
                return null;
            }
        }

    }
}
