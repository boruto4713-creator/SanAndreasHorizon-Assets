using System.Buffers.Binary;
using System.Text;

namespace SanAndreasHorizonLauncher;

internal static class ImgV2Archive
{
    private const int SectorSize = 2048;
    private const int HeaderSize = 8;
    private const int EntrySize = 32;

    private sealed class Entry
    {
        public uint OffsetSectors { get; set; }
        public ushort SizeSectors { get; set; }
        public ushort Reserved { get; set; }
        public byte[] NameBytes { get; set; } = new byte[24];
        public string Name => Encoding.ASCII.GetString(NameBytes).TrimEnd('\0');
    }

    public static string BackupPathFor(string imgPath) =>
        imgPath + ".horizon-original.bak";

    public static async Task InstallBmxAsync(
        string imgPath,
        byte[] dff,
        byte[] txd,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(imgPath))
            throw new FileNotFoundException("No se encontró models/gta3.img.", imgPath);

        if (dff.Length == 0 || txd.Length == 0)
            throw new InvalidDataException("Los archivos BMX descargados están vacíos.");

        string tempPath = imgPath + ".horizon-new.tmp";
        string backupPath = BackupPathFor(imgPath);

        try
        {
            using var source = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entries = ReadDirectory(source);
            EnsureRequiredEntries(entries);

            // The first backup remains untouched on later updates.
            if (!File.Exists(backupPath))
                File.Copy(imgPath, backupPath, false);

            var replacements = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["bmx.dff"] = dff,
                ["bmx.txd"] = txd
            };

            long totalBytes = entries.Sum(e =>
                replacements.TryGetValue(e.Name, out var data)
                    ? (long)data.Length
                    : (long)e.SizeSectors * SectorSize);
            long copiedBytes = 0;

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            await using (var output = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                long dataStart = Align(HeaderSize + (long)entries.Count * EntrySize, SectorSize);
                output.SetLength(dataStart);
                output.Position = dataStart;

                byte[] copyBuffer = new byte[1024 * 1024];

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long aligned = Align(output.Position, SectorSize);
                    if (aligned != output.Position)
                    {
                        await output.WriteAsync(new byte[checked((int)(aligned - output.Position))], cancellationToken);
                    }

                    if (output.Position / SectorSize > uint.MaxValue)
                        throw new InvalidDataException("El archivo IMG supera el límite de desplazamiento del formato.");

                    entry.OffsetSectors = checked((uint)(output.Position / SectorSize));

                    if (replacements.TryGetValue(entry.Name, out var replacement))
                    {
                        long sectors = (replacement.LongLength + SectorSize - 1) / SectorSize;
                        if (sectors > ushort.MaxValue)
                            throw new InvalidDataException(entry.Name + " supera el tamaño máximo de una entrada IMG.");

                        entry.SizeSectors = checked((ushort)sectors);
                        await output.WriteAsync(replacement, cancellationToken);
                        copiedBytes += replacement.LongLength;
                    }
                    else
                    {
                        long length = (long)entry.SizeSectors * SectorSize;
                        long sourceOffset = (long)entry.OffsetSectors * SectorSize;
                        source.Position = sourceOffset;

                        long remaining = length;
                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int wanted = (int)Math.Min(copyBuffer.Length, remaining);
                            int read = await source.ReadAsync(copyBuffer.AsMemory(0, wanted), cancellationToken);
                            if (read <= 0)
                                throw new EndOfStreamException("El archivo gta3.img terminó antes de completar una entrada.");

                            await output.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
                            remaining -= read;
                            copiedBytes += read;
                            ReportProgress(progress, copiedBytes, totalBytes);
                        }
                    }

                    long paddedEnd = Align(output.Position, SectorSize);
                    if (paddedEnd != output.Position)
                    {
                        await output.WriteAsync(new byte[checked((int)(paddedEnd - output.Position))], cancellationToken);
                    }

                    ReportProgress(progress, copiedBytes, totalBytes);
                }

                byte[] directory = BuildDirectory(entries);
                output.Position = 0;
                await output.WriteAsync(directory, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }

            await VerifyBuiltArchiveAsync(tempPath, dff, txd, cancellationToken);
            File.Move(tempPath, imgPath, true);
            progress?.Report(100);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static void RestoreOriginal(string imgPath)
    {
        string backupPath = BackupPathFor(imgPath);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Todavía no existe una copia original de gta3.img.", backupPath);

        using (var backup = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _ = ReadDirectory(backup);
        }

        string restoreTemp = imgPath + ".horizon-restore.tmp";
        try
        {
            File.Copy(backupPath, restoreTemp, true);
            File.Move(restoreTemp, imgPath, true);
        }
        finally
        {
            if (File.Exists(restoreTemp))
                File.Delete(restoreTemp);
        }
    }

    private static List<Entry> ReadDirectory(Stream stream)
    {
        stream.Position = 0;
        byte[] header = new byte[HeaderSize];
        ReadExactly(stream, header);

        if (Encoding.ASCII.GetString(header, 0, 4) != "VER2")
            throw new InvalidDataException("gta3.img no tiene la cabecera VER2 de GTA San Andreas.");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        if (count == 0 || count > 1_000_000)
            throw new InvalidDataException("La tabla de archivos de gta3.img tiene un tamaño no válido.");

        long tableLength = (long)count * EntrySize;
        long tableEnd = HeaderSize + tableLength;
        if (tableEnd > stream.Length || tableLength > int.MaxValue)
            throw new InvalidDataException("La tabla de gta3.img está incompleta.");

        byte[] table = new byte[(int)tableLength];
        ReadExactly(stream, table);

        long minimumDataOffset = Align(tableEnd, SectorSize);
        var entries = new List<Entry>((int)count);

        for (int i = 0; i < count; i++)
        {
            int position = i * EntrySize;
            var entry = new Entry
            {
                OffsetSectors = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(position, 4)),
                SizeSectors = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(position + 4, 2)),
                Reserved = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(position + 6, 2)),
                NameBytes = table.AsSpan(position + 8, 24).ToArray()
            };

            if (string.IsNullOrWhiteSpace(entry.Name))
                throw new InvalidDataException("gta3.img contiene una entrada sin nombre.");

            long dataOffset = (long)entry.OffsetSectors * SectorSize;
            long dataLength = (long)entry.SizeSectors * SectorSize;
            if (dataLength > 0 && (dataOffset < minimumDataOffset || dataOffset > stream.Length - dataLength))
                throw new InvalidDataException("La entrada " + entry.Name + " apunta fuera del archivo gta3.img.");

            entries.Add(entry);
        }

        return entries;
    }

    private static void EnsureRequiredEntries(List<Entry> entries)
    {
        foreach (string required in new[] { "bmx.dff", "bmx.txd" })
        {
            int matches = entries.Count(e => string.Equals(e.Name, required, StringComparison.OrdinalIgnoreCase));
            if (matches != 1)
                throw new InvalidDataException("No se encontró una única entrada '" + required + "' en gta3.img. No se modificó el archivo.");
        }
    }

    private static byte[] BuildDirectory(List<Entry> entries)
    {
        byte[] result = new byte[checked(HeaderSize + entries.Count * EntrySize)];
        Encoding.ASCII.GetBytes("VER2").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), checked((uint)entries.Count));

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            int position = HeaderSize + i * EntrySize;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(position, 4), entry.OffsetSectors);
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(position + 4, 2), entry.SizeSectors);
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(position + 6, 2), entry.Reserved);
            entry.NameBytes.CopyTo(result, position + 8);
        }

        return result;
    }

    private static async Task VerifyBuiltArchiveAsync(
        string path,
        byte[] expectedDff,
        byte[] expectedTxd,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var entries = ReadDirectory(stream);

        await VerifyEntryAsync(stream, entries.Single(e => e.Name.Equals("bmx.dff", StringComparison.OrdinalIgnoreCase)),
            expectedDff, cancellationToken);
        await VerifyEntryAsync(stream, entries.Single(e => e.Name.Equals("bmx.txd", StringComparison.OrdinalIgnoreCase)),
            expectedTxd, cancellationToken);
    }

    private static async Task VerifyEntryAsync(
        Stream stream, Entry entry, byte[] expected, CancellationToken cancellationToken)
    {
        long storedLength = (long)entry.SizeSectors * SectorSize;
        if (storedLength < expected.LongLength)
            throw new InvalidDataException("La entrada " + entry.Name + " quedó truncada.");

        stream.Position = (long)entry.OffsetSectors * SectorSize;
        byte[] actual = new byte[expected.Length];
        await ReadExactlyAsync(stream, actual, cancellationToken);
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("La verificación de " + entry.Name + " no coincidió.");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("No se pudo verificar el modelo dentro de gta3.img.");
            total += read;
        }
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                throw new EndOfStreamException("gta3.img está incompleto.");
            total += read;
        }
    }

    private static long Align(long value, int alignment) =>
        ((value + alignment - 1) / alignment) * alignment;

    private static void ReportProgress(IProgress<int>? progress, long done, long total)
    {
        if (total <= 0)
        {
            progress?.Report(100);
            return;
        }

        progress?.Report((int)Math.Clamp(done * 100 / total, 0, 100));
    }
}
