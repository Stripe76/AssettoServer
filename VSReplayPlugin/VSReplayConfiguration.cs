using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VirtualSteward;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VSReplayConfiguration : IValidateConfiguration<VSReplayConfigurationValidator>
{
    [YamlMember(Description = "Start frame of the replay, to skip a part")]
    public int StartFrame { get; init; } = 0;

    [YamlMember( Description = "Looping lap number, negative numbers are counted from the end" )]
    public int LoopLap { get; init; } = -2;

    [YamlMember( Description = "Start frame number for the looping part if greater than 0, will override the lap setting" )]
    public int LoopStart { get; init; } = 0;
    [YamlMember( Description = "End frame number for the looping part" )]
    public int LoopEnd { get; init; } = 0;

    [YamlMember( Description = "Number of cars that will auto start and keep looping when the server is launched" )]
    public int AutoStart { get; init; } = 0;

    [YamlMember( Description = "Number of cars that will start in a race session, the reaply file must contains at least those" )]
    public int RaceCars { get; init; } = 0;
}
