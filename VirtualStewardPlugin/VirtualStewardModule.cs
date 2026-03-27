using Autofac;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.Hosting;

namespace VirtualStewardPlugin;

public class VirtualStewardModule : AssettoServerModule<VirtualStewardConfiguration>
{
    /*
    private readonly ACServerConfiguration _configuration;

    public VirtualStewardModule( ACServerConfiguration configuration )
    {
        _configuration = configuration;
    }
    */

    protected override void Load( ContainerBuilder builder )
    {
        builder.RegisterType<VirtualStewardPlugin>( ).AsSelf( ).As<IHostedService>( ).SingleInstance( );
    }
}
