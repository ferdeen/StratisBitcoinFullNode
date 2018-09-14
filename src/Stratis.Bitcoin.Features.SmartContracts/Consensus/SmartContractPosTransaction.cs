using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>A smart contract proof of stake transaction.</summary>
    public class SmartContractPosTransaction : PosTransaction
    {
        public SmartContractPosTransaction() : base()
        {
        }

        public SmartContractPosTransaction(string hex, int version = 0) : this()
        {
            version = version == 0 ? Bitcoin.Networks.ProtocolVersion.Protocol.Id : version;

            this.FromBytes(Encoders.Hex.DecodeData(hex), (ProtocolVersion)(uint)version);
        }

        public SmartContractPosTransaction(byte[] bytes) : this()
        {
            this.FromBytes(bytes);
        }
    }
}