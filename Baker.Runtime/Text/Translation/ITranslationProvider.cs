using System;
using System.Collections.Generic;

namespace Baker.Text
{
    public interface ITranslationProvider
    {
        /// <summary>
        /// Attempts to get a translation for a particular language.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <returns>The translation entry found, otherwise null.</returns>
        string Get(string key);

        /// <summary>
        /// Attempts to get a translation for a particular language. Adds it to 
        /// the set if not found.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="defaultValue">The default value to add.</param>
        /// <returns>Translation entry found or added.</returns>
        string GetOrAdd(string key, string defaultValue = null);

                /// <summary>
        /// Attempts to add a value to the set. Only adds if no value exists already.
        /// </summary>
        /// <param name="key">The key of the translation entry.</param>
        /// <param name="language">The language of the translation entry.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>Whether the entry was added successfully or not.</returns>
        bool TryAdd(string key, string language, string value);
    }
}
