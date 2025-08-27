using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines.ReadBenchs;

public class PipelinesBench : IBench
{
    private MinecraftPacketPipeReader _reader;


    private Stream _stream;

    public Task Setup(Stream stream, int compressionThreshold)
    {
        _stream = stream;
        _reader = new MinecraftPacketPipeReader(PipeReader.Create(_stream))
        {
            CompressionThreshold = compressionThreshold
        };
        return Task.CompletedTask;
        
    }

    public async Task Run(int packetsCount)
    {
        var count = 0;
        await foreach (var packet in _reader.ReadPacketsAsync())
        {
            packet.Dispose();
            count++;
            if (count == packetsCount)
                break;
        }
    }

    public Task Cleanup()
    {
        _stream?.Dispose();
        return Task.CompletedTask;
    }
}