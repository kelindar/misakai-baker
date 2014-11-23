using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Baker.Text;
using System.Text.RegularExpressions;

namespace Baker.Processors
{
    /// <summary>
    /// Represents a translation pre-processor.
    /// </summary>
    public class TranslationProcessor : ProcessorBase<IAssetFile, IAssetFile>
    {
        /// <summary>
        /// Default processor.
        /// </summary>
        public static readonly TranslationProcessor Default = new TranslationProcessor();

        /// <summary>
        /// The pattern used for matching translations.
        /// </summary>
        private static readonly Regex Pattern = new Regex(@"\$([a-zA-Z0-9\-]+)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
            );

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public override IAssetFile Process(IAssetFile input)
        {
            try
            {
                // Get the original content
                var content = input.Content.AsString();

                // Get all translation matches
                var matches = Pattern.Matches(content);
                foreach(var match in matches)
                {
                    // Get the key for the entry
                    var key = match.ToString().Substring(1);

                    // Get the text for the translation
                    var text = input.Project.Translations.Get(key);
                    if(text != null)
                    {
                        // If we have found a translation, replace it in the content
                        content = content.Replace(match.ToString(), text);
                    }
                }

                // Return the output
                return AssetOutputFile.Create(
                    from: input,
                    content: content
                    );
            }
            catch (Exception ex)
            {
                // We didn't manage to create anything
                Tracing.Error("Locale", ex);
                return null;
            }
        }

    }
}
