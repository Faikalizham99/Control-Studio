using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using ControlStudio.Core.Widgets;
using ControlStudio.Infrastructure;

// Run from the repository root. This measures real publisher/file work on this
// computer, not UIKit rendering, device memory, FPS or WidgetKit delivery.
var scratch = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".tmp", "widget-performance"));
Directory.CreateDirectory(scratch);
var run = Path.Combine(scratch, "run-" + Guid.NewGuid().ToString("N"));
var results = new List<object>();
var force = args.Contains("--force", StringComparer.Ordinal);
if (args.Any(x => x != "--force")) throw new ArgumentException("Supported option: --force (fully revalidate repeated publications).");
try
{
    var png = MakePng();
    foreach (var (count, layers) in new[] { (1, 1), (10, 10), (50, 30) })
    {
        var folder = Path.Combine(run, count.ToString());
        var repository = new WidgetDesignRepository(Path.Combine(folder, "private"));
        var asset = await repository.ImportPhotoAsync(png);
        for (var index = 0; index < count; index++)
        {
            var photos = Enumerable.Range(0, layers).Select(i => new WidgetLayer(Guid.NewGuid(), "Photo " + i, asset, 1024, 1024, new())).ToArray();
            await repository.SaveAsync(WidgetDesignRules.New() with { Name = "Widget " + index, Layers = photos }, png);
        }
        var publisher = new WidgetDesignPublisher(repository, () => Path.Combine(folder, "shared"));
        Task Publish() => force ? publisher.PublishAsync() : publisher.PublishIfChangedAsync();
        await publisher.PublishAsync(); // First publication installs and validates all payloads.
        await Publish(); // Warm code paths and filesystem cache.
        var samples = new List<(double Milliseconds, long Bytes)>();
        for (var pass = 0; pass < 7; pass++)
        {
            var allocated = GC.GetTotalAllocatedBytes(precise: true);
            var timer = Stopwatch.StartNew();
            await Publish();
            timer.Stop();
            samples.Add((timer.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - allocated));
        }
        results.Add(new { designs = count, photoLayersPerDesign = layers, pngBytes = png.Length,
            medianMilliseconds = Math.Round(samples.Select(x => x.Milliseconds).Order().ElementAt(3), 3),
            medianAllocatedBytes = samples.Select(x => x.Bytes).Order().ElementAt(3),
            samples = samples.Select(x => new { milliseconds = Math.Round(x.Milliseconds, 3), allocatedBytes = x.Bytes }) });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
        os = RuntimeInformation.OSDescription, processorCount = Environment.ProcessorCount,
        workload = "Repeated publication after warm initial installation; seven passes per case", mode = force ? "explicit-refresh" : "unchanged-reload",
        results }, new JsonSerializerOptions { WriteIndented = true }));
}
finally
{
    // Only remove this benchmark's own generated child, never arbitrary input.
    if (Path.GetFullPath(run).StartsWith(scratch + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(run))
        Directory.Delete(run, recursive: true);
}

static byte[] MakePng()
{
    const int edge = 1024;
    var random = new Random(2044);
    using var compressed = new MemoryStream();
    using (var zip = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
    {
        var row = new byte[edge * 3 + 1];
        for (var y = 0; y < edge; y++) { row[0] = 0; random.NextBytes(row.AsSpan(1)); zip.Write(row); }
    }
    using var output = new MemoryStream();
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, edge); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), edge);
    header[8] = 8; header[9] = 2; // Eight-bit RGB, no palette/interlace.
    Chunk("IHDR"u8, header); Chunk("IDAT"u8, compressed.ToArray()); Chunk("IEND"u8, []);
    return output.ToArray();

    void Chunk(ReadOnlySpan<byte> kind, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number); output.Write(kind); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (var value in kind) Update(value);
        foreach (var value in data) Update(value);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
        void Update(byte value) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0); }
    }
}
