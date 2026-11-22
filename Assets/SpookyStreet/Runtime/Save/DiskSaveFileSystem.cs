using System;
using System.IO;
using System.Text;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Save
{
    /// <summary>
    /// <see cref="ISaveFileSystem"/> over System.IO for device saves. Pass
    /// <c>Application.persistentDataPath</c> (or a subfolder) as the store directory; all files of a slot stay in one
    /// directory, so replace and rename are same-volume operations. Not thread-safe: serialize writes.
    /// </summary>
    public sealed class DiskSaveFileSystem : ISaveFileSystem
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public bool Exists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path, Utf8);

        public void WriteAllTextDurable(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] bytes = Utf8.GetBytes(contents ?? string.Empty);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void Replace(string source, string destination, string backup)
        {
            try
            {
                File.Replace(source, destination, backup, true);
            }
            catch (PlatformNotSupportedException)
            {
                // Rename-based fallback: the loader recovers from the temp file if interrupted between renames.
                if (backup != null)
                {
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }

                    File.Move(destination, backup);
                }
                else
                {
                    File.Delete(destination);
                }

                File.Move(source, destination);
            }
        }

        public void Move(string source, string destination) => File.Move(source, destination);

        public void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
