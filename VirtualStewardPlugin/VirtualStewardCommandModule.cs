using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Commands.Contexts;
using Qmmands;

namespace VirtualSteward;

public class VirtualStewardCommandModule : ACModuleBase
{
    private readonly VirtualStewardPlugin _vsPlugin;

    public VirtualStewardCommandModule( VirtualStewardPlugin vsPlugin )
    {
        _vsPlugin = vsPlugin;
    }

    [Command( "bot","ghost","g" ), RequireConnectedPlayer]
    public void StartBot( )
    {
        _vsPlugin.ClientStartBot( (ChatCommandContext)Context );
    }
    [Command( "botstop","ghoststop","gs" ), RequireConnectedPlayer]
    public void StopBot( )
    {
        _vsPlugin.ClientStopBot( (ChatCommandContext)Context );
    }
    [Command( "scout" ), RequireConnectedPlayer]
    public void StartSafetyCar( )
    {
        _vsPlugin.ClientStartBot( (ChatCommandContext)Context,true );
    }
    [Command( "scin" ), RequireConnectedPlayer]
    public void EndSafetyCar( )
    {
        _vsPlugin.ClientEndBot( (ChatCommandContext)Context );
    }

    [Command( "t","target" ), RequireConnectedPlayer]
    public void CreateTargets( )
    {
        _vsPlugin.ClientCreateTargets( (ChatCommandContext)Context );
    }
}
