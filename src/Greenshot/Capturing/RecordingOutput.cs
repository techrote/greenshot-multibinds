using System;
using System.IO;
using System.IO.Compression;

namespace Greenshot.Capturing
{
    public static class RecordingOutput
    {
        public static string UniquePath(string root, string name, string extension = "")
        {
            string path = Path.Combine(root, name + extension);
            for (int i = 2; File.Exists(path) || Directory.Exists(path); i++) path = Path.Combine(root, name + "_" + i + extension);
            return path;
        }

        public static string Finish(string working, string root, string name, bool folder, bool zip, int batch)
        {
            string archive = null;
            if (zip)
            {
                archive = UniquePath(root, name, ".zip");
                // Write to a temporary name; an interrupted archive is never mistaken for a completed ZIP.
                string pending = archive + ".partial";
                ZipFile.CreateFromDirectory(working, pending, CompressionLevel.NoCompression, false);
                File.Move(pending, archive);
            }
            if (folder)
            {
                string destination = UniquePath(root, name);
                Directory.Move(working, destination);
                return destination;
            }
            if (!zip)
            {
                foreach (string file in Directory.GetFiles(working))
                {
                    string basename = Path.GetFileName(file);
                    if (!basename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) basename = "batch" + batch.ToString("0000") + "_" + basename;
                    File.Move(file, UniquePath(root, Path.GetFileNameWithoutExtension(basename), Path.GetExtension(basename)));
                }
            }
            // This path is a unique staging directory created by this recording, with no user-supplied child paths.
            if (!Path.GetFileName(working).StartsWith(".recording-", StringComparison.Ordinal)) throw new IOException("Unexpected recording staging path.");
            if (zip) foreach (string file in Directory.GetFiles(working)) File.Delete(file);
            Directory.Delete(working, false);
            return zip ? archive : root;
        }
    }
}
