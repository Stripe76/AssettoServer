﻿using System;
using System.ComponentModel;
using System.Numerics;
using System.Text;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Outgoing.Handshake;
using AssettoServer.Shared.Network.Packets.Shared;
using CommunityToolkit.Common.Deferred;

namespace AssettoServer.Server;

public delegate void EventHandler<TSender, TArgs>(TSender sender, TArgs args) where TArgs : EventArgs;
public delegate void EventHandlerIn<TSender, TArg>(TSender sender, in TArg args) where TArg : struct;

public class WelcomeMessageSentEventArgs : EventArgs
{
    public required string WelcomeMessage { get; init; }
    public required string ExtraOptions { get; init; }
    public required string EncodedWelcomeMessage { get; init; }
}

public class WelcomeMessageSendingEventArgs : EventArgs
{
    public required StringBuilder Builder { get; init; }
}

public class CSPServerExtraOptionsSendingEventArgs : DeferredEventArgs
{
    public required StringBuilder Builder { get; init; }
}

public class HandshakeAcceptedEventArgs : DeferredEventArgs
{
    public required HandshakeResponse HandshakeResponse { get; init; }
}

public class ClientAuditEventArgs : EventArgs
{
    public KickReason Reason { get; init; }
    public string? ReasonStr { get; init; }
    public ACTcpClient? Admin { get; init; }
}

public class ChatEventArgs : CancelEventArgs
{
    public string Message { get; }

    public ChatEventArgs(string message)
    {
        Message = message;
    }
}

public class ChatMessageEventArgs : EventArgs
{
    public ChatMessage ChatMessage { get; init; }
}

public class SessionChangedEventArgs : EventArgs
{
    public SessionState? PreviousSession { get; }
    public SessionState NextSession { get; }
    public int InvertedGridSlots { get; }

    public SessionChangedEventArgs(SessionState? previousSession, SessionState nextSession, int invertedGridSlots)
    {
        PreviousSession = previousSession;
        NextSession = nextSession;
        InvertedGridSlots = invertedGridSlots;
    }
}

public class CollisionEventArgs : EventArgs
{
    public EntryCar? TargetCar { get; }
    public float Speed { get; }
    public Vector3 Position { get; }
    public Vector3 RelPosition { get; }

    public CollisionEventArgs(EntryCar? targetCar, float speed, Vector3 position, Vector3 relPosition)
    {
        TargetCar = targetCar;
        Speed = speed;
        Position = position;
        RelPosition = relPosition;
    }
}

public class TyreCompoundChangeEventArgs : EventArgs
{
    public TyreCompoundUpdate Packet { get; }
    
    public TyreCompoundChangeEventArgs(TyreCompoundUpdate packet)
    {
        Packet = packet;
    }
}

public class DamageEventArgs : EventArgs
{
    public DamageUpdate Packet { get; }
    
    public DamageEventArgs(DamageUpdate packet)
    {
        Packet = packet;
    }
}

public class Push2PassEventArgs : EventArgs
{
    public P2PUpdate Packet { get; }
    
    public Push2PassEventArgs(P2PUpdate packet)
    {
        Packet = packet;
    }
}

public class LapCompletedEventArgs : EventArgs
{
    public LapCompletedOutgoing Packet { get; }
    
    public LapCompletedEventArgs(LapCompletedOutgoing packet)
    {
        Packet = packet;
    }
}

public class SectorSplitEventArgs : EventArgs
{
    public SectorSplitOutgoing Packet { get; }
    
    public SectorSplitEventArgs(SectorSplitOutgoing packet)
    {
        Packet = packet;
    }
}

public class CarListResponseSendingEventArgs : EventArgs
{
    public CarListResponse Packet { get; }
    
    public CarListResponseSendingEventArgs(CarListResponse packet)
    {
        Packet = packet;
    }
}

public class GetPluginStatusEventArgs : EventArgs
{
    public EntryCar Target { get; set; }
    public AssettoServer.Shared.Model.CarStatus? Status { get; set; }

    public GetPluginStatusEventArgs( EntryCar target )
    {
        Target = target;
    }
}

public class PluginVoteKickEventArgs( byte sessionId ) : EventArgs
{
    public byte SessionId { get; set; } = sessionId;
    public bool Handled { get; set; } = false;
}
