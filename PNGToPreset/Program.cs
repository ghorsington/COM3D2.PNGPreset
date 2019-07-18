using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using COM3D2.PNGPreset.Managed;

namespace PNGToPreset
{
    internal class Program
    {
        private static readonly byte[] IEND_MAGIC = {73, 69, 78, 68};
        private static readonly byte[] EXT_DATA_BEGIN_MAGIC = Encoding.ASCII.GetBytes("EXTPRESET_BGN");
        private static readonly byte[] EXT_DATA_END_MAGIC = Encoding.ASCII.GetBytes("EXTPRESET_END");

        private static void PrintHelp()
        {
            MessageBox.Show(@"PNG2Preset

Converts PNGPreset presets into COM3D2 presets.
Use it to convert presets when you remove PNGPreset plugin.

How to use:
Drag and drop PNG files you want to convert to normal presets.
The tool will generate .preset and extdata file (if present in PNG).");
            Environment.Exit(0);
        }

        private static bool BytesEqual(byte[] r, byte[] l)
        {
            if (r.Length != l.Length)
                return false;

            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < r.Length; i++)
                if (r[i] != l[i])
                    return false;

            return true;
        }

        private static long FindExtData(Stream fs)
        {
            var buf = new byte[EXT_DATA_BEGIN_MAGIC.Length];
            fs.Seek(-buf.Length, SeekOrigin.End);

            fs.Read(buf, 0, buf.Length);

            if (!BytesEqual(buf, EXT_DATA_END_MAGIC)) return fs.Length;

            var pos = fs.Position - EXT_DATA_BEGIN_MAGIC.Length;

            while (true)
            {
                if (pos < 0) // Just make sure so we don't f up in case the user tampered with the file
                    return fs.Length;
                fs.Position = pos;
                fs.Read(buf, 0, buf.Length);

                if (BytesEqual(buf, EXT_DATA_BEGIN_MAGIC))
                    break;
                pos--;
            }

            return fs.Position;
        }

        private static void Copy(Stream from, Stream to, long offset, long length)
        {
            from.Position = offset;

            var buf = new byte[4096];

            while (length > 0)
            {
                var read = from.Read(buf, 0, (int) Math.Min(length, buf.Length));

                if (read == 0)
                    return;

                length -= read;

                to.Write(buf, 0, read);
            }
        }

        private static void ConvertToPreset(string fileName)
        {
            using (var stream = File.OpenRead(fileName))
            {
                using (var br = new BinaryReader(stream))
                {
                    var buf = new byte[IEND_MAGIC.Length];

                    var pos = 0;
                    for (;; stream.Position = ++pos)
                    {
                        var len = stream.Read(buf, 0, buf.Length);

                        if (len != IEND_MAGIC.Length)
                        {
                            Console.WriteLine($"WARN: {fileName} is not a valid PNG file.");
                            return;
                        }

                        if (BytesEqual(buf, IEND_MAGIC))
                            break;
                    }

                    // Skip CRC
                    stream.Position += 4;

                    var presetStartPos = stream.Position;

                    if (br.ReadString() != "CM3D2_PRESET")
                    {
                        Console.WriteLine("PNG file does not contain a preset!");
                        return;
                    }

                    var extDataPos = FindExtData(stream);

                    var dir = Path.GetDirectoryName(Path.GetFullPath(fileName));
                    var name = Path.GetFileNameWithoutExtension(fileName);

                    using (var presetStream = File.Create(Path.Combine(dir, $"{name}.preset")))
                    {
                        Copy(stream, presetStream, presetStartPos,
                            extDataPos - (extDataPos < stream.Length ? EXT_DATA_BEGIN_MAGIC.Length : 0) -
                            presetStartPos);
                    }

                    if (stream.Length - extDataPos <= 0) return;

                    stream.Position = extDataPos;
                    using (var ext = LZMA.Decompress(stream, EXT_DATA_END_MAGIC.Length))
                    using (var extOut = File.Create(Path.Combine(dir, $"{name}.preset.expreset.xml")))
                    {
                        Copy(ext, extOut, 0, ext.Length);
                    }
                }
            }
        }

        private static void Main(string[] args)
        {
            if (args.Length == 0)
                PrintHelp();


            foreach (var file in args)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"WARN: {file} is not a file. Skipping...");
                    continue;
                }

                if (Path.GetExtension(file).ToLowerInvariant() != ".png")
                {
                    Console.WriteLine($"WARN: {file} is not a PNG file. Skipping...");
                    continue;
                }

                try
                {
                    ConvertToPreset(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Failed to convert {file} because {e}");
                }
            }
        }
    }
}