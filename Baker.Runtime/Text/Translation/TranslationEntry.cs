using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baker.Text
{
    /// <summary>
    /// Represents a translation entry.
    /// </summary>
    public class TranslationEntry
    {
        /// <summary>
        /// Construcs a translation entry
        /// </summary>
        public TranslationEntry(string key, string language, string value)
        {
            this.Key = key;
            this.Language = language;
            this.Value = value;
        }

        /// <summary>
        /// Gets or sets the key of the translation.
        /// </summary>
        public readonly string Key;

        /// <summary>
        /// Gets or sets the languageof the translation.
        /// </summary>
        public readonly string Language;

        /// <summary>
        /// Gets or sets the value of the translation.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Converts the entry to a locale string.
        /// </summary>
        /// <returns></returns>
        public string ToLocale()
        {
            return String.Format("{0}:\t{1}", this.Key, this.Language);
        }

        /// <summary>
        /// Gets the hash code for the entry. It's only based on key and language
        /// so that we only have one entry for a key/language pair.
        /// </summary>
        /// <returns>The value of the hash code.</returns>
        public override int GetHashCode()
        {
            return this.Key.GetHashCode() ^ this.Language.GetHashCode();
        }

        /// <summary>
        /// Compares to another entry. It's only based on key and language
        /// so that we only have one entry for a key/language pair.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as TranslationEntry;
            if (other == null)
                return false;

            
            return other.Key == this.Key && other.Language == this.Language;
        }
    }
}
