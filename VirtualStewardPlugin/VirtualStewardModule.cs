using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using Autofac;

namespace VirtualSteward;

public class VirtualStewardModule : AssettoServerModule<VirtualStewardConfiguration>
{
    //private readonly ACServerConfiguration _configuration;

    /*
    public VirtualStewardModule( ACServerConfiguration configuration )
    {
        _configuration = configuration;
    }
    */

    protected override void Load( ContainerBuilder builder )
    {
        builder.RegisterType<VirtualStewardPlugin>( ).AsSelf( ).As<IAssettoServerAutostart>( ).SingleInstance( );
        //builder.RegisterType<VirtualStewardUpdater>( ).AsSelf( ).SingleInstance( ).AutoActivate( );
    }
}
