using System;
using System.IO;

namespace GTIP
{
    internal static class EditorPortraitFiles
    {
        internal static byte[] Read(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 24 || info.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Portrait must be a PNG smaller than 10 MB.");
            var bytes = File.ReadAllBytes(path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) throw new InvalidOperationException("Invalid PNG signature.");
            if (bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82) throw new InvalidOperationException("Missing PNG image header.");
            Func<int, long> number = offset => ((long)bytes[offset] << 24) | ((long)bytes[offset + 1] << 16) | ((long)bytes[offset + 2] << 8) | bytes[offset + 3];
            long width = number(16), height = number(20);
            if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new InvalidOperationException("Portrait dimensions must be between 1 and 4096 pixels.");
            return bytes;
        }
    }
}
