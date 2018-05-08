using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfStakeSpendingSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;

        private const decimal OneMillion = 1_000_000;

        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private const string WalletPassword = "123456";
        private const string AccountName = "account 0";
        private const string NodeReceiver = "nodereceiver";

        private SharedSteps sharedSteps;
        private NodeBuilder nodeBuilder;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;

        private int totalMinedBlocks;

        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;

        public ProofOfStakeSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper) {}

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.nodeBuilder?.Dispose();
        }

        private void two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins()
        {
            this.proofOfStakeSteps.GenerateCoins();
            this.proofOfStakeSteps.WalletTotalAmount().Should().BeGreaterThan(Money.Coins(OneMillion));
            this.coinbaseMaturity = (int)this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.Network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity;
        }

        private void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
            this.sharedSteps.WaitForNodeToSync(this.proofOfStakeSteps.ProofOfStakeNodeWithCoins);

            //this.sharedSteps.MineBlocks(this.coinbaseMaturity + 1,
            //    this.proofOfStakeSteps.ProofOfStakeNodeWithCoins,
            //    this.proofOfStakeSteps.WalletAccount,
            //    this.proofOfStakeSteps.PosWallet,
            //    this.proofOfStakeSteps.PosWalletPassword);
        }

        private void spending_the_coins_from_original_block()
        {
            this.receivingStratisBitcoinNode = this.proofOfStakeSteps.CreateReceiverNode(NodeReceiver, ReceivingWalletName, WalletPassword);

            //this.sharedSteps.WaitForNodeToSync(this.proofOfStakeSteps.ProofOfStakeNodeWithCoins, this.receivingStratisBitcoinNode);

            var sendtoAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountName), 1).FirstOrDefault();

            //TODO : Get spendable txns from this.proofOfStakeSteps.ProofOfStakeNodeWithCoins and make sure it's sync'd before creating and sending a transaction.

            try
            {
                this.lastTransaction = this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.WalletTransactionHandler()
                    .BuildTransaction(SharedSteps.CreateTransactionBuildContext(
                        this.proofOfStakeSteps.PosWallet,
                        this.proofOfStakeSteps.WalletAccount,
                        this.proofOfStakeSteps.PosWalletPassword,
                        new[] {
                            new Recipient {
                                Amount = Money.COIN * 1,
                                ScriptPubKey = sendtoAddress.ScriptPubKey
                            }
                        },
                        FeeType.Medium, 
                        101));

                this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.NodeService<WalletController>()
                    .SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
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