using System;
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
        private readonly HashSet<TranslationEntry> Items =
            new HashSet<TranslationEntry>();
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

        #region Query Members
        /// <summary>
        /// Attempts to get a translation for a particular language.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <returns>The translation entry found, otherwise null.</returns>
        public TranslationEntry Get(string key)
        {
            // Find by key & language
            return this.Items
                .Where(t => t.Key == key && t.Language == this.Project.Language)
                .FirstOrDefault();
        }


        /// <summary>
        /// Attempts to get a translation for a particular language. Adds it to 
        /// the set if not found.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="language">The language of the translation entry.</param>
        /// <param name="defaultValue">The default value to add.</param>
        /// <returns>Translation entry found or added.</returns>
        public TranslationEntry GetOrAdd(string key, string defaultValue = null)
        {
            // Attempt to fetch the entry first
            var entry = this.Get(key);
            if(entry == null)
            {
                // Create and add a new entry to the set
                entry = new TranslationEntry(key, this.Project.Language, defaultValue);
                this.Items.Add(entry);
            }

            return entry;
        }
        #endregion

        #region Add/Update Members
        /// <summary>
        /// Attempts to add a value to the set. Only adds if no value exists already.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="language">The language of the translation entry.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>Whether the entry was added successfully or not.</returns>
        public bool TryAdd(string key, string language, string value)
        {
            // Attempt to fetch the entry first
            var entry = this.Items
                .Where(t => t.Key == key && t.Language == language)
                .FirstOrDefault();
            if (entry != null)
                return false;
            
            // Create and add a new entry to the set
            entry = new TranslationEntry(key, language, value);
            this.Items.Add(entry);
            return true;
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
                        File.WriteAllText(localeFile, String.Empty);

                    // We have a file, read it
                    foreach (var line in File.ReadAllLines(localeFile))
                    {
                        // Parse the line
                        var splitIndex = line.IndexOf(':');
                        var key = line.Substring(0, splitIndex);
                        var value = line.Substring(splitIndex).TrimStart();

                        // Try to add it to the set
                        this.TryAdd(key, language, value);
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
