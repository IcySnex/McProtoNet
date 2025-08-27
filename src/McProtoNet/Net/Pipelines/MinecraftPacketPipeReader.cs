using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DotNext;
using DotNext.Buffers;
using DotNext.IO.Pipelines;
using McProtoNet.Abstractions;
using McProtoNet.Net.Zlib;
using LengthFormat = DotNext.IO.LengthFormat;

namespace McProtoNet.Net;

internal sealed class DecryptedPipeReader : PipeReader
{
    public override bool TryRead(out ReadResult result)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public override void AdvanceTo(SequencePosition consumed)
    {
        throw new NotImplementedException();
    }

    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        throw new NotImplementedException();
    }

    public override void CancelPendingRead()
    {
        throw new NotImplementedException();
    }

    public override void Complete(Exception? exception = null)
    {
        throw new NotImplementedException();
    }
}
internal sealed class MinecraftPacketPipeReader
{
    private readonly PipeReader pipeReader;
    //private static readonly MemoryAllocator<byte> s_allocator = ArrayPool<byte>.Shared.ToAllocator();

    private bool isEncrypted;

    public MinecraftPacketPipeReader(PipeReader pipeReader)
    {
        this.pipeReader = pipeReader;
        //this.decompressor = decompressor;
    }

    public int CompressionThreshold { get; set; }


    public async IAsyncEnumerable<NewInputPacket> ReadPacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunkcount = 0;
        cancellationToken.ThrowIfCancellationRequested();
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = default;
            try
            {
                result = await pipeReader.ReadAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await pipeReader.CompleteAsync();
                break;
            }

            var buffer = result.Buffer;
            if (result.IsCompleted) break;

            if (result.IsCanceled) break;
    
            

            try
            {
                while (TryReadPacket(ref buffer, out var packet))
                {
                    yield return Decompress(packet);
                }
            }
            finally
            {
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        await pipeReader.CompleteAsync().ConfigureAwait(false);
    }

    public static Memory<byte> DecryptAES(ReadOnlySequence<byte> buffer, ICryptoTransform decryptor)
    {
        // Вычисляем длину данных
        long totalLength = buffer.Length;

        if (totalLength == 0)
            return Memory<byte>.Empty;

        // Резервируем буфер для расшифрованных данных
        byte[] output = ArrayPool<byte>.Shared.Rent((int)totalLength);

        int offset = 0;

        foreach (var segment in buffer)
        {
            // TransformBlock работает с массивами
            offset += decryptor.TransformBlock(
                segment.Span.ToArray(), // временно создаём массив из сегмента
                0,
                segment.Span.Length,
                output,
                offset
            );
        }

        // В случае последнего блока (TransformFinalBlock) — вызываем отдельно
        // Для пайплайнов обычно весь блок можно считать финальным
        byte[] final = decryptor.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        if (final.Length > 0)
        {
            Array.Copy(final, 0, output, offset, final.Length);
            offset += final.Length;
        }

        // Создаём Memory<byte> только на реально расшифрованные данные
        var result = new Memory<byte>(output, 0, offset);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> packet)
    {
        scoped SequenceReader<byte> reader = new(buffer);


        packet = ReadOnlySequence<byte>.Empty;

        if (buffer.Length < 1) return false; // Недостаточно данных для чтения заголовка пакета

        if (!reader.TryReadVarInt(out var length, out _)) return false; // Невозможно прочитать длину заголовка


        if (length > reader.Remaining) return false; // Недостаточно данных для чтения полного пакета


        packet = reader.UnreadSequence.Slice(0, length);

        reader.Advance(length);


        buffer = buffer.Slice(reader.Position);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NewInputPacket Decompress(in ReadOnlySequence<byte> data)
    {
        if (CompressionThreshold == -1)
        {
            //Без сжатия
            return new NewInputPacket(data);
        }

        data.TryReadVarInt(out var sizeUncompressed, out var len);

        if (sizeUncompressed == 0)
        {
            // Со сжатием, короткий пакет
            return new NewInputPacket(data.Slice(1));
        }

        // Со сжатием, длинный пакет
        return new NewInputPacket(data.Slice(len).Decompress(sizeUncompressed));
    }
}

public struct NewInputPacket : IDisposable
{
    public readonly int Id;
    public readonly ReadOnlySequence<byte> Data;

    private readonly MemoryOwner<byte>? _memoryOwner;

    public NewInputPacket(ReadOnlySequence<byte> data)
    {
        data.TryReadVarInt(out var value, out var offset);
        Id = value;
        Data = data.Slice(offset);
    }


    /// <summary>
    /// Constructor for compressed packet
    /// </summary>
    /// <param name="owner"></param>
    public NewInputPacket(MemoryOwner<byte> owner)
    {
        _memoryOwner = owner;
        var data = new ReadOnlySequence<byte>(owner.Memory);
        data.TryReadVarInt(out var value, out var offset);
        Id = value;
        Data = data.Slice(offset);
    }

    public void Dispose()
    {
        _memoryOwner?.Dispose();
    }
}