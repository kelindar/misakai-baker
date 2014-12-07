using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Baker.Text;
using System.Diagnostics;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a typescript compiler.
    /// </summary>
    public class TypeScriptProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly TypeScriptProcessor Default = new TypeScriptProcessor();


        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Only processes .ts files
                if (input.Extension != ".ts")
                    return input;

                // Measure
                var watch = new Stopwatch();
                watch.Start();

                // compiles a TS file
                var output = TypeScriptCompiler.Compile(input.Content.AsString());

                // We've compiled
                watch.Stop();

                // Compiled successfully
                Tracing.Info("TypeScript", "Compiled " + input.RelativeName + ", " + watch.ElapsedMilliseconds + " ms.");

                // Return processed output
                return AssetOutputFile.Create(
                    from: input,
                    content: output,
                    extension: "js"
                    );
            }
            catch(Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("TypeScript", ex);
                return null;
            }
        }

    }

    internal class BakerConsole
    {
        public BakerConsole() { }

        public void Print(string iString)
        {
            Tracing.Info("TypeScript", iString);
        }

        public void log(string iString)
        {
            Tracing.Info("TypeScript", iString);
        }
    }
}
