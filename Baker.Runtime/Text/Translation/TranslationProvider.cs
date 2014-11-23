using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baker.Text
{
    /// <summary>
    /// Represents a set of translation entries
    /// </summary>
    public class TranslationProvider : ITranslationProvider
    {
        #region Constructors
        private readonly ConcurrentDictionary<string, TranslationEntry> Map =
            new ConcurrentDictionary<string, TranslationEntry>();
        private readonly SiteProject Project;
        private readonly DirectoryInfo LocalePath;

        /// <summary>
        /// Constructs a translation provider
        /// </summary>
        public TranslationProvider(SiteProject project)
        {
            // Set the project
            this.Project = project;

            // Make sure we have locale directory
            var localePath = Path.Combine(this.Project.Directory.FullName, "_locale");
            if (!Directory.Exists(localePath))
                Directory.CreateDirectory(localePath);
            this.LocalePath = new DirectoryInfo(localePath);

            // Load the locale files
            this.LoadOrCreate();
        }

        /// <summary>
        /// Gets the key used for storage internally.
        /// </summary>
        private string GetKey(string key)
        {
            return key + ":" + this.Project.Language;
        }
        #endregion

        #region Public Members
        /// <summary>
        /// Attempts to get a translation for a particular language.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <returns>The translation entry found, otherwise null.</returns>
        public string Get(string key)
        {
            // Find by key & language
            TranslationEntry entry;
            if(this.Map.TryGetValue(this.GetKey(key), out entry))
                return entry.Value;
            return null;
        }


        /// <summary>
        /// Attempts to get a translation for a particular language. Adds it to 
        /// the set if not found.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="language">The language of the translation entry.</param>
        /// <param name="defaultValue">The default value to add.</param>
        /// <returns>Translation entry found or added.</returns>
        public string GetOrAdd(string key, string defaultValue = null)
        {
            // Get or add internally
            var entry = this.Map.GetOrAdd(
                this.GetKey(key), 
                (k) => new TranslationEntry(key, this.Project.Language, defaultValue)
                );

            // Return the value
            return entry.Value;
        }

        /// <summary>
        /// Attempts to add a value to the set. Only adds if no value exists already.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="language">The language of the translation entry.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>Whether the entry was added successfully or not.</returns>
        public bool TryAdd(string key, string language, string value)
        {
            // Try add internally
            return this.Map.TryAdd(key, new TranslationEntry(key, language, value));
        }
        #endregion

        /// <summary>
        /// Loads or creates all locale files in the directory.
        /// </summary>
        private void LoadOrCreate()
        {
            foreach(var language in this.Project.Configuration.Languages)
            {
                try
                {
                    // If we don't have the file, create it
                    var localeFile = Path.Combine(this.LocalePath.FullName, language + ".locale");
                    if (!File.Exists(localeFile))
                        File.WriteAllText(localeFile, String.Empty, Encoding.UTF8);

                    // We have a file, read it
                    foreach (var line in File.ReadAllLines(localeFile))
                    {
                        if (String.IsNullOrWhiteSpace(line))
                            continue; 
                        try
                        {
                            // Parse the line
                            var splitIndex = line.IndexOf(':');
                            var key = line.Substring(0, splitIndex).Trim() + ":" + language;
                            var value = line.Substring(splitIndex + 1).TrimStart();

                            // Try to add it to the set
                            this.TryAdd(key, language, value);
                        }
                        catch (Exception ex)
                        {
                            Tracing.Error("Locale", ex + " Line: " + line);
                        }

                    }
                }
                catch(Exception ex)
                {
                    Tracing.Error("Locale", ex);
                }


            }
        }


    }
}
