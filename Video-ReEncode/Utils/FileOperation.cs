
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReEncode.Utils
{
    public class FileOperation
    {
        public static void SaveFile(string filename, string contents)
        {
            FileInfo fileInfo = new FileInfo(filename);
            if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();
            
            using (StreamWriter sw = File.CreateText(filename))
            {
                sw.Write(contents);
            }
        }

        public static string LoadFile(string filename)
        {
            string fileContents = "";
            if (File.Exists(filename))
            {
                using (StreamReader sr = File.OpenText(filename))
                {
                    fileContents = sr.ReadToEnd();
                }
            }

            return fileContents;
        }

        /** Moving a file within the same directory is considered a rename.
         */
        public static bool MoveFile(string oldPath, string newPath) {
            try
            {
                File.Move(oldPath, newPath);
            }
            catch {
                return false;
            }

            return true;
        }

        public static bool DeleteFile(string path) {
            try
            {
                File.Delete(path);
                
            }
            catch {
                return false;
            }

            return true;
        }

        public static List<string> GetNestedFiles(string directoryPath, List<string> fileExtensionsFilter = null) {
            var result = new List<string>();

            var files = Directory.EnumerateFiles(directoryPath);
            foreach (var file in files)
            {
                if(fileExtensionsFilter == null || fileExtensionsFilter.IndexOf(Path.GetExtension(file).ToLower()) != -1) 
                    result.Add(file);
            }

            if (Directory.Exists(directoryPath)) {
                var dirs = Directory.EnumerateDirectories(directoryPath);
                foreach (var dir in dirs)
                {
                    result.AddRange(GetNestedFiles(dir, fileExtensionsFilter));
                }
            }

            return result;
        }

        public static List<string> GetRelativeNestedFileList(string directoryPath, List<string> fileExtensionsFilter = null) {
            return GetNestedFiles(directoryPath, fileExtensionsFilter).Select(file => GetRelativePath(directoryPath, file)).ToList();
        }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fromPath"/> or <paramref name="toPath"/> is <c>null</c>.</exception>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                throw new ArgumentNullException("fromPath");
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException("toPath");
            }

            Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
            Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!Path.HasExtension(path) &&
                !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }

    }
    
}
