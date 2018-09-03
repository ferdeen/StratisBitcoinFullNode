using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(
            Network network,
            string sendingWalletName,
            string sendingAccountName,
            string sendingPassword,
            ICollection<Recipient> recipients,
            FeeType feeType,
            int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = new WalletAccountReference(sendingWalletName, sendingAccountName),
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = sendingPassword,
                Recipients = recipients.ToList()
            };
        }
    }
}