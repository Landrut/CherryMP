﻿using System.Collections.Generic;
using ProtoBuf;

namespace CherryMPShared
{
    [ProtoContract]
    public class NativeResponse
    {
        [ProtoMember(1)]
        public NativeArgument Response { get; set; }

        [ProtoMember(2)]
        public uint Id { get; set; }
    }

    [ProtoContract]
    public class NativeTickCall
    {
        [ProtoMember(1)]
        public NativeData Native { get; set; }

        [ProtoMember(2)]
        public string Identifier { get; set; }
    }

    [ProtoContract]
    public class ScriptEventTrigger
    {
        [ProtoMember(1)]
        public string EventName { get; set; }

        [ProtoMember(2)]
        public List<NativeArgument> Arguments { get; set; }

        [ProtoMember(3)]
        public string Resource { get; set; }
    }

    [ProtoContract]
    public class NativeData
    {
        [ProtoMember(1)]
        public ulong Hash { get; set; }

        [ProtoMember(2)]
        public List<NativeArgument> Arguments { get; set; }

        [ProtoMember(3)]
        public NativeArgument ReturnType { get; set; }

        [ProtoMember(4)]
        public uint Id { get; set; }

        [ProtoMember(5)]
        public bool Internal { get; set; }
    }

    [ProtoContract]
    public class ObjectData
    {
        [ProtoMember(1)]
        public Vector3 Position { get; set; }

        [ProtoMember(2)]
        public float Radius { get; set; }

        [ProtoMember(3)]
        public int modelHash { get; set; }
    }

    [ProtoContract]
    [ProtoInclude(2, typeof(IntArgument))]
    [ProtoInclude(3, typeof(UIntArgument))]
    [ProtoInclude(4, typeof(StringArgument))]
    [ProtoInclude(5, typeof(FloatArgument))]
    [ProtoInclude(6, typeof(BooleanArgument))]
    [ProtoInclude(7, typeof(LocalPlayerArgument))]
    [ProtoInclude(8, typeof(EntityArgument))]
    [ProtoInclude(9, typeof(LocalGamePlayerArgument))]
    [ProtoInclude(10, typeof(Vector3Argument))]
    [ProtoInclude(11, typeof(EntityPointerArgument))]
    [ProtoInclude(12, typeof(ListArgument))]
    public class NativeArgument
    {
        [ProtoMember(1)]
        public string Id { get; set; }
    }

    [ProtoContract]
    public class ListArgument : NativeArgument
    {
        [ProtoMember(1)]
        public List<NativeArgument> Data { get; set; }
    }

    [ProtoContract]
    public class LocalPlayerArgument : NativeArgument
    {
    }

    [ProtoContract]
    public class LocalGamePlayerArgument : NativeArgument
    {
    }

    [ProtoContract]
    public class OpponentPedHandleArgument : NativeArgument
    {
        public OpponentPedHandleArgument(long opponentHandle)
        {
            Data = opponentHandle;
        }

        public OpponentPedHandleArgument()
        {
        }

        [ProtoMember(1)]
        public long Data { get; set; }
    }

    [ProtoContract]
    public class IntArgument : NativeArgument
    {
        [ProtoMember(1)]
        public int Data { get; set; }
    }

    [ProtoContract]
    public class UIntArgument : NativeArgument
    {
        [ProtoMember(1)]
        public uint Data { get; set; }
    }

    [ProtoContract]
    public class StringArgument : NativeArgument
    {
        [ProtoMember(1)]
        public string Data { get; set; }
    }

    [ProtoContract]
    public class FloatArgument : NativeArgument
    {
        [ProtoMember(1)]
        public float Data { get; set; }
    }

    [ProtoContract]
    public class BooleanArgument : NativeArgument
    {
        [ProtoMember(1)]
        public bool Data { get; set; }
    }


    [ProtoContract]
    public class Vector3Argument : NativeArgument
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }
    }

    [ProtoContract]
    public class EntityArgument : NativeArgument
    {
        public EntityArgument()
        {
        }

        public EntityArgument(int netHandle)
        {
            NetHandle = netHandle;
        }

        [ProtoMember(1)]
        public int NetHandle { get; set; }
    }

    [ProtoContract]
    public class EntityPointerArgument : NativeArgument
    {
        public EntityPointerArgument(int netHandle)
        {
            NetHandle = netHandle;
        }

        public EntityPointerArgument()
        {
        }

        [ProtoMember(1)]
        public int NetHandle { get; set; }
    }
}