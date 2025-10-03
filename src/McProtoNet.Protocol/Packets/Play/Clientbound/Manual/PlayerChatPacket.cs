using McProtoNet.Protocol;
using McProtoNet.NBT;
using McProtoNet.Serialization;
using System;

namespace McProtoNet.Protocol.Packets.Play.Clientbound
{
    [PacketInfo("PlayerChat", PacketState.Play, PacketDirection.Clientbound)]
    public abstract partial class PlayerChatPacket : IServerPacket
    {
        [PacketSubInfo(759, 759)]
        public sealed partial class V759 : PlayerChatPacket
        {
            public string SignedChatContent { get; set; }
            public string? UnsignedChatContent { get; set; }
            public int Type { get; set; }
            public Guid SenderUuid { get; set; }
            public string SenderName { get; set; }
            public string? SenderTeam { get; set; }
            public long Timestamp { get; set; }
            public long Salt { get; set; }
            public byte[] Signature { get; set; }

            public override void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion)
            {
                SignedChatContent = reader.ReadString();
                UnsignedChatContent = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
                Type = reader.ReadVarInt();
                SenderUuid = reader.ReadUUID();
                SenderName = reader.ReadString();
                SenderTeam = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
                Timestamp = reader.ReadSignedLong();
                Salt = reader.ReadSignedLong();

                Signature = reader.ReadBuffer(LengthFormat.VarInt);
            }
        }

        [PacketSubInfo(760, 760)]
        public sealed partial class V760 : PlayerChatPacket
        {
            public byte[]? PreviousSignature { get; set; }
            public Guid SenderUuid { get; set; }
            public byte[] Signature { get; set; }
            public string PlainMessage { get; set; }
            public string? FormattedMessage { get; set; }
            public long Timestamp { get; set; }
            public long Salt { get; set; }
            public int[] PreviousMessages { get; set; }
            public string? UnsignedChatContent { get; set; }
            public int FilterType { get; set; }
            public long[]? FilterTypeMask { get; set; }
            public int Type { get; set; }
            public string NetworkName { get; set; }
            public string? NetworkTargetName { get; set; }

            public override void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion)
            {
                PreviousSignature =
                    reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadBuffer(LengthFormat.VarInt));
                SenderUuid = reader.ReadUUID();
                Signature = reader.ReadBuffer(LengthFormat.VarInt);
                PlainMessage = reader.ReadString();
                FormattedMessage = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
                Timestamp = reader.ReadSignedLong();
                Salt = reader.ReadSignedLong();
                PreviousMessages = reader.ReadArray<int, VarIntArrayReader>(LengthFormat.VarInt);
                UnsignedChatContent = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
                FilterType = reader.ReadVarInt();
                if (FilterType == 2)
                    FilterTypeMask = reader.ReadArray<long, LongArrayReader>(LengthFormat.VarInt);
                Type = reader.ReadVarInt();
                NetworkName = reader.ReadString();
                NetworkTargetName = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
            }
        }

        [PacketSubInfo(761, 764)]
        public sealed partial class V761_764 : PlayerChatPacket
        {
            public Guid SenderUuid { get; set; }
            public int Index { get; set; }
            public byte[]? Signature { get; set; }
            public string PlainMessage { get; set; }
            public long Timestamp { get; set; }
            public long Salt { get; set; }
            public int[] PreviousMessages { get; set; }
            public string? UnsignedChatContent { get; set; }
            public int FilterType { get; set; }
            public long[]? FilterTypeMask { get; set; }
            public int Type { get; set; }
            public string NetworkName { get; set; }
            public string? NetworkTargetName { get; set; }

            public override void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion)
            {
                SenderUuid = reader.ReadUUID();
                Index = reader.ReadVarInt();
                Signature = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadBuffer(256));
                PlainMessage = reader.ReadString();
                Timestamp = reader.ReadSignedLong();
                Salt = reader.ReadSignedLong();

                PreviousMessages = reader.ReadArray<int, VarIntArrayReader>(LengthFormat.VarInt);
                UnsignedChatContent = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
                FilterType = reader.ReadVarInt();
                if (FilterType == 2)
                    FilterTypeMask = reader.ReadArray<long, LongArrayReader>(LengthFormat.VarInt);
                Type = reader.ReadVarInt();
                NetworkName = reader.ReadString();
                NetworkTargetName = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadString());
            }
        }

        [PacketSubInfo(765, 766)]
        public sealed partial class V765_766 : PlayerChatPacket
        {
            public Guid SenderUuid { get; set; }
            public int Index { get; set; }
            public byte[]? Signature { get; set; }
            public string PlainMessage { get; set; }
            public long Timestamp { get; set; }
            public long Salt { get; set; }
            public int[] PreviousMessages { get; set; }
            public NbtTag? UnsignedChatContent { get; set; }
            public int FilterType { get; set; }
            public long[]? FilterTypeMask { get; set; }
            public int Type { get; set; }

            public override void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion)
            {
                SenderUuid = reader.ReadUUID();
                Index = reader.ReadVarInt();
                Signature = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadBuffer(256));
                PlainMessage = reader.ReadString();
                Timestamp = reader.ReadSignedLong();
                Salt = reader.ReadSignedLong();

                PreviousMessages = reader.ReadArray<int, VarIntArrayReader>(LengthFormat.VarInt);
                UnsignedChatContent = reader.ReadOptionalNbtTag(protocolVersion);
                FilterType = reader.ReadVarInt();
                if (FilterType == 2)
                    FilterTypeMask = reader.ReadArray<long, LongArrayReader>(LengthFormat.VarInt);
                Type = reader.ReadVarInt();
            }
        }

        [PacketSubInfo(767, 769)]
        public sealed partial class V767_769 : PlayerChatPacket
        {
            public Guid SenderUuid { get; set; }
            public int Index { get; set; }
            public byte[]? Signature { get; set; }
            public string PlainMessage { get; set; }
            public long Timestamp { get; set; }
            public long Salt { get; set; }
            public int[] PreviousMessages { get; set; }
            public NbtTag? UnsignedChatContent { get; set; }
            public int FilterType { get; set; }
            public long[]? FilterTypeMask { get; set; }

            public override void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion)
            {
                SenderUuid = reader.ReadUUID();
                Index = reader.ReadVarInt();
                Signature = reader.ReadOptional((ref MinecraftPrimitiveReader r) => r.ReadBuffer(256));
                PlainMessage = reader.ReadString();
                Timestamp = reader.ReadSignedLong();
                Salt = reader.ReadSignedLong();

                PreviousMessages = reader.ReadArray<int, VarIntArrayReader>(LengthFormat.VarInt);
                UnsignedChatContent = reader.ReadOptionalNbtTag(protocolVersion);
                FilterType = reader.ReadVarInt();
                if (FilterType == 2)
                    FilterTypeMask = reader.ReadArray<long, LongArrayReader>(LengthFormat.VarInt);
            }
        }

        public abstract void Deserialize(ref MinecraftPrimitiveReader reader, int protocolVersion);
    }
}