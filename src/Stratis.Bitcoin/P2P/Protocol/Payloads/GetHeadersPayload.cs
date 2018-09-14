using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// Ask block headers that happened since BlockLocator.
    /// </summary>
    [Payload("getheaders")]
    public class GetHeadersPayload : Payload
    {
        private int version;

        public int Version
        {
            get
            {
                return this.version;
            }

            set
            {
                this.version = value;
            }
        }

        private BlockLocator blockLocator;

        public BlockLocator BlockLocator
        {
            get
            {
                return this.blockLocator;
            }

            set
            {
                this.blockLocator = value;
            }
        }

        private uint256 hashStop = uint256.Zero;

        public uint256 HashStop
        {
            get
            {
                return this.hashStop;
            }

            set
            {
                this.hashStop = value;
            }
        }

        public GetHeadersPayload()
        {
            this.version = Networks.ProtocolVersion.Protocol.Id;
        }

        public GetHeadersPayload(BlockLocator locator)
        {
            this.BlockLocator = locator;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.blockLocator);
            stream.ReadWrite(ref this.hashStop);
        }
    }
}
