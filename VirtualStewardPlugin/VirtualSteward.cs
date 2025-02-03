using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using AssettoServer.Server;
using AssettoServer.Network.Tcp;
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
                uint maxTimeTier1 = _configuration.RaceMaxLaptimeTier1;
                uint maxTimeTier2 = _configuration.RaceMaxLaptimeTier2;

                if( _configuration.RacePolePercentage > 0 && session.Grid != null )
                {
                    var pole = session.Grid.First( );
                    if( pole != null )
                    {
                        var entryCar = qualy.Results[pole.SessionId];

                        maxTimeTier1 = (uint)(entryCar.BestLap * (_configuration.RacePolePercentage / 100.0f));

                        if( maxTimeTier1 > maxTimeTier2 )
                            maxTimeTier2 = maxTimeTier1;
                    }
                }
                if( maxTimeTier1 > 0 )
                {
                    if( maxTimeTier2 == 0 )
                        maxTimeTier2 = maxTimeTier1;

                    var tier1 = qualy.Results
                        .Where(result => result.Value.BestLap <= maxTimeTier1)
                        .Select(result => _entryCarManager.EntryCars[result.Key])
                        .ToList();

                    var tier2 = qualy.Results
                        .Where(result => result.Value.BestLap <= maxTimeTier2 && result.Value.BestLap > maxTimeTier1)
                        .Select(result => _entryCarManager.EntryCars[result.Key])
                        .ToList();

                    var tier3 = qualy.Results
                        .Where(result => result.Value.BestLap > maxTimeTier2 || result.Value.BestLap == 0 )
                        .Select(result => _entryCarManager.EntryCars[result.Key])
                        .ToList();

                    StringBuilder message = new ( $"Treshold lap time for tier 1: {TimeFromMilliseconds( maxTimeTier1 )}\r\n" );
                    foreach( var entryCar in tier1 )
                    {
                        if( entryCar.Client != null && entryCar.Client.IsConnected )
                            message.AppendLine( $"Player: {entryCar.Client.Name}" );
                    }
                    if( maxTimeTier2 != 0 )
                    {
                        message.AppendLine( $"Treshold lap time for tier 2: {TimeFromMilliseconds( maxTimeTier2 )}" );
                        foreach( var entryCar in tier2 )
                        {
                            if( entryCar.Client != null && entryCar.Client.IsConnected )
                            {
                                foreach( var target in tier1 )
                                    entryCar.SetHiddenToCar( target,true );
                                message.AppendLine( $"Player {entryCar.Client.Name}" );
                            }
                        }
                    }
                    message.AppendLine( "Tier 3:" );
                    foreach( var entryCar in tier3 )
                    {
                        if( entryCar.Client != null && entryCar.Client.IsConnected )
                        {
                            foreach( var target in tier1 )
                                entryCar.SetHiddenToCar( target,true );
                            foreach( var target in tier2 )
                                entryCar.SetHiddenToCar( target,true );
                            message.AppendLine( $"Player {entryCar.Client.Name}" );
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

    #region Helpers
    public static string TimeFromMilliseconds( uint milliseconds,bool writeMs = true )
    {
        if( milliseconds >= 3600000 )
        {
            if( writeMs )
                return String.Format( "{0:00}:{1:00}:{2:00}:{3:000}",milliseconds / 60000 / 60,milliseconds / 60000,milliseconds / 1000 % 60,milliseconds % 1000 );
            return String.Format( "{0:00}:{1:00}:{2:00}",milliseconds / 60000 / 60,milliseconds / 60000,milliseconds / 1000 % 60,milliseconds % 1000 );
        }
        if( writeMs )
            return String.Format( "{1:00}:{2:00}:{3:000}",milliseconds / 60000 / 60,milliseconds / 60000,milliseconds / 1000 % 60,milliseconds % 1000 );
        return String.Format( "{1:00}:{2:00}",milliseconds / 60000 / 60,milliseconds / 60000,milliseconds / 1000 % 60,milliseconds % 1000 );
    }
    #endregion
}
