using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotNext.IO;
using McProtoNet.Abstractions;
using McProtoNet.Benchmark.Pipelines.ReadBenchs;
using McProtoNet.Net;

namespace McProtoNet.Benchmark.Pipelines;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class PipelinesBenchmarks
{
    [Params(1_000_000)] public int PacketsCount;

    public int CompressionThreshold = 0;


    private TcpClient _client;
    private Stream _stream;

    private TestServer _server = new();

    private IBench _streamBench = new StreamBench();
    private IBench _bufferedStreamBench = new BufferedStreamBench();
    private IBench _pipeBench = new PipelinesBench();
    private IBench _pipe2Bench = new Pipelines2Bench();

    [GlobalSetup]
    public async Task Setup()
    {
        await _server.Run(PacketsCount, CompressionThreshold);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", 6060);
        _stream = _client.GetStream();
        await Task.Delay(50);

        _streamBench.Setup(_stream, CompressionThreshold);
        _bufferedStreamBench.Setup(_stream, CompressionThreshold);
        _pipeBench.Setup(_stream, CompressionThreshold);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _stream.Dispose();
        _client.Dispose();
        _streamBench.Cleanup();
        _bufferedStreamBench.Cleanup();
        _pipeBench.Cleanup();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _server.Stop();
    }


    //[Benchmark]
    public async Task ReadPacketsStreaming()
    {
        await _streamBench.Run(PacketsCount);
    }
    [Benchmark]
    public async Task ReadPacketsBufferedStreaming()
    {
        await _bufferedStreamBench.Run(PacketsCount);
    }
    

    [Benchmark]
    public async Task ReadPacketsWithPipeLines()
    {
        await _pipeBench.Run(PacketsCount);
    }

    [Benchmark]
    public async Task ReadPacketsWithPipeLines2()
    {
        await _pipe2Bench.Run(PacketsCount);
    }

   
}