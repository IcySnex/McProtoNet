using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.Diagnostics;
using McProtoNet.Abstractions;
using McProtoNet.Net;
using McProtoNet.Serialization;

namespace McProtoNet.Benchmark.Pipelines;

public class TestServer
{
    private static MemoryOwner<byte> GeneratePacket()
    {
        var packet = new TestPacket
        {
            EntityId = 1,
            DX = 2,
            DY = 3,
            DZ = 4,
            Yaw = 5,
            Pitch = 6,
            OnGround = true
        };
        MinecraftPrimitiveWriter writer = new();
        writer.WriteVarInt(3); //ID
        packet.Serialize(ref writer);
        return writer.GetWrittenMemory();
    }

    private static byte[] bytes;

    private TcpListener listener;
    private CancellationTokenSource cts;

    public async Task Run(int packetsCount, int compressionThreshold)
    {
        cts = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Any, 6060);
        listener.Start();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var writer = new MinecraftPacketSender();
        writer.SwitchCompression(compressionThreshold);

        var stream = new MemoryStream();
        writer.BaseStream = stream;

        for (int i = 0; i < packetsCount; i++)
        {
            var buffer = GeneratePacket();
            var packet = new OutputPacket(buffer);
            await writer.SendAndDisposeAsync(packet, CancellationToken.None);
        }

        bytes = stream.ToArray();

        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var socket = await listener.AcceptSocketAsync(CancellationToken.None);
                await Task.Run(async () =>
                {
                    try
                    {
                        //Console.WriteLine("Connected");
                        var time = new Timestamp();
                        await using var ns = new NetworkStream(socket, true);
                        await ns.WriteAsync(bytes, CancellationToken.None);
                        //Console.WriteLine($"Sent Time: {time.Elapsed}");
                    }
                    catch (Exception e)
                    {
                        
                        //Console.WriteLine(e);
                    }
                    finally
                    {
                        //Console.WriteLine("Disconnected");
                    }
                });
            }
        });


        
    }

    public void Stop()
    {
        cts.Cancel();
        listener.Stop();
        listener.Dispose();
        cts.Dispose();
    }
}