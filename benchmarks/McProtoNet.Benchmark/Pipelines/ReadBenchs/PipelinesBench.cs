using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
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


public class Pipelines2Bench : IBench
{
    private MinecraftPacketPipeReader _reader;


    private Stream _stream;
    private Pipe _pipe = new Pipe();
    private CancellationTokenSource _cts;

    public Task Setup(Stream stream, int compressionThreshold)
    {
        Task.Run(async () =>
        {
            try
            {
                var writer = _pipe.Writer;
                while (!_cts.IsCancellationRequested)
                {
                    var memory = writer.GetMemory();
                    int a = await stream.ReadAsync(memory, _cts.Token);
                    if (a == 0)
                        break;
                    writer.Advance(a);
                    var result = writer.FlushAsync(_cts.Token);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                
            }
            
        });
        _stream = stream;
        _cts = new CancellationTokenSource();
        _reader = new MinecraftPacketPipeReader(_pipe.Reader)
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

    public async Task Cleanup()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        await _pipe.Reader.CompleteAsync();
        await _pipe.Writer.CompleteAsync();
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }
        _pipe.Reset();
    }
}