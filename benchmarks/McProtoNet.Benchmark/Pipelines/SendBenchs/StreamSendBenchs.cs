using System;
using System.IO;
using System.Threading.Tasks;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines.SendBenchs;

public class StreamSendBench : ISendBench
{
    private readonly MinecraftPacketSender _sender = new();
    private Stream _stream;

    public Task Setup(Stream stream, int compressionThreshold)
    {
        _stream = stream;
        _sender.BaseStream = _stream;
        _sender.SwitchCompression(compressionThreshold);
        return Task.CompletedTask;
    }

    public async Task Run(int packetsCount, ReadOnlyMemory<byte> packet)
    {
        for (int i = 0; i < packetsCount; i++)
        {
            await _sender.SendPacketAsync(packet);
        }
    }

    public Task Cleanup()
    {
        _stream?.Dispose();
        return Task.CompletedTask;
    }
}