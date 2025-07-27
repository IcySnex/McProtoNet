using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DotNext.Buffers;
using McProtoNet.Abstractions;
using McProtoNet.Net.Zlib;
using McProtoNet.Serialization;

namespace McProtoNet.Net;

/// <summary>
/// Reads Minecraft protocol packets from a stream, handling compression if enabled
/// </summary>
public sealed class MinecraftPacketReader
{
    private static readonly MemoryAllocator<byte> memoryAllocator = ArrayPool<byte>.Shared.ToAllocator();

    /// <summary>
    /// The compression threshold in bytes. Values less than 0 indicate compression is disabled.
    /// </summary>
    private int _compressionThreshold = -1;

    /// <summary>
    /// Gets or sets the underlying stream to read packets from
    /// </summary>
    public Stream BaseStream { get; set; }

    /// <summary>
    /// Reads the next packet from the stream asynchronously
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation</param>
    /// <returns>The read packet data</returns>
    /// <exception cref="Exception">Thrown when decompression fails or packet size is invalid</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    //[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<InputPacket> ReadNextPacketAsync(CancellationToken token = default)
    {
        var len = await BaseStream.ReadVarIntAsync(token);

        var buffer = memoryAllocator.AllocateExactly(len);
        try
        {
            await BaseStream.ReadExactlyAsync(buffer.Memory, token);

            if (_compressionThreshold < 0)
            {
                return new InputPacket(buffer);
            }

            var sizeUncompressed = buffer.Span.ReadVarInt(out var offsetSizeUncompressed);

            if (sizeUncompressed <= 0) return new InputPacket(buffer, offset: offsetSizeUncompressed);
            
            
            var memoryOwner = memoryAllocator.AllocateExactly(sizeUncompressed);
            try
            {
                DecompressCore(buffer.Span[offsetSizeUncompressed..], memoryOwner.Span);

                return new InputPacket(memoryOwner);
            }
            catch
            {
                memoryOwner.Dispose();
                throw;
            }
            finally
            {
                buffer.Dispose();
            }

        }
        catch
        {
            buffer.Dispose();
            throw;
        }

        // var len = await BaseStream.ReadVarIntAsync(token);
        // if (_compressionThreshold < 0)
        // {
        //     var buffer = memoryAllocator.AllocateExactly(len);
        //     try
        //     {
        //         await BaseStream.ReadExactlyAsync(buffer.Memory, token);
        //         return new InputPacket(buffer);
        //     }
        //     catch
        //     {
        //         buffer.Dispose();
        //         throw;
        //     }
        // }
        //
        // var sizeUncompressed = await BaseStream.ReadVarIntAsync(token);
        // if (sizeUncompressed > 0)
        // {
        //     if (sizeUncompressed < _compressionThreshold)
        //         throw new Exception(
        //             $"Длина sizeUncompressed меньше порога сжатия. sizeUncompressed: {sizeUncompressed} Порог: {_compressionThreshold}");
        //     len -= sizeUncompressed.GetVarIntLength();
        //
        //     var compressedBuffer = memoryAllocator.AllocateExactly(len);
        //
        //     try
        //     {
        //         await BaseStream.ReadExactlyAsync(compressedBuffer.Memory, token);
        //         var memoryOwner = memoryAllocator.AllocateExactly(sizeUncompressed);
        //         try
        //         {
        //             DecompressCore(compressedBuffer.Span, memoryOwner.Span);
        //             return new InputPacket(memoryOwner);
        //         }
        //         catch
        //         {
        //             memoryOwner.Dispose();
        //             throw;
        //         }
        //     }
        //     finally
        //     {
        //         compressedBuffer.Dispose();
        //     }
        // }
        //
        // {
        //     if (sizeUncompressed != 0)
        //         throw new Exception("size incorrect");
        //
        //     var buffer = memoryAllocator.AllocateExactly(len - 1); // -1 is sizeUncompressed length !!!
        //     try
        //     {
        //         await BaseStream.ReadExactlyAsync(buffer.Memory, token);
        //         return new InputPacket(buffer);
        //     }
        //     catch
        //     {
        //         buffer.Dispose();
        //         throw;
        //     }
        // }
    }

    /// <summary>
    /// Decompresses data using LibDeflate
    /// </summary>
    /// <param name="bufferCompress">The compressed data buffer</param>
    /// <param name="uncompress">The buffer to store decompressed data</param>
    /// <exception cref="Exception">Thrown when decompression fails or output size is incorrect</exception>
    private static void DecompressCore(ReadOnlySpan<byte> bufferCompress, Span<byte> uncompress)
    {
        var decompressor = LibDeflateCache.RentDecompressor();
        var status = decompressor.Decompress(
            bufferCompress,
            uncompress, out var written);

        if (written != uncompress.Length)
            throw new Exception("Written not equal uncompress buffer length");

        if (status != OperationStatus.Done) throw new Exception("Decompress Error");
    }

    /// <summary>
    /// Enables or disables packet compression with the specified threshold
    /// </summary>
    /// <param name="threshold">The compression threshold in bytes. Values less than 0 disable compression.</param>
    public void SwitchCompression(int threshold)
    {
        _compressionThreshold = threshold;
    }
}