using ACLibrary.Replays;
using AssettoServer.Network.Tcp;

namespace VirtualSteward.Bots;

public class VSBot
{
    public bool IsActive = false;
    public bool IsWaiting = false;
    public bool RecalcVelocities = false;

    public float ReplayFrequency = 0.33f;

    public int SessionId = -1;
    public int CarIndex = -1;
    public byte PakSequenceId = 0;
    public int Frame = -1;
    public int ResetCount = 0;

    public bool Loop = true;

    public int FrameOffset = 0;
    public int FrameStart = -1;
    public int LoopStart = -1;
    public int LoopEnd = -1;

    public long TimeStampStart = 0;
    public long ServerTimeStart = 0;
    public float SteeringRatio = 14;

    public ReplayCar? Car = null;
    public ACTcpClient? Client = null;

    public void Reset( )
    {
        IsActive = false;
        IsWaiting = false;

        Frame = -1;
        ResetCount = 10;
        TimeStampStart = -1;
        Client = null;
    }
}

public class VSBotList : List<VSBot> 
{
}
