using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfStakeSpendingSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;
        private SharedSteps sharedSteps;

        private const decimal OneMillion = 1_000_000;

        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private const string WalletPassword = "123456";
        private const string AccountName = "account 0";

        private NodeBuilder nodeBuilder;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;

        private int totalMinedBlocks;

        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;



        // NOTE: This constructor is allows test steps names to be logged
        public ProofOfStakeSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins()
        {
            this.proofOfStakeSteps.GenerateCoins();
            this.proofOfStakeSteps.WalletTotalAmount().Should().BeGreaterThan(Money.Coins(OneMillion));
        }

        private void a_block_is_mined_creating_spendable_coins()
        {
            this.MineBlocks(this.coinbaseMaturity + 1, this.nodes[SendingWalletName], SendingWalletName, AccountName);
        }

        private void more_blocks_mined_to_just_BEFORE_maturity_of_original_block()
        {
           //this.MineBlocks(this.coinbaseMaturity - 1, this.sendingStratisBitcoinNode);
        }

        private void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
           // this.MineBlocks(this.coinbaseMaturity, this.sendingStratisBitcoinNode);
        }

        private void spending_the_coins_from_original_block()
        {
            //var sendtoAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager()
            //    .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountName), 2).ElementAt(1);

            //try
            //{
            //    this.lastTransaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
            //        .BuildTransaction(SharedSteps.CreateTransactionBuildContext(SendingWalletName, AccountName, WalletPassword, sendtoAddress.ScriptPubKey, Money.COIN * 1, FeeType.Medium, 101));

            //    this.sendingStratisBitcoinNode.FullNode.NodeService<WalletController>()
            //        .SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            //}
            //catch (Exception exception)
            //{
            //    this.caughtException = exception;
            //}
        }

        private void the_transaction_is_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            this.ResetCaughtException();
        }

        private void the_transaction_is_put_in_the_mempool()
        {
            var tx = this.sendingStratisBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        private void MineBlocks(int blockCount, CoreNode node, string walletName, string accoutnName)
        {
            var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accoutnName));
            var wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(WalletPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            WaitForBlockStoreToSync(node);

            this.totalMinedBlocks = this.totalMinedBlocks + blockCount;

            node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(walletName)
                .Sum(s => s.Transaction.Amount)
                .Should().Be(Money.COIN * this.totalMinedBlocks * 50);
        }

        private void ResetCaughtException()
        {
            this.caughtException = null;
        }

        private void WaitForBlockStoreToSync(CoreNode node)
        {
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
        }
    }
}