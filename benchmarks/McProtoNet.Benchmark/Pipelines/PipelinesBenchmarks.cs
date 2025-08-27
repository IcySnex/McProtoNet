using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using McProtoNet.Benchmark.Pipelines.ReadBenchs;


namespace McProtoNet.Benchmark.Pipelines;

public enum BenchType
{
    Stream,
    BufferedStream,
    Pipelines,
    Pipelines2
}

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class PipelinesBenchmarks
{
    [Params(1_000_000)] public int PacketsCount;
    [Params(-1)] public int CompressionThreshold;

    [Params(BenchType.BufferedStream, BenchType.Pipelines, BenchType.Pipelines2)]
    public BenchType Bench { get; set; }

    private TestServer _server = new();

    // Инстансы всех реализаций, чтобы не пересоздавать зависимости/конфигурации
    private readonly IBench _streamBench = new StreamBench();
    private readonly IBench _bufferedStreamBench = new BufferedStreamBench();
    private readonly IBench _pipeBench = new PipelinesBench();
    private readonly IBench _pipe2Bench = new Pipelines2Bench();

    // Активный бенч и поток для текущего прогона
    private IBench _activeBench;
    private Stream _stream;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Запускаем сервер один раз перед всеми прогонками
        await _server.Run(PacketsCount, CompressionThreshold);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        // Подключаемся (новый TCP соединение для этой итерации)
        _stream = await Connect();

        // Выбираем и настраиваем только нужный бенч
        switch (Bench)
        {
            case BenchType.Stream:
                _activeBench = _streamBench;
                break;
            case BenchType.BufferedStream:
                _activeBench = _bufferedStreamBench;
                break;
            case BenchType.Pipelines:
                _activeBench = _pipeBench;
                break;
            case BenchType.Pipelines2:
                _activeBench = _pipe2Bench;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _activeBench.Setup(_stream, CompressionThreshold);
    }

    private static async Task<Stream> Connect()
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 6060);
        return client.GetStream(); // Закрытие Stream закроет сокет
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        if (_activeBench != null)
        {
            await _activeBench.Cleanup();
            _activeBench = null;
        }

        if (_stream != null)
        {
            try
            {
                _stream.Dispose();
            }
            catch
            {
                /* ignore */
            }

            _stream = null;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _server.Stop();
    }

    // единый бенчмарк — BenchmarkDotNet выполнит его для каждого значения Bench
    [Benchmark]
    public async Task ReadPackets()
    {
        if (_activeBench == null) throw new InvalidOperationException("Active bench is not configured.");
        await _activeBench.Run(PacketsCount);
    }
}