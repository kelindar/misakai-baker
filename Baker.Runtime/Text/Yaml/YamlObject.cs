using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using System.ComponentModel;

namespace Baker.Text
{
    /// <summary>
    /// Represents a YAML-serialized object.
    /// </summary>
    public abstract class YamlObject
    {
        /// <summary>
        /// Constructs a default Yaml Object and assigns default values.
        /// </summary>
        public YamlObject()
        {
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(this))
            {
                // Set default value if DefaultValueAttribute is present
                var attr = prop.Attributes[typeof(DefaultValueAttribute)] as DefaultValueAttribute;
                if (attr != null)
                    prop.SetValue(this, attr.Value);
            }
        }

        #region Serialization Members
        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source string.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromString<T>(string content)
            where T : YamlObject
        {
            return FromStream<T>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source bytes.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromBytes<T>(byte[] content)
            where T : YamlObject
        {
            return FromStream<T>(new MemoryStream(content));
        }

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source stream.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromAsset<T>(IAssetFile content)
            where T : YamlObject
        {
            return FromStream<T>(content.Content.AsStream());
        }

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source stream.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromFile<T>(FileInfo file)
            where T : YamlObject
        {
            using (var stream = new FileStream(file.FullName, FileMode.Open))
                return FromStream<T>(stream);
        }

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source stream.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromFile<T>(string path)
            where T : YamlObject
        {
            using (var stream = new FileStream(path, FileMode.Open))
                return FromStream<T>(stream);
        }

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source stream.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromSearch<T>(DirectoryInfo directory, string searchPattern, SearchOption options)
            where T : YamlObject
        {
            if(!directory.Exists)
                return null;

            // Search
            var file = directory
                .GetFiles(searchPattern, options)
                .FirstOrDefault();
            if (file == null)
                return null;
   
            using (var stream = new FileStream(file.FullName, FileMode.Open))
                return FromStream<T>(stream);

        }

        

        /// <summary>
        /// Deserializes a <see cref="YamlObject"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="content">The source stream.</param>
        /// <returns>The deserialized object.</returns>
        public static T FromStream<T>(Stream content)
            where T : YamlObject
        {
            // Deserialize with standard options
            var deserializer = new YamlDotNet.Serialization.Deserializer(
                namingConvention: new CamelCaseNamingConvention()
                );
            using (var reader = new StreamReader(content, Encoding.UTF8))
                return deserializer.Deserialize<T>(reader);
        }


        /// <summary>
        /// Serializes this <see cref="YamlObject"/>.
        /// </summary>
        /// <returns>The serialized content.</returns>
        public byte[] ToBytes()
        {
            var serializer = new YamlDotNet.Serialization.Serializer(
                options: YamlDotNet.Serialization.SerializationOptions.EmitDefaults,
                namingConvention: new CamelCaseNamingConvention()
                );
            using (var memory = new MemoryStream())
            using (var writer = new StreamWriter(memory))
            {
                // Serialize to the stream
                serializer.Serialize(writer, this);
                writer.Flush();

                // Return the buffer
                return memory.ToArray();
            }
        }

        /// <summary>
        /// Serializes the object to a file.
        /// </summary>
        /// <param name="file">The file path to serialize into.</param>
        public void ToFile(string file)
        {
            File.WriteAllBytes(file, this.ToBytes());
        }
        #endregion

    }
}
