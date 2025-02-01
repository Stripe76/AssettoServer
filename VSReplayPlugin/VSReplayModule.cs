using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using Autofac;

namespace VirtualSteward;

public class VSReplayModule : AssettoServerModule<VSReplayConfiguration>
{
    protected override void Load( ContainerBuilder builder )
    {
        builder.RegisterType<VSReplayPlugin>( ).AsSelf( ).As<IAssettoServerAutostart>( ).SingleInstance( );
    }
}
