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

    private CancellationTokenSource _cts;

    private Task? _gg;

    

    public async Task Setup(Stream stream, int compressionThreshold)
    {
        _pipe = new Pipe();
        _cts = new CancellationTokenSource();
        TaskCompletionSource tcs = new();
        _gg = Task.Run(async () =>
        {
            var reader = _pipe.Reader;
            tcs.TrySetResult();

            try
            {
                while (true)
                {
                    // Используем токен, чтобы ReadAsync можно было прервать снаружи
                    ReadResult result = await reader.ReadAsync(_cts.Token);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            // Пишем в сеть по сегментам — безопасно и без лишних аллокаций
                            await stream.WriteAsync(buffer);
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
                // Завершение reader'а
                try
                {
                    await _pipe.Reader.CompleteAsync();
                }
                catch
                {
                }

                //Console.WriteLine("Reader task finished");
            }
        });

        // сохраняем stream и writer
        _stream = stream;
        _writer = new MinecraftPacketPipeWriter(_pipe.Writer)
        {
            CompressionThreshold = compressionThreshold
        };

        // Ждём, пока он стартует
        await tcs.Task.ConfigureAwait(false);
    }

    public async Task Run(int packetsCount, ReadOnlyMemory<byte> packet)
    {
        for (int i = 0; i < packetsCount; i++)
        {
            await _writer.SendPacketAsync(packet);
            await _writer.FlushAsync();
        }
    }

    public async Task Cleanup()
    {
        try
        {
            // 1) Сигналим отмену выполнения (ReadAsync будет выброшен OperationCanceledException)
            _cts.Cancel();

            // 2) Просим писатель/читатель завершиться корректно
            await _pipe.Writer.CompleteAsync().ConfigureAwait(false);
            _pipe.Reader.CancelPendingRead();

            // 3) Дождаться фоновой задачи (которая уже получила отмену и завершит loop)
            if (_gg is not null)
                await _gg.ConfigureAwait(false);

            // 4) Завершаем reader на всякий случай
            await _pipe.Reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Cleanup exception: " + ex);
        }
        finally
        {
            _cts.Dispose();
            _gg = null;

            if (_stream is not null)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                _stream = null;
            }

            _pipe.Reset();
        }
    }
}