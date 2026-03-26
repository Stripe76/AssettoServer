using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VirtualSteward;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VSReplayConfiguration : IValidateConfiguration<VSReplayConfigurationValidator>
{
    [YamlMember( Description = "Replay file name, must be placed in AssettoServer folder" )]
    public string ReplayFile { get; init; } = "replay.acreplay";

    [YamlMember(Description = "Start frame of the replay, to skip a part")]
    public int StartFrame { get; init; } = 0;

    [YamlMember( Description = "Looping lap number, negative numbers are counted from the end" )]
    public int LoopLap { get; init; } = 1;

    [YamlMember( Description = "Start frame number for the looping part if greater than 0, will override the lap setting" )]
    public int LoopStart { get; init; } = 0;
    [YamlMember( Description = "End frame number for the looping part" )]
    public int LoopEnd { get; init; } = 0;

    [YamlMember( Description = "If set here every bot will autostart unless overidden in bots settings" )]
    public bool AutoStart { get; init; } = false;
    [YamlMember( Description = "If greater than 0 cars will be this frames one from the other instead of evenly ditributed" )]
    public int AutoStartOffset { get; init; } = 0;

    [YamlMember( Description = "Enable if using an online recorded replay and cars are stuttering/shaking" )]
    public bool RecalcVelocities { get; init; } = false;
}
