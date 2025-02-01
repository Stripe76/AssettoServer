using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using AssettoServer.Server;
using AssettoServer.Network.Tcp;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Udp;
using AssettoServer.Shared.Network.Packets;
using System.Xml.Linq;
using System.Numerics;
using AssettoServer.Shared.Model;

namespace VirtualSteward;

public class VSBot
{
    public bool IsActive = false;
    public int SessionId = -1;
    public int CarIndex = -1;
    public byte PakSequenceId = 0;
    public int Frame = -1;

    public bool Loop = true;

    public int FrameOffset = 0;
    public int FrameStart = -1;
    public int LoopStart = -1;
    public int LoopEnd = -1;

    public long TimeStampStart = 0;
    public float SteeringRatio = 14;

    public VCar? Car = null;
    public ACTcpClient? Client = null;

    public void Reset( )
    {
        IsActive = false;
        Frame = -1;
        TimeStampStart = -1;
        Client = null;
    }
}
public class VSBotList : List<VSBot> 
{
}

public class VSTarget
{
    public bool IsActive = false;
    public int CurrentTarget = 0;

    public byte PakSequenceId = 0;

    public ACTcpClient? Client = null;

    public VCar? ReplayCar = null;
    public List<VSCarStatusPair> Cars = [];
    public VSTargetPositionList Targets = [];
}
public class VSTargetList : List<VSTarget>
{
}
public class VSTargetPosition
{
    public int Frame;

    public Vector3 Position;
    public Vector3 Rotation;

    public float NormalizedPosition;
}
public class VSTargetPositionList : List<VSTargetPosition>
{ 
}

public class VSCarStatusPair( EntryCar car,CarStatus status ) 
{
    public EntryCar Car = car;
    public CarStatus Status = status;
}

public class VSReplayPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly VSReplayConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACUdpServer _udpServer;

    private readonly VSBotList _bots = [];
    private readonly VSTargetList _targetBots = [];
    private readonly List<EntryCar> _targetCars = [];
    private readonly VSTargetPositionList _targetPositions = [];

    private static ThreadLocal<byte[]> UdpSendBuffer { get; } = new( ( ) => GC.AllocateArray<byte>( 1500,true ) );

    private VReplay? _replay = null;

    public VSReplayPlugin( ACUdpServer udpServer,SessionManager sessionManager,VSReplayConfiguration configuration,ACServerConfiguration serverConfiguration,EntryCarManager entryCarManager,IHostApplicationLifetime applicationLifetime ) : base( applicationLifetime )
    {
        _udpServer = udpServer;
        _sessionManager = sessionManager;
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientDisconnected += EntryCarManager_ClientDisconnected;
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
                    bot.Client = context.Client;
                    bot.Loop = true;
                    bot.Frame = bot.FrameOffset = (startMoving) ? bot.FrameStart + 1 : bot.FrameStart;

                    if( startMoving )
                        context.Broadcast( "SC exiting" );
                    else
                        context.Broadcast( "Bot activated - Get close to it to make it start" );
                }
                else
                {
                    context.Broadcast( "No bots available" );
                }
            }
            else
            {
                bot.Frame = bot.FrameOffset = (startMoving) ? bot.FrameStart + 1 : bot.FrameStart;
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

    internal void ClientCreateTargets( ChatCommandContext context )
    {
        ACTcpClient client = context.Client;
        if( client != null && _replay != null )
        {
            VSTarget target = new ( )
            {
                IsActive = true,
                Client = client,
                ReplayCar = _replay.Cars[0],
                Cars = [],
                Targets = _targetPositions,
            };
            foreach( EntryCar car in _targetCars )
            {
                target.Cars.Add( new VSCarStatusPair( car,new CarStatus( ) ) );
            }
            _targetBots.Add( target );

            client.Collision -= Client_Collision;
            client.Collision += Client_Collision;
        }
    }

    private void Setup( )
    {
        _bots.Clear( );

        if( _entryCarManager != null && _replay != null && _replay.Cars.Count > 0 )
        {
            VLap? lap = null;
            VCar vsCar = _replay.Cars[0];
            if( vsCar != null && vsCar.Laps.Count > 0 )
            {
                int n = _configuration.LoopLap;
                if( n >= 0 && n < vsCar.Laps.Count )
                    lap = vsCar.Laps[n];
                if( n < 0 && vsCar.Laps.Count-n >= 0 )
                    lap = vsCar.Laps[vsCar.Laps.Count + n];
#if DEBUG
                if( lap != null )
                {
                    int targetNumber = 20;
                    int distance = (lap.nFinish - lap.nStart) / targetNumber;

                    _targetPositions.Clear( );
                    for( int i = 0; i < targetNumber; i++ )
                    {
                        VSTargetPosition pos = new ()
                        { 
                            Frame = lap.nStart + i * distance,
                        };
                        _targetPositions.Add( pos );
                    }
                }
#endif
            }
            int autoStart = _configuration.AutoStart;

            for( int i = 0; i < _entryCarManager.EntryCars.Length; i++ )
            {
                var car = _entryCarManager.EntryCars[i];
                if( car != null )
                {
                    if( car.Skin.EndsWith( "/VS" ) )
                    {
                        VSBot bot = new()
                        {
                            SessionId = car.SessionId,
                            CarIndex = i,
                            FrameStart = _configuration.StartFrame,
                            Car = vsCar,
                        };
                        if( lap != null )
                        {
                            bot.LoopStart = lap.nStart;
                            bot.LoopEnd = lap.nFinish;
                        }
                        if( _configuration.LoopStart > 0 )
                            bot.LoopStart = _configuration.LoopStart;
                        if( _configuration.LoopEnd > 0 )
                            bot.LoopEnd = _configuration.LoopEnd;

                        if( autoStart-- > 0 )
                        {
                            bot.IsActive = true;
                            bot.Loop = true;
                            bot.Frame = bot.LoopStart + (( bot.LoopEnd - bot.LoopStart ) / _configuration.AutoStart) * autoStart;
                        }
                        _bots.Add( bot );
                    }
                    else if( car.Skin.EndsWith( "/TG" ) )
                    {
                        car.GetPluginStatus += Car_GetPluginStatus;

                        _targetCars.Add( car );
                    }
                    else
                    {
                        car.PositionUpdateReceived += Car_PositionUpdateReceived;
                    }
                }
            }
        }
    }

    private bool LoadReplay( string fileName )
    {
        if( File.Exists( fileName ) )
        {
            VStewardAC vStewardAC = new ( );
            if( vStewardAC != null )
            {
                Log.Information( "{plugin}: loading replay: {fileName}","VS Plugin",fileName );

                LoadReplayOptions loadOptions = new ( );
                VReplay? replay = vStewardAC.LoadReplay( fileName,ref loadOptions );

                if( replay != null )
                {
                    if( replay.Cars != null && replay.Cars.Count > 0 )
                    {
                        Log.Information( "{plugin}: replay file loaded","VS Plugin" );

                        _replay = replay;

                        Log.Information( "{plugin}: cars setup","VS Plugin" );

                        Setup( );

                        return true;
                    }
                    else
                    {
                        Log.Error( "{plugin}: error loading file: {result}","VS Plugin","No cars in replay file" );
                    }
                }
                else
                {
                    Log.Error( "{plugin}: error loading file: {result}","VS Plugin",loadOptions.Result );
                }
            }
            else
            {
                Log.Error( "{plugin}: error creating library","VS Plugin" );
            }
        }
        else
        {
            Log.Error( "{plugin}: replay file not found: {fileName}","VS Plugin",fileName );
        }
        return false;
    }

    private void Update( )
    {
        if( _entryCarManager != null && _replay != null && _bots != null )
        {
            UpdateBots( );
            UpdateTargets( );
        }
    }

    private void UpdateBots( )
    {
        for( int i = 0; i < _bots.Count; i++ )
        {
            VSBot bot = _bots[i];
            if( bot != null && bot.IsActive && bot.Car != null )
            {
                VCar vsCar = bot.Car;
                if( bot.TimeStampStart < 0 )
                    bot.TimeStampStart = _sessionManager.ServerTimeMilliseconds;

                if( bot.Frame > bot.FrameStart )
                    bot.Frame++;

                if( bot.Loop && bot.LoopEnd > 0 && bot.Frame > bot.LoopEnd )
                {
                    bot.Frame = bot.FrameOffset = bot.LoopStart;
                    bot.TimeStampStart = _sessionManager.ServerTimeMilliseconds;
                }
                if( bot.Frame >= vsCar.Positions.Count )
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
                var car = _entryCarManager.EntryCars[bot.CarIndex];
                if( car != null )
                {
                    VCarPos carPos = vsCar.GetCarPos( bot.Frame );
                    if( carPos != null )
                    {
                        float fSteeringRatio = bot.SteeringRatio;

                        int nSteerAngle = (int)((carPos.fSteeringWheel / 270.0F) * 254);
                        if( nSteerAngle > 127 )
                            nSteerAngle = 127;
                        if( nSteerAngle < -127 )
                            nSteerAngle = -127;

                        byte asFL = 100,asFR = 100, asRL = 100, asRR = 100;
                        if( !float.IsNaN( carPos.fFLAngular ) )
                            asFL = (byte)(Math.Clamp( MathF.Round( MathF.Log10( carPos.fFLAngular + 1.0f ) * 20.0f ) * Math.Sign( carPos.fFLAngular ),-100.0f,154.0f ) + 100.0f);
                        if( !float.IsNaN( carPos.fFRAngular ) )
                            asFR = (byte)(Math.Clamp( MathF.Round( MathF.Log10( carPos.fFRAngular + 1.0f ) * 20.0f ) * Math.Sign( carPos.fFRAngular ),-100.0f,154.0f ) + 100.0f);
                        if( !float.IsNaN( carPos.fRLAngular ) )
                            asRL = (byte)(Math.Clamp( MathF.Round( MathF.Log10( carPos.fRLAngular + 1.0f ) * 20.0f ) * Math.Sign( carPos.fRLAngular ),-100.0f,154.0f ) + 100.0f);
                        if( !float.IsNaN( carPos.fRRAngular ) )
                            asRR = (byte)(Math.Clamp( MathF.Round( MathF.Log10( carPos.fRRAngular + 1.0f ) * 20.0f ) * Math.Sign( carPos.fRRAngular ),-100.0f,154.0f ) + 100.0f);

                        //car.Status.Timestamp = (int)(bot.TimeStampStart + (bot.Frame - bot.FrameOffset) * _replay.ReplayFrequency);
                        car.Status.Timestamp = _sessionManager.ServerTimeMilliseconds;

                        car.Status.PakSequenceId = bot.PakSequenceId++;
                        //car.Status.PakSequenceId = (byte)bot.Frame;
                        car.Status.Position = new System.Numerics.Vector3( (float)carPos.xBody,(float)carPos.zBody,(float)carPos.yBody );
                        car.Status.Rotation = new System.Numerics.Vector3( (float)carPos.aBodyX,(float)carPos.aBodyY,(float)carPos.aBodyZ );
                        if( bot.Frame > bot.FrameStart )
                            car.Status.Velocity = new System.Numerics.Vector3( carPos.fXVelocity,carPos.fZVelocity,carPos.fYVelocity );
                        else
                            car.Status.Velocity = new System.Numerics.Vector3( 0,0,0 );
                        car.Status.TyreAngularSpeed[0] = asFL;
                        car.Status.TyreAngularSpeed[1] = asFR;
                        car.Status.TyreAngularSpeed[2] = asRL;
                        car.Status.TyreAngularSpeed[3] = asRR;
                        car.Status.SteerAngle = (byte)nSteerAngle;
                        car.Status.WheelAngle = (byte)(127 + (((carPos.fSteeringWheel / 360.0F) * 254) / (fSteeringRatio / 4)));
                        car.Status.EngineRpm = (ushort)carPos.fRPM;
                        car.Status.Gear = (byte)carPos.nGear;
                        car.Status.StatusFlag = (CarStatusFlags)0;
                        car.Status.PerformanceDelta = 0;
                        car.Status.Gas = (byte)carPos.nGasPedal;
                        car.Status.NormalizedPosition = 0;

                        car.HasUpdateToSend = true;
                        //SendUpdate( car );
                    }
                }
            }
        }
    }
    private void UpdateTargets( )
    {
        for( int i = 0; i < _targetBots.Count; i++ )
        {
            VSTarget target = _targetBots[i];
            if( target != null && target.IsActive && target.ReplayCar != null )
            {
                for( int j = 0; j < target.Cars.Count; j++ )
                {
                    EntryCar car = target.Cars[j].Car;
                    if( car != null )
                    {
                        int targetNumer = target.CurrentTarget+j;
                        if( targetNumer >= target.Targets.Count )
                            targetNumer -= target.Targets.Count;

                        VCar vsCar = target.ReplayCar;
                        VCarPos carPos = vsCar.GetCarPos( target.Targets[targetNumer].Frame );
                        if( carPos != null )
                        {
                            CarStatus status = target.Cars[j].Status;
                            status.Timestamp = _sessionManager.ServerTimeMilliseconds;

                            status.PakSequenceId = target.PakSequenceId;
                            status.Position = new System.Numerics.Vector3( (float)carPos.xBody,(float)carPos.zBody,(float)carPos.yBody );
                            status.Rotation = new System.Numerics.Vector3( (float)carPos.aBodyX,(float)carPos.aBodyY,(float)carPos.aBodyZ );
                            status.Velocity = new System.Numerics.Vector3( 0,0,0 );
                            status.TyreAngularSpeed[0] = 100;
                            status.TyreAngularSpeed[1] = 100;
                            status.TyreAngularSpeed[2] = 100;
                            status.TyreAngularSpeed[3] = 100;
                            status.SteerAngle = 127;
                            status.WheelAngle = 127;
                            status.EngineRpm = (ushort)carPos.fRPM;
                            status.Gear = (byte)carPos.nGear;
                            status.StatusFlag = (CarStatusFlags)0;
                            status.PerformanceDelta = 0;
                            status.Gas = (byte)carPos.nGasPedal;
                            status.NormalizedPosition = 0;

                            //target.PakSequenceId++;

                            car.HasUpdateToSend = true;
                        }
                    }
                }
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

    private async Task LoadReplayAsync( CancellationToken stoppingToken )
    {
        await Task.Run( ( ) => { LoadReplay( "replay.acreplay" ); },stoppingToken );
    }
    private async Task UpdateAsync( CancellationToken stoppingToken )
    {
        if( _replay != null )
        {
            Log.Information( "{plugin}: starting update loop","VS Plugin" );

            using var timer = new PeriodicTimer( TimeSpan.FromMilliseconds( _replay.ReplayFrequency ) );

            while( await timer.WaitForNextTickAsync( stoppingToken ) )
            {
                try
                {
                    Update( );
                }
                catch( Exception ex )
                {
                    Log.Error( ex,"Error in VS update" );
                }
            }
        }
        else
        {
            Log.Error( "Replay file not loaded" );
        }
    }

    protected override Task ExecuteAsync( CancellationToken stoppingToken )
    {
        if( LoadReplay( "replay.acreplay" ) )
        {
            _ = UpdateAsync( stoppingToken );
        }
        return Task.CompletedTask;
    }

    private void Client_Collision( ACTcpClient client,CollisionEventArgs args )
    {
        VSTarget? target = _targetBots.FirstOrDefault( x => x.Client == client );
        if( target != null )
        {
            EntryCar? collideCar = args.TargetCar;
            if( collideCar != null )
            {
                EntryCar? car = target.Cars.FirstOrDefault( x => x.Car == collideCar )?.Car;
                if( car != null )
                {
                    target.CurrentTarget++;

                    if( target.CurrentTarget >= target.Targets.Count )
                        target.CurrentTarget = 0;

                    SendMessage( client,"Box hit" );
                }
            }
        }
    }

    private void Car_GetPluginStatus( EntryCar sender,GetPluginStatusEventArgs args )
    {
        VSTarget? target = _targetBots.FirstOrDefault( x => x.Client == args.Target.Client );
        if( target != null )
        {
            foreach( VSCarStatusPair car in target.Cars )
            {
                if( car.Car == sender )
                {
                    args.Status = car.Status;
                    break;
                }
            }
        }
    }
    private void Car_PositionUpdateReceived( EntryCar sender,in AssettoServer.Shared.Network.Packets.Incoming.PositionUpdateIn args )
    {
        ACTcpClient? client = sender.Client;
        if( client != null )
        {
            VSBot? bot = _bots.FirstOrDefault( x => x.Client == client );
            if( bot != null )
            {
                if( bot.Frame <= bot.FrameStart && sender.IsInRange( _entryCarManager.EntryCars[bot.CarIndex],6 ) )
                {
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
}
