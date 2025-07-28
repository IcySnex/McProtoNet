using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotNext;
using DotNext.Buffers;
using McProtoNet.Abstractions;
using McProtoNet.Net;
using McProtoNet.Serialization;

namespace McProtoNet.Benchmark.Pipelines;

[Config(typeof(AntiVirusFriendlyConfig))]
[MemoryDiagnoser]
public class ReadBenchmarks
{
    [Params(1_000_000)] public int PacketsCount;

    public int CompressionThreshold = 128;


    private static readonly Random Rand = new Random(53);

    private static MemoryOwner<byte> GeneratePacket()
    {
        var packet = new TestPacket
            {
                Name = Rand.NextString("abcdefghijklmnopqrstuvwxyz",50),
                EntityId = Rand.Next(0, 500),
                DX = (short)Rand.Next(0, 500),
                DY = (short)Rand.Next(0, 500),
                DZ = (short)Rand.Next(0, 500),
                Yaw = (sbyte)Rand.Next(0, 100),
                Pitch = (sbyte)Rand.Next(0, 100),
                OnGround = Rand.NextBoolean()
            };
            // {
            //     EntityId = 1,
            //     DX = 2,
            //     DY = 3,
            //     DZ = 4,
            //     Yaw = 5,
            //     Pitch = 6,
            //     OnGround = true,
            //     SpecialData = new byte[1024]
            // };
        MinecraftPrimitiveWriter writer = new();
        writer.WriteVarInt(3); //ID
        packet.Serialize(ref writer);
        return writer.GetWrittenMemory();
    }

    private TcpClient _client;
    private Stream _stream;

    [GlobalSetup]
    public async Task Setup()
    {
        // string tempFileName = Path.GetTempFileName();
        // _mainStream = File.OpenWrite(tempFileName);
        // Random r = new Random(73);
        // var writer = new MinecraftPacketSender();
        //
        // writer.SwitchCompression(CompressionThreshold);
        //
        // writer.BaseStream = _mainStream;
        //
        // for (int i = 0; i < PacketsCount; i++)
        // {
        //     var buffer = GeneratePacket();
        //
        //     var packet = new OutputPacket(buffer);
        //
        //     await writer.SendAndDisposeAsync(packet, CancellationToken.None);
        // }
        // _mainStream.Close();
        // _mainStream = File.OpenRead(tempFileName);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", 6060);
        _stream = _client.GetStream();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _stream.Dispose();
        _client.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // _mainStream.Close();
        // File.Delete(_mainStream.Name);
        // _client.Close();
    }


    //[Benchmark]
    public async Task ReadPacketsStreaming()
    {
        
        var reader = new MinecraftPacketReader();
        reader.SwitchCompression(CompressionThreshold);
        reader.BaseStream = _stream;
        TestPacket packet1 = new TestPacket();
        for (var i = 0; i < PacketsCount; i++)
        {
            using var packet = await reader.ReadNextPacketAsync();

            ReadPacket(packet, packet1);
        }
    }

    private static void CheckPacket(TestPacket packet)
    {
        if (packet.EntityId != 1
            || packet.DX != 2
            || packet.DY != 3
            || packet.DZ != 4
            || packet.Yaw != 5
            || packet.Pitch != 6
            || packet.OnGround != true)
        {
            throw new Exception("Packet data is not correct");
        }
    }

    [Benchmark]
    public async Task ReadPacketsWithPipeLines()
    {
      
        var reader = new MinecraftPacketPipeReader(PipeReader.Create(_stream))
        {
            CompressionThreshold = CompressionThreshold
        };
        var count = 0;
        TestPacket packet1 = new TestPacket();
        await foreach (var packet in reader.ReadPacketsAsync())
        {

            ReadPacket(packet, packet1);
            
            CheckPacket(packet1);
            packet.Dispose();
            count++;
            if (count == PacketsCount)
                break;
        }
    }

    private static TestPacket ReadPacket(NewInputPacket data, TestPacket packet)
    {
        SequenceReader<byte> reader = new SequenceReader<byte>(data.Data);
        packet.Deserialize(ref reader);
        return packet;
    }
    
    private static TestPacket ReadPacket(InputPacket data, TestPacket packet)
    {
        MinecraftPrimitiveReader reader = new (data.Data);
        packet.Deserialize(ref reader);
        return packet;
    }
}

public class TestPacket
{
    private int _entityId;
    
        public string Name { get; set; }

    public int EntityId
    {
        get => _entityId;
        set => _entityId = value;
    }

    private short _dx;

    public short DX
    {
        get => _dx;
        set => _dx = value;
    }

    private short _dy;

    public short DY
    {
        get => _dy;
        set => _dy = value;
    }

    private short _dz;

    public short DZ
    {
        get => _dz;
        set => _dz = value;
    }

    private sbyte _yaw;

    public sbyte Yaw
    {
        get => _yaw;
        set => _yaw = value;
    }

    private sbyte _pitch;

    public sbyte Pitch
    {
        get => _pitch;
        set => _pitch = value;
    }

    private bool _onGround;

    public bool OnGround
    {
        get => _onGround;
        set => _onGround = value;
    }

    //public byte[] SpecialData { get; set; }

    public void Serialize(ref MinecraftPrimitiveWriter writer)
    {
        //writer.WriteString(Name);
        writer.WriteVarInt(EntityId);
        writer.WriteSignedShort(DX);
        writer.WriteSignedShort(DY);
        writer.WriteSignedShort(DZ);
        writer.WriteSignedByte(Yaw);
        writer.WriteSignedByte(Pitch);
        writer.WriteBoolean(OnGround);
        
        // writer.WriteVarInt(SpecialData.Length);
        // writer.WriteBuffer(SpecialData);
    }

    public void Deserialize(ref MinecraftPrimitiveReader reader)
    {
        //Name = reader.ReadString();
        EntityId = reader.ReadVarInt();
        DX = reader.ReadSignedShort();
        DY = reader.ReadSignedShort();
        DZ = reader.ReadSignedShort();
        Yaw = reader.ReadSignedByte();
        Pitch = reader.ReadSignedByte();
        OnGround = reader.ReadBoolean();

        //SpecialData = reader.ReadBuffer(reader.ReadVarInt());
    }

    public void Deserialize(ref SequenceReader<byte> reader)
    {
        
        //reader.TryReadString(out var name);
        //Name = name;
        bool success = true;
        success &= reader.TryReadVarInt(out _entityId, out _);
        success &= reader.TryReadBigEndian(out _dx);
        success &= reader.TryReadBigEndian(out _dy);
        success &= reader.TryReadBigEndian(out _dz);
        success &= reader.TryRead(out var yaw);
        Yaw = (sbyte)yaw;

        success &= reader.TryRead(out var pitch);
        Pitch = (sbyte)pitch;

        success &= reader.TryRead(out var onGround);
        OnGround = onGround == 1;

        if (!success)
            throw new EndOfStreamException("Где то Try не сработал");

        //reader.TryReadVarInt(out var specialDataLength, out _);

        // var specialData = new byte[specialDataLength];
        // reader.TryCopyTo(specialData);
        // SpecialData = specialData;
    }
}