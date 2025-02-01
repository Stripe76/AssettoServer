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

    /*
    [Command( "bot","ghost","g" ), RequireConnectedPlayer]
    public void StartBot( )
    {
        _vsPlugin.ClientStartBot( (ChatCommandContext)Context );
    }
    */
}
