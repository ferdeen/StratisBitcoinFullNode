﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using static Stratis.Bitcoin.Features.Miner.PosMinting;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProofOfStakeSteps
    {
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private SharedSteps sharedSteps;

        private HdAddress powSenderAddress;
        private Key powSenderPrivateKey;

        private HdAddress posReceiverAddress;
        private Key posReceiverPrivateKey;

        private const decimal OneMillion = 1_000_000;

        public readonly string PowMiner = "ProofOfWorkNode";
        public readonly string PosStaker = "ProofOfStakeNode";

        public readonly string PowWallet = "powwallet";
        public readonly string PowWalletPassword = "password";

        public readonly string PosWallet = "poswallet";
        public readonly string PosWalletPassword = "password";

        public readonly string WalletAccount = "account 0";
    
        public ProofOfStakeSteps(string displayName) 
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder(displayName);
        }

        public void GenerateCoins()
        {
            ProofOfWorkNodeWithWallet();
            MineGenesisAndPremineBlocks();
            MineCoinsToMaturity();
            ProofOfStakeNodeWithWallet();
            SyncWithProofWorkNode();
            SendOneMillionCoinsFromPowWalletToPosWallet();
            PowWalletBroadcastsTransactionOfOneMillionCoinsAndPosWalletReceives();
            PosNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked();
            PosNodeStartsStaking();
            PosNodeWalletHasEarnedCoinsThroughStaking();
        }

        public long WalletTotalAmount()
        {
            return this.nodes[this.PosStaker].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(this.PosWallet)
                .Sum(utxo => utxo.Transaction.Amount);
        }

        public CoreNode ProofOfStakeNodeWithCoins => this.nodes?[this.PosStaker];

        public CoreNode AddAndConnectProofOfStakeNodes(string nodeName, string walletName, string walletPassword)
        {
            var newProofOfStakeNode = this.nodeGroupBuilder.CreateStratisPosNode(nodeName)
                .Start()
                .NotInIBD()
                .WithWallet(walletName, walletPassword)
                .Build();

            this.nodeGroupBuilder.WithConnections().Connect(this.PosStaker, nodeName);

            return newProofOfStakeNode[nodeName];
        }

        public void ProofOfWorkNodeWithWallet()
        {
            this.nodes = this.nodeGroupBuilder
                    .CreateStratisPowMiningNode(this.PowMiner)
                    .Start()
                    .NotInIBD()
                    .WithWallet(this.PowWallet, this.PowWalletPassword)
                    .Build();

            this.powSenderAddress = this.nodes[this.PowMiner].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.PowWallet, this.WalletAccount));
            var wallet = this.nodes[this.PowMiner].FullNode.WalletManager().GetWalletByName(this.PowWallet);
            this.powSenderPrivateKey = wallet.GetExtendedPrivateKeyForAddress(this.PowWalletPassword, this.powSenderAddress).PrivateKey;
        }

        public void MineGenesisAndPremineBlocks()
        {
            this.sharedSteps.MinePremineBlocks(this.nodes[this.PowMiner], this.PowWallet, this.WalletAccount, this.PowWalletPassword);
        }

        public void MineCoinsToMaturity()
        {
            this.nodes[this.PowMiner].GenerateStratisWithMiner(100);
            this.sharedSteps.WaitForNodeToSync(this.nodes[this.PowMiner]);
        }

        public void ProofOfStakeNodeWithWallet()
        {
            this.nodeGroupBuilder
                    .CreateStratisPosNode(this.PosStaker)
                    .Start()
                    .NotInIBD()
                    .WithWallet(this.PosWallet, this.PosWalletPassword)
                    .Build();
        }

        public void SyncWithProofWorkNode()
        {
            this.posReceiverAddress = this.nodes[this.PosStaker].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.PosWallet, this.WalletAccount));
            var wallet = this.nodes[this.PosStaker].FullNode.WalletManager().GetWalletByName(this.PosWallet);
            this.posReceiverPrivateKey = wallet.GetExtendedPrivateKeyForAddress(this.PosWalletPassword, this.posReceiverAddress).PrivateKey;

            this.nodes[this.PosStaker].SetDummyMinerSecret(new BitcoinSecret(this.posReceiverPrivateKey, this.nodes[this.PosStaker].FullNode.Network));
            this.nodeGroupBuilder.WithConnections().Connect(this.PowMiner, this.PosStaker);
        }

        public void SendOneMillionCoinsFromPowWalletToPosWallet()
        {
            var context = SharedSteps.CreateTransactionBuildContext(
                this.PowWallet,
                this.WalletAccount,
                this.PowWalletPassword,
                new List<Recipient>() { new Recipient { Amount = Money.COIN * 1000000, ScriptPubKey = this.posReceiverAddress.ScriptPubKey } },
                FeeType.Medium,
                101);

            context.OverrideFeeRate = new FeeRate(Money.Satoshis(20000));

            var unspent = this.nodes[this.PowMiner].FullNode.WalletManager().GetSpendableTransactionsInWallet(this.PowWallet);
            var coins = new List<Coin>();

            var blockTimestamp = unspent.OrderBy(u => u.Transaction.CreationTime).Select(ts => ts.Transaction.CreationTime).First();
            var transaction = this.nodes[this.PowMiner].FullNode.Network.Consensus.ConsensusFactory.CreateTransaction();
            transaction.Time = (uint)blockTimestamp.ToUnixTimeSeconds();

            foreach (var item in unspent.OrderByDescending(a => a.Transaction.Amount))
            {
                coins.Add(new Coin(item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey));
            }

            var coin = coins.First();
            var txIn = transaction.AddInput(new TxIn(coin.Outpoint, this.powSenderAddress.ScriptPubKey));
            transaction.AddOutput(new TxOut(new Money(9699999999995400), this.powSenderAddress.ScriptPubKey));
            transaction.AddOutput(new TxOut(new Money(100000000000000), this.posReceiverAddress.ScriptPubKey));
            transaction.Sign(this.nodes[this.PowMiner].FullNode.Network, this.powSenderPrivateKey, new[] { coin });

            this.nodes[this.PowMiner].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        public void PowWalletBroadcastsTransactionOfOneMillionCoinsAndPosWalletReceives()
        {
            TestHelper.WaitLoop(() => this.nodes[this.PosStaker].CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.nodes[this.PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(this.PosWallet).Any());

            this.sharedSteps.WaitForNodeToSync(this.nodes[this.PosStaker]);

            var received = this.nodes[this.PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(this.PosWallet);
            received.Sum(s => s.Transaction.Amount).Should().Be(Money.COIN * 1000000);
        }

        public void PosNodeMinesTenBlocksMoreEnsuringTheyCanBeStaked()
        {
            this.nodes[this.PosStaker].GenerateStratisWithMiner(Convert.ToInt32(this.nodes[this.PosStaker].FullNode.Network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity));
        }

        public void PosNodeStartsStaking()
        {
            var minter = this.nodes[this.PosStaker].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PosWallet, WalletPassword = PosWalletPassword });
        }

        public void PosNodeWalletHasEarnedCoinsThroughStaking()
        {
            TestHelper.WaitLoop(() =>
            {
                var staked = this.nodes[this.PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(this.PosWallet).Sum(s => s.Transaction.Amount);
                return staked >= Money.COIN * 1000000;
            });
        }

        public IActionResult SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

            if (walletTransactionModel == null) return null;

            return this.nodes[this.PosStaker].FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        public IActionResult BuildTransaction(string nodeName, string walletName, string walletAccount, string walletPassword)
        {
            var recevierNode = this.CreateReceiverNode(nodeName, walletName, walletPassword);

            var transactionResult = this.nodes[this.PosStaker].FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = this.WalletAccount,
                    AllowUnconfirmed = true,
                    Amount = Money.Coins(OneMillion + 40).ToString(),
                    DestinationAddress = this.GetReceiverUnusedAddressFromWallet(
                        recevierNode,
                        walletName,
                        walletAccount,
                        walletPassword),
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = this.PosWalletPassword,
                    WalletName = this.PosWallet,
                    FeeAmount = Money.Satoshis(20000).ToString()
                });

            return transactionResult;
        }

        public string GetReceiverUnusedAddressFromWallet(CoreNode node, string walletName, string walletAccount, string walletPassword)
        {            
            node.FullNode.WalletManager().CreateWallet(walletPassword, walletName);

            return node.FullNode.WalletManager().GetUnusedAddress(
                new WalletAccountReference(walletName, walletAccount)).Address;
        }

        public CoreNode GetNode(string nodeName)
        {
            return this.nodes[nodeName];
        }

        public CoreNode CreateReceiverNode(string nodeName, string walletName, string walletPassword)
        {
            return this.AddAndConnectProofOfStakeNodes(nodeName, walletName, walletPassword);
        }
    }
}
