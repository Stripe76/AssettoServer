using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace VSReplayPlugin;

public class VSReplayModule : AssettoServerModule<VSReplayConfiguration>
{
    protected override void Load( ContainerBuilder builder )
    {
        builder.RegisterType<VSReplayPlugin>( ).AsSelf( ).As<IHostedService>( ).SingleInstance( );
    }
}
