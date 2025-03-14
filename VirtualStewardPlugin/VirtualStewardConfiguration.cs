﻿using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VirtualSteward;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class VirtualStewardConfiguration : IValidateConfiguration<VirtualStewardConfigurationValidator>
{
    [YamlMember(Description = "Enables kick hide in practice sessions")]
    public bool KickHideOnPractice { get; init; } = true;
    [YamlMember( Description = "Enables kick hide in qualify sessions" )]
    public bool KickHideOnQualify { get; init; } = false;
    [YamlMember( Description = "Enables kick hide in race sessions" )]
    public bool KickHideOnRace { get; init; } = false;

    [YamlMember( Description = "If enabled a player will become invisible for the players he hides" )]
    public bool MutualKickHide { get; init; } = false;

    [YamlMember( Description = "Set the maximum qualify lap time to make the car visible in tier 1 in ms, zero to disable" )]
    public uint RaceMaxLaptimeTier1 { get; init; } = 0;
    [YamlMember( Description = "Set the maximum qualify lap time to make the car visible in tier 2 in ms, zero to disable" )]
    public uint RaceMaxLaptimeTier2 { get; init; } = 0;
    [YamlMember( Description = "Set the maximum qualify lap time as a percentage of the pole for tier 1, zero to disable" )]
    public uint RacePolePercentage { get; init; } = 0;
}
