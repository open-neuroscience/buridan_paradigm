// =============================================================================
// ProtocolFiles.cs
//
// Reading and writing protocol files (<folder>/<name>.json) on the PC.
//
// Relative folder names are resolved against the current directory, which
// Bonsai sets to the folder of the running workflow. The default "protocols"
// therefore means bonsai/protocols next to BuridanArena.bonsai.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides access to protocol files stored on the PC.
    /// </summary>
    public static class ProtocolFiles
    {
        const string FileExtension = ".json";

        /// <summary>
        /// Returns the absolute path of a protocol folder.
        /// </summary>
        public static string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                throw new ArgumentException("The protocol folder is not set.");
            }
            return Path.GetFullPath(folder);
        }

        /// <summary>
        /// Returns the full path of the file for a protocol name.
        /// </summary>
        public static string GetFilePath(string folder, string name)
        {
            if (!ProtocolDefinition.IsValidName(name))
            {
                throw new ArgumentException("Invalid protocol name '" + name + "'.");
            }
            string absoluteFolder = ResolveFolder(folder);
            return Path.Combine(absoluteFolder, name + FileExtension);
        }

        /// <summary>
        /// Returns the names of all protocol files in a folder, sorted
        /// alphabetically. Returns an empty array if the folder does not exist.
        /// </summary>
        public static string[] ListNames(string folder)
        {
            string absoluteFolder = ResolveFolder(folder);
            List<string> names = new List<string>();
            if (!Directory.Exists(absoluteFolder))
            {
                return names.ToArray();
            }

            string[] files = Directory.GetFiles(absoluteFolder, "*" + FileExtension);
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (ProtocolDefinition.IsValidName(name))
                {
                    names.Add(name);
                }
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        /// <summary>
        /// Loads a protocol by name from a folder.
        /// </summary>
        public static ProtocolDefinition Load(string folder, string name)
        {
            string path = GetFilePath(folder, name);
            string json = File.ReadAllText(path, Encoding.UTF8);
            ProtocolDefinition protocol = ProtocolJson.Deserialize(json);
            return protocol;
        }

        /// <summary>
        /// Validates and saves a protocol to &lt;folder&gt;/&lt;name&gt;.json, creating
        /// the folder if needed. Returns the path of the written file.
        /// </summary>
        public static string Save(string folder, ProtocolDefinition protocol)
        {
            protocol.Validate();

            string absoluteFolder = ResolveFolder(folder);
            Directory.CreateDirectory(absoluteFolder);

            string finalPath = GetFilePath(folder, protocol.Name);
            string temporaryPath = finalPath + ".tmp";
            string json = ProtocolJson.Serialize(protocol);

            // Write a temporary file first, then move it into place, so an
            // interrupted save never leaves a half-written protocol behind.
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(temporaryPath, finalPath);
            return finalPath;
        }
    }
}
