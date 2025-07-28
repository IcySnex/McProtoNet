using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Buffers;
using McProtoNet.Abstractions;
using McProtoNet.Net;
using McProtoNet.Serialization;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using DotNext.Buffers;
using DotNext.Diagnostics;
using McProtoNet;
using McProtoNet.Serialization;

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

class Program
{
    private static readonly Random Rand = new Random(53);

    private static MemoryOwner<byte> GeneratePacket()
    {
        var packet = new TestPacket
        // {
        //     Name = Rand.NextString("abcdefghijklmnopqrstuvwxyz", 50),
        //     EntityId = Rand.Next(0, 500),
        //     DX = (short)Rand.Next(0, 500),
        //     DY = (short)Rand.Next(0, 500),
        //     DZ = (short)Rand.Next(0, 500),
        //     Yaw = (sbyte)Rand.Next(0, 100),
        //     Pitch = (sbyte)Rand.Next(0, 100),
        //     OnGround = Rand.NextBoolean()
        // };
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

    public static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, 6060);
        listener.Start();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var writer = new MinecraftPacketSender();

        writer.SwitchCompression(128);

        var stream = new MemoryStream();
        writer.BaseStream = stream;

        for (int i = 0; i < 1_000_000; i++)
        {
            var buffer = GeneratePacket();

            var packet = new OutputPacket(buffer);

            await writer.SendAndDisposeAsync(packet, CancellationToken.None);
        }

        bytes = stream.ToArray();

        Console.WriteLine($"Generate {bytes.Length} bytes in {stopwatch.ElapsedMilliseconds} ms");

        while (true)
        {
            var socket = await listener.AcceptSocketAsync(CancellationToken.None);
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Connected");
                    var time = new Timestamp();
                    await using var ns = new NetworkStream(socket, true);
                    await ns.WriteAsync(bytes, CancellationToken.None);
                    Console.WriteLine($"Sent Time: {time.Elapsed}");
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    Console.WriteLine("Disconnected");
                }
            });
        }
    }
}