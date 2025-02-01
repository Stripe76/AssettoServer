using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using AssettoServer.Server;
using AssettoServer.Network.Tcp;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Udp;
using System.Numerics;
using AssettoServer.Shared.Model;
using System.Text;

namespace VirtualSteward;

public class VirtualStewardPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly VirtualStewardConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;

    public VirtualStewardPlugin( SessionManager sessionManager,VirtualStewardConfiguration configuration,ACServerConfiguration serverConfiguration,EntryCarManager entryCarManager,IHostApplicationLifetime applicationLifetime ) : base( applicationLifetime )
    {
        _sessionManager = sessionManager;
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientConnected += EntryCarManager_ClientConnected;
        _sessionManager.SessionChanged += SessionManager_SessionChanged;
    }

    private void SendMessage( EntryCar car,string message )
    {
        SendMessage( car.Client,message );
    }
    private void SendMessage( ACTcpClient? tcpClient,string message )
    {
        tcpClient?.SendPacket( new ChatMessage { SessionId = 255,Message = message } );
    }

    protected override Task ExecuteAsync( CancellationToken stoppingToken )
    {
        return Task.CompletedTask;
    }

    private void SessionManager_SessionChanged( SessionManager sender,SessionChangedEventArgs args )
    {
        SessionState session = args.NextSession;
        if( session.Configuration.Type == SessionType.Qualifying )
        {
            foreach( var car in _entryCarManager.EntryCars )
            {
                car.SetHiddenToAllCars( true );
            }
        }
        else if( session.Configuration.Type == SessionType.Race )
        {
            foreach( var car in _entryCarManager.EntryCars )
                car.SetHiddenToAllCars( false );

            SessionState? qualy = args.PreviousSession;
            if( qualy != null && qualy.Configuration.Type == SessionType.Qualifying && qualy.Results != null)
            {
                uint maxTime = _configuration.RaceMaxLaptime;

                if( _configuration.RacePolePercentage > 0 )
                {
                    // TODO
                }
                if( maxTime > 0 )
                {
                    StringBuilder message = new ( );
                    var valids = qualy.Results
                        .Where(result => result.Value.BestLap < maxTime)
                        .Select(result => _entryCarManager.EntryCars[result.Key])
                        .ToList();

                    var toHides = qualy.Results
                        .Where(result => result.Value.BestLap > maxTime || result.Value.BestLap == 0 )
                        .Select(result => _entryCarManager.EntryCars[result.Key])
                        .ToList();

                    foreach( var entryCar in toHides )
                    {
                        if( entryCar.Client != null && entryCar.Client.IsConnected )
                        {
                            foreach( var target in valids )
                                entryCar.SetHiddenToCar( target,true );
                            message.AppendLine( $"Player {entryCar.Client.Name} is hidden" );
                        }
                    }
                    if( message.Length > 0 )
                    {
                        _entryCarManager.BroadcastPacket( new ChatMessage { SessionId = 255,Message = message.ToString( ) } );
                    }
                }
            }
        }
    }

    private void EntryCarManager_ClientConnected( ACTcpClient sender,EventArgs args )
    {
        sender.VoteKickUser += Client_VoteKickUser;

        if( sender.EntryCar != null )
        {
            sender.EntryCar.GetPluginStatus += EntryCar_GetPluginStatus;
            sender.EntryCar.PositionUpdateReceived += EntryCar_PositionUpdateReceived;

            sender.EntryCar.StorePitStatus = true;

            if( _sessionManager.CurrentSession.Configuration.Type != SessionType.Practice )
            {
                sender.EntryCar.SetHiddenToAllCars( true );
            }
        }
    }

    private void Client_VoteKickUser( ACTcpClient sender,PluginVoteKickEventArgs args )
    {
        SessionType sessionType = _sessionManager.CurrentSession.Configuration.Type;

        if( (_configuration.KickHideOnPractice && sessionType == SessionType.Practice) ||
            (_configuration.KickHideOnQualify && sessionType == SessionType.Qualifying) ||
            (_configuration.KickHideOnRace && sessionType == SessionType.Race) )
        {
            args.Handled = true;

            foreach( var target in _entryCarManager.EntryCars )
            {
                if( target.SessionId == args.SessionId )
                {
                    bool hidden = !target.IsHiddenToCar( sender.EntryCar );

                    target.SetHiddenToCar( sender.EntryCar,hidden );
                    if( _configuration.MutualKickHide )
                        sender.EntryCar.SetHiddenToCar( target,hidden );
                    break;
                }
            }
        }
    }

    private void EntryCar_GetPluginStatus( EntryCar sender,GetPluginStatusEventArgs args )
    {
        if( sender.IsHiddenToCar( args.Target ) && sender.PitStatus != null )
        {
            sender.PitStatus.Timestamp = sender.Status.Timestamp;
            sender.PitStatus.PakSequenceId = sender.Status.PakSequenceId;

            args.Status = sender.PitStatus;
        }
    }
    private void EntryCar_PositionUpdateReceived( EntryCar sender,in AssettoServer.Shared.Network.Packets.Incoming.PositionUpdateIn args )
    {
        if( sender.StorePitStatus )
        {
            if( sender.Status.Gas > 0 && sender.PitStatus != null )
            {
                sender.StorePitStatus = false;
            }
            else
            {
                CarStatus status = sender.PitStatus ??= new( );

                status.Position = sender.Status.Position;
                status.Rotation = sender.Status.Rotation;
                status.Velocity = sender.Status.Velocity;
                status.TyreAngularSpeed[0] = sender.Status.TyreAngularSpeed[0];
                status.TyreAngularSpeed[1] = sender.Status.TyreAngularSpeed[1];
                status.TyreAngularSpeed[2] = sender.Status.TyreAngularSpeed[2];
                status.TyreAngularSpeed[3] = sender.Status.TyreAngularSpeed[3];
                status.SteerAngle = sender.Status.SteerAngle;
                status.WheelAngle = sender.Status.WheelAngle;
                status.EngineRpm = sender.Status.EngineRpm;
                status.Gear = sender.Status.Gear;
                status.StatusFlag = sender.Status.StatusFlag;
                status.PerformanceDelta = sender.Status.PerformanceDelta;
                status.Gas = sender.Status.Gas;
                status.NormalizedPosition = sender.Status.NormalizedPosition;
            }
        }
    }
}
