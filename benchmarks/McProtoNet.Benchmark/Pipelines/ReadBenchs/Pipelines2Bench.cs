using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines.ReadBenchs;

public class Pipelines2Bench : IBench
{
    private MinecraftPacketPipeReader _reader;


    private Stream _stream;
    private Pipe _pipe = new(new PipeOptions(useSynchronizationContext: false, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline));
    private CancellationTokenSource _cts;

    public async Task Setup(Stream stream, int compressionThreshold)
    {
        TaskCompletionSource tcs = new();
        _= Task.Run(async () =>
        {
            try
            {
                var writer = _pipe.Writer;
                tcs.SetResult();
                while (!_cts.IsCancellationRequested)
                {
                    var memory = writer.GetMemory();
                    int a = await stream.ReadAsync(memory, _cts.Token);
                    if (a == 0)
                        break;
                    writer.Advance(a);
                    var result = await writer.FlushAsync(_cts.Token);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Environment.FailFast("Error");
            }
            finally
            {
                
            }
            
        });
        _stream = stream;
        _cts = new CancellationTokenSource();
        _reader = new MinecraftPacketPipeReader(_pipe.Reader)
        {
            CompressionThreshold = compressionThreshold
        };
        await tcs.Task;
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