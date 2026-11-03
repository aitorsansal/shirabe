// Brotli-compresses the bundled dictionary database for the APK.
// Run by the app's CompressDictionaryDatabase MSBuild target; usable by hand too:
//   dotnet run --file scripts/compress-dictionary.cs -- <input.db3> <output.db3.br>
//
// Quality 11 with the maximum 16 MB window (24) compresses the database to
// roughly two thirds of what the APK's own zip deflate achieves. It takes a few
// minutes, which is why the MSBuild target only reruns it when the input changes.
// The output is written to a temporary file and moved into place at the end, so
// an interrupted run never leaves a truncated file that MSBuild would then treat
// as up to date.

using System.Buffers;
using System.IO.Compression;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: compress-dictionary.cs <input> <output>");
    return 1;
}

var (input, output) = (args[0], args[1]);
var temporary = output + ".tmp";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

using (var encoder = new BrotliEncoder(quality: 11, window: 24))
using (var source = File.OpenRead(input))
using (var destination = File.Create(temporary))
{
    var inBuffer = new byte[1 << 20];
    var outBuffer = new byte[1 << 20];
    int read;
    while ((read = source.Read(inBuffer)) > 0)
        Drain(encoder, inBuffer.AsSpan(0, read), outBuffer, destination, isFinalBlock: false);
    Drain(encoder, ReadOnlySpan<byte>.Empty, outBuffer, destination, isFinalBlock: true);
}

File.Move(temporary, output, overwrite: true);
Console.WriteLine($"{Path.GetFileName(input)}: {new FileInfo(input).Length:N0} -> {new FileInfo(output).Length:N0} bytes");
return 0;

static void Drain(BrotliEncoder encoder, ReadOnlySpan<byte> data, byte[] outBuffer, Stream destination, bool isFinalBlock)
{
    while (true)
    {
        var status = encoder.Compress(data, outBuffer, out var consumed, out var written, isFinalBlock);
        destination.Write(outBuffer, 0, written);
        data = data[consumed..];

        if (status == OperationStatus.InvalidData)
            throw new InvalidDataException("Brotli encoder rejected the input.");
        // DestinationTooSmall: more output pending for the same input, loop again.
        if (status == OperationStatus.Done || (status == OperationStatus.NeedMoreData && data.IsEmpty))
            return;
    }
}
