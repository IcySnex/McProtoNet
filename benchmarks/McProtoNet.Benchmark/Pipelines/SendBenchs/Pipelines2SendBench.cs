using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines.SendBenchs;

public class Pipelines2SendBench : ISendBench
{
    private MinecraftPacketPipeWriter _writer;

    private Stream _stream;

    private Pipe _pipe = new();



    

    public async Task Setup(Stream stream, int compressionThreshold)
    {
        _stream = stream;
        _writer = new MinecraftPacketPipeWriter(_pipe.Writer)
        {
            CompressionThreshold = compressionThreshold
        };
    }

    public async Task Run(int packetsCount, ReadOnlyMemory<byte> packet)
    {
        var task = Task.Run(async () =>
        {
            var reader = _pipe.Reader;

            try
            {
                while (true)
                {
                    // Используем токен, чтобы ReadAsync можно было прервать снаружи
                    ReadResult result = await reader.ReadAsync(CancellationToken.None);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            // Пишем в сеть по сегментам — безопасно и без лишних аллокаций
                            await _stream.WriteAsync(buffer);
                        }

                        if (result.IsCanceled || result.IsCompleted)
                            break;
                    }
                    finally
                    {
                        // Если пусто — AdvanceTo(start), иначе AdvanceTo(end)
                        reader.AdvanceTo(buffer.IsEmpty ? buffer.Start : buffer.End);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                /* ожидаемо при Cancel */
            }
            catch (Exception ex)
            {
                Console.WriteLine("Reader loop exception: " + ex);
                Environment.FailFast("Reader loop error");
            }
            finally
            {
                _pipe.Reader.CancelPendingRead();
                await _pipe.Reader.CompleteAsync();
            }
        });
        for (int i = 0; i < packetsCount; i++)
        {
            await _writer.SendPacketAsync(packet);
            await _writer.FlushAsync();
        }

        await _pipe.Writer.CompleteAsync();
        await task;
    }

    public async Task Cleanup()
    {
        _pipe.Reset();
    }
}