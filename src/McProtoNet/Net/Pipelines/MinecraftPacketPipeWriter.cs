using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using DotNext.Buffers;
using McProtoNet.Net.Zlib;
using McProtoNet.Serialization;

namespace McProtoNet.Net;

internal sealed class MinecraftPacketPipeWriter
{
    private static readonly byte[] ZeroVarInt = { 0 };

    private readonly PipeWriter pipeWriter;
    private readonly ICryptoTransform cryptoTransform;

    public MinecraftPacketPipeWriter(PipeWriter pipeWriter)
    {
        this.pipeWriter = pipeWriter;
    }

    public ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
        pipeWriter.FlushAsync(cancellationToken);

    public int CompressionThreshold { get; set; }

    public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (CompressionThreshold < 0)
        {
            pipeWriter.WriteVarInt(data.Length);
            pipeWriter.Write(data.Span);
            //await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (data.Length < CompressionThreshold)
        {
            
            pipeWriter.WriteVarInt(data.Length + 1);
            pipeWriter.WriteVarInt(0);

            //await pipeWriter.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new NotSupportedException();
        // var uncompressedSize = data.Length;
        // using scoped var compressor = new ZlibCompressor();
        // var length = compressor.GetBound(uncompressedSize);
        //
        // var compressedBuffer = ArrayPool<byte>.Shared.Rent(length);
        //
        // try
        // {
        //     var bytesCompress = compressor.Compress(data.Span, compressedBuffer.AsSpan(0, length));
        //
        //     var compressedLength = bytesCompress;
        //
        //     var fullsize = compressedLength + uncompressedSize.GetVarIntLength();
        //
        //     pipeWriter.WriteVarInt(fullsize);
        //     pipeWriter.WriteVarInt(uncompressedSize);
        //     pipeWriter.Write(compressedBuffer.AsSpan(0, bytesCompress));
        // }
        // finally
        // {
        //     ArrayPool<byte>.Shared.Return(compressedBuffer);
        // }
        //
        // return pipeWriter.FlushAsync(cancellationToken);
    }
}