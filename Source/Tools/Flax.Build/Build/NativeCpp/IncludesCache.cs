// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Flax.Build.NativeCpp
{
    /// <summary>
    /// The C++ header files includes cache for C++ source files.
    /// </summary>
    public static class IncludesCache
    {
        private static Dictionary<string, string[]> DirectIncludesCache = new Dictionary<string, string[]>();
        private static Dictionary<string, string[]> AllIncludesCache = new Dictionary<string, string[]>();
        private static Dictionary<string, DateTime> DirectIncludesTimestampCache = new Dictionary<string, DateTime>();
        private static Dictionary<string, DateTime> AllIncludesTimestampCache = new Dictionary<string, DateTime>();
        private static Dictionary<string, bool> FileExistsCache = new Dictionary<string, bool>();
        private static Dictionary<string, DateTime> FileTimestampCache = new Dictionary<string, DateTime>();
        private static readonly string IncludeToken = "include";
        private static string CachePath;

        public static void LoadCache()
        {
            CachePath = Path.Combine(Globals.Root, Configuration.IntermediateFolder, "IncludesCache.cache");
            if (!File.Exists(CachePath))
                return;

            using (var stream = new FileStream(CachePath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != 1)
                    return;

                // DirectIncludesCache
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        string[] values = new string[reader.ReadInt32()];
                        for (int j = 0; j < values.Length; j++)
                            values[j] = reader.ReadString();

                        DirectIncludesCache.Add(key, values);
                    }
                }

                // AllIncludesCache
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        string[] values = new string[reader.ReadInt32()];
                        for (int j = 0; j < values.Length; j++)
                            values[j] = reader.ReadString();

                        AllIncludesCache.Add(key, values);
                    }
                }

                // DirectIncludesTimestampCache
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        DateTime value = new DateTime(reader.ReadInt64());
                        DirectIncludesTimestampCache.Add(key, value);
                    }
                }

                // AllIncludesTimestampCache
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        DateTime value = new DateTime(reader.ReadInt64());
                        AllIncludesTimestampCache.Add(key, value);
                    }
                }
            }
        }

        public static void SaveCache()
        {
            using (var stream = new FileStream(CachePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // Version
                writer.Write(1);

                // DirectIncludesCache
                {
                    writer.Write(DirectIncludesCache.Count);
                    foreach (KeyValuePair<string, string[]> kvp in DirectIncludesCache)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value.Length);
                        foreach (var value in kvp.Value)
                            writer.Write(value);
                    }
                }

                // AllIncludesCache
                {
                    writer.Write(AllIncludesCache.Count);
                    foreach (KeyValuePair<string, string[]> kvp in AllIncludesCache)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value.Length);
                        foreach (var value in kvp.Value)
                            writer.Write(value);
                    }
                }

                // DirectIncludesTimestampCache
                {
                    writer.Write(DirectIncludesTimestampCache.Count);
                    foreach (KeyValuePair<string, DateTime> kvp in DirectIncludesTimestampCache)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value.Ticks);
                    }
                }

                // AllIncludesTimestampCache
                {
                    writer.Write(AllIncludesTimestampCache.Count);
                    foreach (KeyValuePair<string, DateTime> kvp in AllIncludesTimestampCache)
                    {
                        writer.Write(kvp.Key);
                        writer.Write(kvp.Value.Ticks);
                    }
                }
            }
        }

        private static bool FileExists(string path)
        {
            if (FileExistsCache.TryGetValue(path, out bool result))
                return result;

            result = File.Exists(path);
            FileExistsCache.Add(path, result);
            return result;
        }

        private static DateTime FileLastWriteTime(string path)
        {
            if (FileTimestampCache.TryGetValue(path, out DateTime result))
                return result;

            result = File.GetLastWriteTime(path);
            FileTimestampCache.Add(path, result);
            return result;
        }

        /// <summary>
        /// Finds all included files by the source file (including dependencies).
        /// </summary>
        /// <param name="sourceFile">The source file path.</param>
        /// <returns>The list of included files by this file. Not null nut may be empty.</returns>
        public static string[] FindAllIncludedFiles(string sourceFile)
        {
            DateTime? lastModified = null;

            // Try hit the cache
            string[] result;
            if (AllIncludesCache.TryGetValue(sourceFile, out result))
            {
                if (AllIncludesTimestampCache.TryGetValue(sourceFile, out var cachedTimestamp))
                {
                    lastModified = FileLastWriteTime(sourceFile);
                    if (lastModified == cachedTimestamp)
                        return result;
                }

                AllIncludesCache.Remove(sourceFile);
                AllIncludesTimestampCache.Remove(sourceFile);
            }

            if (!FileExists(sourceFile))
                throw new Exception(string.Format("Cannot scan file \"{0}\" for includes because it does not exist.", sourceFile));

            //using (new ProfileEventScope("FindAllIncludedFiles"))
            {
                // Collect included files
                var includedFiles = new HashSet<string>();
                includedFiles.Add(sourceFile);
                FindAllIncludedFiles(sourceFile, includedFiles);

                // Process result
                includedFiles.Remove(sourceFile);
                result = includedFiles.ToArray();
                AllIncludesCache.Add(sourceFile, result);

                if (!AllIncludesTimestampCache.ContainsKey(sourceFile))
                    AllIncludesTimestampCache.Add(sourceFile, lastModified ?? File.GetLastWriteTime(sourceFile));

                /*Log.Info("File includes for " + sourceFile);
                foreach (var e in result)
                {
                    Log.Info("    " + e);
                }
                Log.Info("");*/

                return result;
            }
        }

        private static void FindAllIncludedFiles(string sourceFile, HashSet<string> includedFiles)
        {
            var directIncludes = GetDirectIncludes(sourceFile);

            // Scan files to get further dependencies
            for (int i = 0; i < directIncludes.Length; i++)
            {
                // Skip visited files
                if (!includedFiles.Add(directIncludes[i]))
                    continue;

                FindAllIncludedFiles(directIncludes[i], includedFiles);
            }
        }

        private static string[] GetDirectIncludes(string sourceFile)
        {
            DateTime? lastModified = null;

            // Try hit the cache
            string[] result;
            if (DirectIncludesCache.TryGetValue(sourceFile, out result))
            {
                if (DirectIncludesTimestampCache.TryGetValue(sourceFile, out var cachedTimestamp))
                {
                    lastModified = FileLastWriteTime(sourceFile);
                    if (lastModified == cachedTimestamp)
                        return result;
                }

                DirectIncludesCache.Remove(sourceFile);
                DirectIncludesTimestampCache.Remove(sourceFile);
            }

            // Find all files included directly
            var includedFiles = new HashSet<string>();
            var sourceFileFolder = Path.GetDirectoryName(sourceFile);
            var fileContents = File.ReadAllText(sourceFile, Encoding.UTF8);
            var length = fileContents.Length;
            for (int i = 0; i < length; i++)
            {
                // Skip single-line comments
                if (i < length - 1 && fileContents[i] == '/' && fileContents[i + 1] == '/')
                {
                    while (++i < length && fileContents[i] != '\n')
                    {
                    }

                    continue;
                }

                // Skip multi-line comments
                if (i > 0 && fileContents[i - 1] == '/' && fileContents[i] == '*')
                {
                    i++;

                    while (++i < length && !(fileContents[i - 1] == '*' && fileContents[i] == '/'))
                    {
                    }

                    i++;
                    continue;
                }

                // Read all until preprocessor instruction begin
                if (fileContents[i] != '#')
                    continue;

                // Skip spaces and tabs
                while (++i < length && (fileContents[i] == ' ' || fileContents[i] == '\t'))
                {
                }

                // Skip anything other than 'include' text
                if (i + IncludeToken.Length >= length)
                    break;
                var token = fileContents.Substring(i, IncludeToken.Length);
                if (token != IncludeToken)
                    continue;
                i += IncludeToken.Length;

                // Skip all before path start
                while (++i < length && fileContents[i] != '\n' && fileContents[i] != '"' && fileContents[i] != '<')
                {
                }

                // Skip all until path end
                var includeStart = i;
                while (++i < length && fileContents[i] != '\n' && fileContents[i] != '"' && fileContents[i] != '>')
                {
                }

                // Extract included file path
                var includedFile = fileContents.Substring(includeStart, i - includeStart);
                includedFile = includedFile.Trim();
                if (includedFile.Length == 0)
                    continue;
                bool isLibraryInclude = includedFile[0] == '<';
                includedFile = includedFile.Substring(1, includedFile.Length - 1);

                // Skip files from system libraries and SDKs
                if (isLibraryInclude)
                {
                    //Log.Error(sourceFile + " -> " + includedFile);
                    //continue;
                }

                // Relative to the workspace root
                var includedFilePath = Path.Combine(Globals.Root, "Source", includedFile);
                if (!FileExists(includedFilePath))
                {
                    // Relative to the source file
                    includedFilePath = Path.Combine(sourceFileFolder, includedFile);
                    if (!FileExists(includedFilePath))
                    {
                        // Relative to any of the included project workspaces
                        var project = Globals.Project;
                        var isValid = false;
                        foreach (var reference in project.References)
                        {
                            includedFilePath = Path.Combine(reference.Project.ProjectFolderPath, "Source", includedFile);
                            if (FileExists(includedFilePath))
                            {
                                isValid = true;
                                break;
                            }
                        }

                        if (!isValid)
                        {
                            // Invalid include
                            //Log.Error(string.Format("File '{0}' includes file '{1}' but it does not exist!", sourceFile, includedFile));
                            continue;
                        }
                    }
                }

                // Filter path
                includedFilePath = includedFilePath.Replace('\\', '/');
                includedFilePath = Utilities.RemovePathRelativeParts(includedFilePath);

                // Collect file
                includedFiles.Add(includedFilePath);
            }

            // Process result
            result = includedFiles.ToArray();
            DirectIncludesCache.Add(sourceFile, result);
            if (!DirectIncludesTimestampCache.ContainsKey(sourceFile))
                DirectIncludesTimestampCache.Add(sourceFile, lastModified ?? FileLastWriteTime(sourceFile));
            return result;
        }
    }
}
