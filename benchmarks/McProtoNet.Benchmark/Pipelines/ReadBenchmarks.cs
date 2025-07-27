using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
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

    [Params(-1, 128)] public int CompressionThreshold;


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

    private Stream _mainStream;

    [GlobalSetup]
    public async Task Setup()
    {
        MemoryStream ms = new MemoryStream();
        _mainStream = ms;
        Random r = new Random(73);
        var writer = new MinecraftPacketSender();

        writer.SwitchCompression(CompressionThreshold);

        var allocator = ArrayPool<byte>.Shared.ToAllocator();

        writer.BaseStream = ms;

        for (int i = 0; i < PacketsCount; i++)
        {
            var buffer = GeneratePacket();

            var packet = new OutputPacket(buffer);

            await writer.SendAndDisposeAsync(packet, CancellationToken.None);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
    }


    [Benchmark]
    public async Task ReadPacketsStreaming()
    {
        _mainStream.Position = 0;
        var reader = new MinecraftPacketReader();
        reader.SwitchCompression(CompressionThreshold);
        reader.BaseStream = _mainStream;
        TestPacket packet1 = new TestPacket();
        for (var i = 0; i < PacketsCount; i++)
        {
            using var packet = await reader.ReadNextPacketAsync();

            // if (packet.Id != 53)
            // {
            //     throw new Exception($"Packet id fail: {packet.Id}");
            // }

            var parser = new MinecraftPrimitiveReader(packet.Data);

            packet1.Deserialize(ref parser);

            //CheckPacket(packet1);
        }
    }

    private void CheckPacket(TestPacket packet)
    {
        if (packet.EntityId != 1 || packet.DX != 2 || packet.DY != 3 || packet.DZ != 4 || packet.Yaw != 5 ||
            packet.Pitch != 6 || packet.OnGround != true)
        {
            throw new Exception(
                $"Packet is not as expected. EntityId: {packet.EntityId}, DX: {packet.DX}, DY: {packet.DY}, DZ: {packet.DZ}, Yaw: {packet.Yaw}, Pitch: {packet.Pitch}, OnGround: {packet.OnGround}");
        }
    }

    [Benchmark]
    public async Task ReadPacketsWithPipeLines()
    {
        _mainStream.Position = 0;
        var reader = new MinecraftPacketPipeReader(PipeReader.Create(_mainStream))
        {
            CompressionThreshold = CompressionThreshold
        };
        var count = 0;
        TestPacket packet1 = new TestPacket();
        await foreach (var packet in reader.ReadPacketsAsync())
        {
            // if (packet.Id != 53)
            // {
            //     throw new Exception($"Packet id fail: {packet.Id}");
            // }

            ReadPacket(packet, packet1);

            //CheckPacket(packet1);


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
        writer.WriteString(Name);
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
        Name = reader.ReadString();
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
        
        reader.TryReadString(out var name);
        Name = name;
        
        reader.TryReadVarInt(out _entityId, out _);
        reader.TryReadBigEndian(out _dx);
        reader.TryReadBigEndian(out _dy);
        reader.TryReadBigEndian(out _dz);
        reader.TryRead(out var yaw);
        Yaw = (sbyte)yaw;

        reader.TryRead(out var pitch);
        Pitch = (sbyte)pitch;

        reader.TryRead(out var onGround);
        OnGround = onGround == 1;

        //reader.TryReadVarInt(out var specialDataLength, out _);

        // var specialData = new byte[specialDataLength];
        // reader.TryCopyTo(specialData);
        // SpecialData = specialData;
    }
}