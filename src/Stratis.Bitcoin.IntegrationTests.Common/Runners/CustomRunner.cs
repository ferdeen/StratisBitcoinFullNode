using System;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class CustomNodeRunner : NodeRunner
    {
        private readonly string agent;
        private readonly Action<IFullNodeBuilder> callback;
        private readonly Network network;
        private readonly int protocolVersion;
        private readonly NodeConfigParameters configParameters;

        public CustomNodeRunner(string dataDir, Action<IFullNodeBuilder> callback, Network network, 
            int protocolVersion = 0, NodeConfigParameters configParameters = null, string agent = "Custom")
            : base(dataDir)
        {
            this.callback = callback;
            this.network = network;
            this.protocolVersion = protocolVersion == 0 ? Networks.ProtocolVersion.Protocol.Id : protocolVersion;
            this.agent = agent;
            this.configParameters = configParameters ?? new NodeConfigParameters();
        }

        public override void BuildNode()
        {
            var argsAsStringArray = this.configParameters.AsConsoleArgArray();
            var settings = new NodeSettings(this.network, this.protocolVersion, this.agent, argsAsStringArray);
            IFullNodeBuilder builder = new FullNodeBuilder().UseNodeSettings(settings);

            this.callback(builder);
            this.FullNode = (FullNode)builder.Build();
        }
    }
}