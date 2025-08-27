using System.IO;
using System.Threading.Tasks;
using DotNext.IO;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines.ReadBenchs;

public class BufferedStreamReadBench : IReceiveBench
{
    private readonly MinecraftPacketReader _reader;

    private Stream _stream;

    public BufferedStreamReadBench()
    {
        _reader = new MinecraftPacketReader();
    }


    public Task Setup(Stream stream, int compressionThreshold)
    {
        _reader.SwitchCompression(compressionThreshold);
        _stream = new PoolingBufferedStream(stream);
        _reader.BaseStream = _stream;
        return Task.CompletedTask;
    }

    public async Task Run(int packetsCount)
    {
        for (var i = 0; i < packetsCount; i++)
        {
            using var packet = await _reader.ReadNextPacketAsync();
        }
    }

    public Task Cleanup()
    {
        _stream?.Dispose();
        return Task.CompletedTask;
    }
}