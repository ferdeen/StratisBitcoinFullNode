using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.PeerFlooding.Controllers;

namespace Stratis.Bitcoin.Features.PeerFlooding
{
    public class PeerFloodingFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;
        private readonly NodeSettings nodeSettings;
        private readonly ILogger logger;
        private readonly IFullNodeBuilder fullNodeBuilder;

        public PeerFloodingFeature(IFullNodeBuilder fullNodeBuilder, FullNode fullNode, NodeSettings nodeSettings, ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class PeerFloodingFeatureExtension
    {
        public static IFullNodeBuilder UsePeerFlooding(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<PeerFloodingFeature>("PeerFlooding");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PeerFloodingFeature>()
                    .FeatureServices(services => { services.AddSingleton<PeerFloodingController>(); });
            });

            return fullNodeBuilder;
        }
    }
}