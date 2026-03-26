using ACLibrary.Data;
using ACLibrary.Replays;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using Framework.IniFiles;
using Microsoft.Extensions.Hosting;
using Serilog;
using VirtualSteward;
using VirtualSteward.Bots;

namespace VSReplayPlugin;

public class VSReplayPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly VSReplayConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;

    private readonly VSBotList _bots = [];
    private readonly SortedList<string,Replay> _replays = [];

    public VSReplayPlugin( ACUdpServer udpServer,
        SessionManager sessionManager,
        VSReplayConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        EntryCarManager entryCarManager,
        IHostApplicationLifetime applicationLifetime ) : base( applicationLifetime )
    {
        _sessionManager = sessionManager;
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientDisconnected += EntryCarManager_ClientDisconnected;
    }

    protected override Task ExecuteAsync( CancellationToken stoppingToken )
    {
        IniFile botsSettings = new IniFile( Path.Combine( _serverConfiguration.BaseFolder,"plugin_vs_replay_cfg.ini" ) );
        if( Setup( botsSettings ) )
        {
            _ = UpdateAsync( stoppingToken );
        }
        return Task.CompletedTask;
    }

    private async Task UpdateAsync( CancellationToken stoppingToken )
    {
        if( _replays.Values.Count > 0 )
        {
            Log.Information( "{plugin}: starting update loop","VS Plugin" );

            using var timer = new PeriodicTimer( TimeSpan.FromMilliseconds( _replays.Values[0].ReplayFrequency ) );

            while( await timer.WaitForNextTickAsync( stoppingToken ) )
            {
                try
                {
                    UpdateBots( );
                }
                catch( Exception ex )
                {
                    Log.Error( "{plugin}: Error in VS update","VS Plugin" );
                }
            }
        }
        else
        {
            Log.Error( "{plugin}: Replay file not loaded","VS Plugin" );
        }
    }

    private bool Setup( IniFile botsSettings )
    {
        _bots.Clear( );

        int autoStartCount = 0;
        for( int i = 0; i < 24; i++ )
        {
            string section = $"CAR_{i}";
            string? playerName = botsSettings.GetValue( "PlayerName",section )?.ToLower( );
            if( playerName == null )
                continue;

            Replay? replay = GetReplay( botsSettings.GetValue( "ReplayFile",section ) ?? _configuration.ReplayFile );
            if( replay != null )
            {
                ReplayCar? replayCar = replay.Cars.FirstOrDefault( botCar => botCar.PlayerName.ToLower( ).Equals( playerName ) );
                if( replayCar != null )
                {
                    var serverCar = _entryCarManager.EntryCars[i];

                    VSBot bot = new( )
                    {
                        SessionId = serverCar.SessionId,
                        Car = replayCar,
                        CarIndex = i,
                        FrameStart = botsSettings.GetIntValue( "StartFrame",section,_configuration.StartFrame ),
                    };
                    PlayerLap? lap = null;
                    PlayerLapList playerLaps = CreateLapsList( replayCar );

                    if( playerLaps.Count > 0 )
                    {
                        int n = botsSettings.GetIntValue( "LoopLap",section,_configuration.LoopLap );
                        if( n >= 0 && n < playerLaps.Count )
                            lap = playerLaps[n];
                    }
                    if( lap != null )
                    {
                        bot.LoopStart = (int)lap.StartFrame;
                        bot.LoopEnd = (int)lap.EndFrame;
                    }
                    int loopStart = botsSettings.GetIntValue( "LoopStart",section,_configuration.LoopStart ); 
                    int loopEnd = botsSettings.GetIntValue( "LoopEnd",section,_configuration.LoopEnd );
                    if( loopStart > 0 )
                        bot.LoopStart = loopStart;
                    if( loopEnd > 0 )
                        bot.LoopEnd = loopEnd;
                    
                    if( botsSettings.GetBoolValue( "AutoStart",section,_configuration.AutoStart ) )
                    {
                        bot.IsActive = true;
                        bot.Loop = true;

                        int loopOffset = botsSettings.GetIntValue( "AutoStartOffset",section,_configuration.AutoStartOffset );
                        if( loopOffset == 0 )
                            autoStartCount++;
                        bot.Frame = bot.LoopStart + loopOffset;
                    }
                    serverCar.AllowedGuids.Add( 87470088877857164 );
                    serverCar.PositionUpdateReceived += Car_PositionUpdateReceived;
                    
                    _bots.Add( bot );
                }
                else
                {
                    Log.Error( "{plugin}: player '{player}' not found in '{replay}'","VS Plugin",playerName,replay.FileFullPath );
                }
            }
        }
        int p = 0;
        foreach( var bot in _bots )
        {
            if( bot.IsActive && bot.Frame == bot.LoopStart )
                bot.Frame = bot.LoopStart + (( bot.LoopEnd - bot.LoopStart ) / autoStartCount) * p++;
        }
        foreach( var serverCar in _entryCarManager.EntryCars )
        {
            if( !serverCar.AllowedGuids.Contains( 87470088877857164 ) )
            {
                serverCar.PositionUpdateReceived += Car_PositionUpdateReceived;
            }
        }
        return true;
    }

    private Replay? GetReplay( string replayFile )
    {
        if( _replays.TryGetValue( replayFile,out var replay ) )
            return replay;
        
        replay = LoadReplay( replayFile );
        if( replay != null )
        {
            _replays.Add( replayFile,replay );

            return replay;
        }
        return null;
    }
    private Replay? LoadReplay( string fileName )
    {
        if( File.Exists( fileName ) )
        {
            Log.Information( "{plugin}: loading replay: {fileName}","VS Plugin",fileName );

            Replay? replay = Replay.LoadReplay( fileName,null );
            if( replay != null )
            {
                if( replay.Cars.Length > 0 )
                {
                    Log.Information( "{plugin}: replay file loaded","VS Plugin" );

                    return replay;
                }
                else
                {
                    Log.Error( "{plugin}: error loading file: {result}","VS Plugin","No cars in replay file" );
                }
            }
            else
            {
                Log.Error( "{plugin}: error loading file: {result}","VS Plugin",fileName );
            }
        }
        else
        {
            Log.Error( "{plugin}: replay file not found: {fileName}","VS Plugin",fileName );
        }
        return null;
    }

    private void UpdateBots( )
    {
        for( int i = 0; i < _bots.Count; i++ )
        {
            VSBot bot = _bots[i];
            if( !bot.IsActive && bot.ServerTimeStart > 0 && bot.ServerTimeStart < _sessionManager.ServerTimeMilliseconds )
                bot.IsActive = true;

            if( bot.Car != null )
            {
                if( bot.IsActive )
                {
                    ReplayCar replayCar = bot.Car;
                    if( bot.TimeStampStart < 0 )
                        bot.TimeStampStart = _sessionManager.ServerTimeMilliseconds;

                    if( !bot.IsWaiting )
                        bot.Frame++;

                    if( bot.Loop && bot.LoopEnd > 0 && bot.Frame > bot.LoopEnd )
                    {
                        bot.Frame = bot.FrameOffset = bot.LoopStart;
                        bot.TimeStampStart = _sessionManager.ServerTimeMilliseconds;
                    }
                    if( bot.Frame >= replayCar.Data.Length )
                    {
                        if( bot.Loop )
                        {
                            bot.Frame = bot.FrameOffset = bot.FrameStart;
                            bot.TimeStampStart = _sessionManager.ServerTimeMilliseconds;
                        }
                        else
                        {
                            bot.Reset( );
                        }
                    }
                    var serverCar = _entryCarManager.EntryCars[bot.CarIndex];
                    if( bot.Frame >= 0 && bot.Frame < replayCar.Data.Length )
                    {
                        ACCarFrame carPos = replayCar.Data[bot.Frame].Frame;

                        float fSteeringRatio = bot.SteeringRatio;

                        int nSteerAngle = (int)(((float)carPos.Steer / 270.0f) * 254);
                        if( nSteerAngle > 127 )
                            nSteerAngle = 127;
                        if( nSteerAngle < -127 )
                            nSteerAngle = -127;

                        byte asFL = GetAngularSpeed( carPos.WheelAngularSpeedFL );
                        byte asFR = GetAngularSpeed( carPos.WheelAngularSpeedFR );
                        byte asRL = GetAngularSpeed( carPos.WheelAngularSpeedRL );
                        byte asRR = GetAngularSpeed( carPos.WheelAngularSpeedRR );

                        //car.Status.Timestamp = (int)(bot.TimeStampStart + (bot.Frame - bot.FrameOffset) * _replay.ReplayFrequency);
                        serverCar.Status.Timestamp = _sessionManager.ServerTimeMilliseconds;

                        bot.PakSequenceId++;

                        //if( bot.PakSequenceId == 0 )
                        //bot.PakSequenceId = 1;

                        serverCar.Status.PakSequenceId = bot.PakSequenceId;

                        //car.Status.PakSequenceId = (byte)bot.Frame;
                        serverCar.Status.Position = new System.Numerics.Vector3( carPos.BodyTranslation.X,carPos.BodyTranslation.Y,carPos.BodyTranslation.Z );
                        serverCar.Status.Rotation = new System.Numerics.Vector3( (float)carPos.BodyOrientation.X,(float)carPos.BodyOrientation.Y,(float)carPos.BodyOrientation.Z );

                        if( bot.Frame > bot.FrameStart )
                        {
                            if( _configuration.RecalcVelocities && bot.Frame > 0 )
                            {
                                // TODO
                                /*
                                VCarPos prevPos = vsCar.GetCarPos( bot.Frame-1 );
                                if( prevPos != null )
                                {
                                    float X = (float)(((carPos.xBody - prevPos.xBody) / _replay.ReplayFrequency) * 1000f);
                                    float Y = (float)(((carPos.yBody - prevPos.yBody) / _replay.ReplayFrequency) * 1000f);
                                    float Z = (float)(((carPos.zBody - prevPos.zBody) / _replay.ReplayFrequency) * 1000f);

                                    car.Status.Velocity = new System.Numerics.Vector3( X,Z,Y );
                                }
                                */
                            }
                            else
                            {
                                serverCar.Status.Velocity = new System.Numerics.Vector3( (float)carPos.Velocity.X,(float)carPos.Velocity.Y,(float)carPos.Velocity.Z );
                            }
                        }
                        else
                        {
                            serverCar.Status.Velocity = new System.Numerics.Vector3( 0,0,0 );
                        }
                        serverCar.Status.TyreAngularSpeed[0] = asFL;
                        serverCar.Status.TyreAngularSpeed[1] = asFR;
                        serverCar.Status.TyreAngularSpeed[2] = asRL;
                        serverCar.Status.TyreAngularSpeed[3] = asRR;
                        serverCar.Status.SteerAngle = (byte)(127 + nSteerAngle);
                        serverCar.Status.WheelAngle = (byte)(127 + ((((float)carPos.Steer / 360.0F) * 254) / (fSteeringRatio / 4)));
                        serverCar.Status.EngineRpm = (ushort)carPos.EngineRpm;
                        serverCar.Status.Gear = (byte)carPos.Gear;
                        serverCar.Status.StatusFlag = (CarStatusFlags)0;
                        serverCar.Status.PerformanceDelta = 0;
                        serverCar.Status.Gas = (byte)carPos.Gas;
                        serverCar.Status.NormalizedPosition = 0;

                        serverCar.HasUpdateToSend = true;
                    }
                }
                else if( bot.ResetCount > 0 )
                {
                    bot.ResetCount--;

                    EntryCar serverCar = _entryCarManager.EntryCars[bot.CarIndex];
                    //if( serverCar != null )
                    {
                        ACCarFrame carPos = bot.Car.Data[bot.Frame].Frame;

                        serverCar.Status.Timestamp = _sessionManager.ServerTimeMilliseconds;
                        serverCar.Status.PakSequenceId = bot.PakSequenceId++;

                        serverCar.Status.Position = new System.Numerics.Vector3( carPos.BodyTranslation.X,carPos.BodyTranslation.Y,carPos.BodyTranslation.Z );
                        serverCar.Status.Rotation = new System.Numerics.Vector3( (float)carPos.BodyOrientation.X,(float)carPos.BodyOrientation.Y,(float)carPos.BodyOrientation.Z );
                        serverCar.Status.Velocity = new System.Numerics.Vector3( 0,0,0 );
                        serverCar.Status.TyreAngularSpeed[0] = 100;
                        serverCar.Status.TyreAngularSpeed[1] = 100;
                        serverCar.Status.TyreAngularSpeed[2] = 100;
                        serverCar.Status.TyreAngularSpeed[3] = 100;
                        serverCar.Status.SteerAngle = 127;
                        serverCar.Status.WheelAngle = 127;
                        serverCar.Status.EngineRpm = 800;
                        serverCar.Status.Gear = 1;
                        serverCar.Status.StatusFlag = 0;
                        serverCar.Status.PerformanceDelta = 0;
                        serverCar.Status.Gas = (byte)0;
                        serverCar.Status.NormalizedPosition = 0;

                        serverCar.HasUpdateToSend = true;
                    }
                }
            }
        }
    }

    internal void ClientStartBot( ChatCommandContext context, bool startMoving = false )
    {
        ACTcpClient client = context.Client;
        if( client != null )
        {
            VSBot? bot = _bots.FirstOrDefault( x => x.Client == client );
            if( bot == null )
            {
                bot = _bots.FirstOrDefault( x => !x.IsActive );
                if( bot != null )
                {
                    bot.IsActive = true;
                    bot.IsWaiting = !startMoving;
                    bot.Client = context.Client;
                    bot.Loop = true;
                    bot.Frame = bot.FrameOffset = bot.FrameStart;
                    bot.PakSequenceId = 0;

                    if( startMoving )
                        context.Broadcast( "SC exiting" );
                    else
                        context.Broadcast( "Bot activated - Get close to make it start" );
                }
                else
                {
                    context.Broadcast( "No bots available" );
                }
            }
            else
            {
                bot.IsWaiting = !startMoving;
                bot.Frame = bot.FrameOffset = bot.FrameStart;
                bot.TimeStampStart = -1;

                context.Broadcast( "Bot reset" );
            }
        }
    }
    internal void ClientStopBot( ChatCommandContext context )
    {
        ACTcpClient? client = context.Client;
        if( client != null )
        {
            VSBot? bot = _bots.FirstOrDefault( x => x.Client == client );
            if( bot != null )
            {
                bot.Reset( );

                context.Broadcast( "Bot sent to garage" );
            }
        }
    }
    internal void ClientEndBot( ChatCommandContext context )
    {
        ACTcpClient? client = context.Client;
        if( client != null )
        {
            VSBot? bot = _bots.FirstOrDefault( x => x.Client == client );
            if( bot != null )
            {
                bot.Loop = false;

                context.Broadcast( "SC entering" );
            }
        }
    }
    
    private void SendMessage( EntryCar car,string message )
    {
        SendMessage( car.Client,message );
    }
    private void SendMessage( ACTcpClient? tcpClient,string message )
    {
        tcpClient?.SendPacket( new ChatMessage { SessionId = 255,Message = message } );
    }

    private PlayerLapList CreateLapsList( ReplayCar replayCar )
    {
        PlayerLapList lapList = [];

        uint currentLap = 0;
        uint frames = (uint)replayCar.Data.Length;
        for( uint i = 1; i < frames; i++ )
        {
            uint lapTime = replayCar.Data[i].Frame.LapTime;
            uint prevLapTime = replayCar.Data[i-1].Frame.LapTime;

            //if( pos != null && last != null )
            {
                if (i == 1 && prevLapTime != 0)
                {
                    lapList.Add( new PlayerLap( currentLap,0,frames ) );
                }
                // Giri e tempi sul giro
                if ((lapTime < prevLapTime) || (lapTime > 0 && prevLapTime == 0))
                {
                    if (lapList.Count > 0)
                    {
                        PlayerLap lastLap = lapList[^1];
                        lastLap.EndFrame = i - 1;
                    }
                    lapList.Add( new PlayerLap( ++currentLap,i,frames ) );
                }
            }
        }
        return lapList;
    }
    
    private void Car_PositionUpdateReceived( EntryCar sender,in AssettoServer.Shared.Network.Packets.Incoming.PositionUpdateIn args )
    {
        ACTcpClient? client = sender.Client;
        if( client != null )
        {
            VSBot? bot = _bots.FirstOrDefault( x => x.Client == client );
            if( bot != null )
            {
                if( bot.IsWaiting && sender.IsInRange( _entryCarManager.EntryCars[bot.CarIndex],6.5f ) )
                {
                    bot.IsWaiting = false;
                    bot.TimeStampStart = -1;
                    bot.Frame = bot.FrameOffset = bot.FrameStart+1;

                    SendMessage( client,"Bot is on the move" );
                }
            }
        }
    }
    private void EntryCarManager_ClientDisconnected( ACTcpClient sender,EventArgs args )
    {
        _bots?.FirstOrDefault( x => x.Client == sender )?.Reset( );
    }

    private byte GetAngularSpeed( Half angularSpeed )
    {
        if( !Half.IsNaN( angularSpeed ) )
            return (byte)(Math.Clamp( MathF.Round( MathF.Log10( (float)angularSpeed + 1.0f ) * 20.0f ) * Math.Sign( (float)angularSpeed ),-100.0f,154.0f ) + 100.0f);
        return 100;
    }
}

public class PlayerLap( uint lapNumber,uint startFrame,uint endFrame ) 
{
    public uint StartFrame = startFrame;
    public uint EndFrame = endFrame;
}

public class PlayerLapList : List<PlayerLap>
{

}
