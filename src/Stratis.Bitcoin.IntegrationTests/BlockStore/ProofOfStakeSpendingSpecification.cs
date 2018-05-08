using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
// ReSharper disable ArrangeThisQualifier

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfStakeSpendingSpecification : BddSpecification
    {
        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_stake_BEFORE_coin_maturity_will_fail()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins);
            When(spending_the_coins_from_original_block);
            Then(the_transaction_is_rejected_from_the_mempool);
        }

        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_stake_AFTER_maturity_will_succeed()
        {
            Given(two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins);
            And(more_blocks_mined_to_just_AFTER_maturity_of_original_block);
            When(spending_the_coins_from_original_block);
            Then(the_transaction_is_put_in_the_mempool);
        }
    }
}