using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public partial class ProofOfStakeMintCoinsSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;

        private HdAddress powSenderAddress;
        private Key powSenderPrivateKey;

        private HdAddress posReceiverAddress;
        private Key posReceiverPrivateKey;

        private Transaction lastTransaction;
        private TransactionBuildContext transactionBuildContext;
    
        private const string PowWallet = "powwallet";
        private const string PowWalletPassword = "password";

        private const string PosWallet = "poswallet";
        private const string PosWalletPassword = "password";

        private const string WalletAccount = "account 0";

        private bool initialBlockSignature;
        private bool initialTimeStamp;

        public ProofOfStakeMintCoinsSpecification(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
        }

        protected override void BeforeTest() { }

        protected override void AfterTest() { }

        private void a_proof_of_work_node_with_wallet()
        {
            this.proofOfStakeSteps.ProofOfWorkNodeWithWallet();
        }

        private void it_mines_genesis_and_premine_blocks()
        {
            this.proofOfStakeSteps.MineGenesisAndPremineBlocks();
        }

        private void mine_coins_to_maturity()
        {
            this.proofOfStakeSteps.MineCoinsToMaturity();
        }

        private void a_proof_of_stake_node_with_wallet()
        {
            this.proofOfStakeSteps.ProofOfStakeNodeWithWallet();
        }

        private void it_syncs_with_proof_work_node()
        {
            this.proofOfStakeSteps.SyncWithProofWorkNode();
        }

        private void sends_a_million_coins_from_pow_wallet_to_pos_wallet()
        {
            this.proofOfStakeSteps.SendOneMillionCoinsFromPowWalletToPosWallet();
        }

        private void pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives()
        {
            this.proofOfStakeSteps.PowWalletBroadcastsTransactionOfOneMillionCoinsAndPosWalletReceives();
        }

        private void pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked()
        {
            this.proofOfStakeSteps.PosNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked();
        }

        private void pos_node_starts_staking()
        {
            this.proofOfStakeSteps.PosNodeStartsStaking();
        }

        private void pos_node_wallet_has_earned_coins_through_staking()
        {
            this.proofOfStakeSteps.PosNodeWalletHasEarnedCoinsThroughStaking();
        }

        private void a_staking_wallet_minting_coins()
        {
            a_proof_of_work_node_with_wallet();
            it_mines_genesis_and_premine_blocks();
            mine_coins_to_maturity();
            a_proof_of_stake_node_with_wallet();
            it_syncs_with_proof_work_node();
            sends_a_million_coins_from_pow_wallet_to_pos_wallet();
            pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives();
            pos_node_mines_ten_blocks_more_ensuring_they_can_be_staked();
            pos_node_starts_staking();
            pos_node_wallet_has_earned_coins_through_staking();
        }

        private void it_creates_a_transaction_to_spend()
        {
            this.powSenderAddress = this.nodes[PowMiner].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(PowWallet, WalletAccount));

            this.transactionBuildContext = SharedSteps.CreateTransactionBuildContext(
                    PosWallet, 
                    WalletAccount, 
                    PosWalletPassword, 
                    new List<Recipient>() { new Recipient { Amount = Money.COIN * 100, ScriptPubKey = this.powSenderAddress.ScriptPubKey } }, 
                    FeeType.Medium, 
                    10);

            this.transactionBuildContext.OverrideFeeRate = new FeeRate(Money.Satoshis(20000));
        }

        private void it_is_rejected_because_of_no_spendable_coins()
        {
            try
            {
                this.lastTransaction = this.nodes[PosStaker].FullNode.WalletTransactionHandler()
                    .BuildTransaction(SharedSteps.CreateTransactionBuildContext(
                        PosWallet, 
                        WalletAccount, 
                        PosWalletPassword, 
                        new List<Recipient>() { new Recipient { Amount = Money.COIN * 100, ScriptPubKey = this.powSenderAddress.ScriptPubKey } },
                        FeeType.Medium, 
                        10));

                this.nodes[PosStaker].FullNode.NodeService<WalletController>()
                   .SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                exception.Should().BeOfType<WalletException>();
                exception.Message.Should().Be("No spendable transactions found.");
            }
        }
    }
}