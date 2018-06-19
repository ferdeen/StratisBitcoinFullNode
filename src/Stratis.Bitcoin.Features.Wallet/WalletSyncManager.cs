﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager, IWalletBlockProducerConsumer
    {
        private readonly IWalletManager walletManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBlockStoreCache blockStoreCache;

        private readonly StoreSettings storeSettings;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        protected ChainedHeader walletTip;

        public ChainedHeader WalletTip => this.walletTip;

        /// <inheritdoc />
        public BufferBlock<Block> BlockBuffer { get; }

        private readonly ConcurrentQueue<uint256> hashBlocks;

        private readonly ConcurrentQueue<Block> blocksQueue = new ConcurrentQueue<Block>();

        private readonly object queueLock = new object();

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
            Network network, IBlockStoreCache blockStoreCache, StoreSettings storeSettings, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.walletManager = walletManager;
            this.chain = chain;
            this.blockStoreCache = blockStoreCache;
            this.storeSettings = storeSettings;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.BlockBuffer = new BufferBlock<Block>();
            this.hashBlocks = new ConcurrentQueue<uint256>();
            this.asyncLoopFactory = asyncLoopFactory;
        }

        /// <inheritdoc />
        public void Start()
        {
            this.logger.LogTrace("()");

            // When a node is pruned it impossible to catch up
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be
            // able to download blocks from peers to catch up.
            if (this.storeSettings.Prune)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.", this.walletManager.LastBlockHeight());

            this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
            if (this.walletTip == null)
            {
                // The wallet tip was not found in the main chain.
                // this can happen if the node crashes unexpectedly.
                // To recover we need to find the first common fork
                // with the best chain. As the wallet does not have a
                // list of chain headers, we use a BlockLocator and persist
                // that in the wallet. The block locator will help finding
                // a common fork and bringing the wallet back to a good
                // state (behind the best chain).
                ICollection<uint256> locators = this.walletManager.GetFirstWalletBlockLocator();
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chain.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                this.walletManager.WalletTipHash = fork.HashBlock;
                this.walletTip = fork;
            }
            else
            {
                this.asyncLoop = this.asyncLoopFactory.Run(nameof(WalletSyncManager), async token =>
                {
                    try
                    {
                        ChainedHeader tip = this.chain.Tip;

                        // if not up-to-date then get previous blocks and sync
                        if (tip.Height > this.walletTip.Height)
                        {
                            ChainedHeader findTip = tip.FindAncestorOrSelf(this.walletTip);
                            if (findTip == null)
                            {
                                this.logger.LogTrace("(-)[NEW_TIP_AHEAD_NOT_IN_WALLET]");
                                return;
                            }

                            this.logger.LogTrace("Wallet tip '{0}' is behind the new tip '{1}'.", this.walletTip, tip);

                            ChainedHeader next = this.walletTip;

                            while (next.Height < tip.Height)
                            {
                                token.ThrowIfCancellationRequested();

                                next = tip.GetAncestor(next.Height + 1);

                                Block nextBlock = this.blockStoreCache.GetBlockAsync(next.HashBlock).GetAwaiter().GetResult();

                                if (nextBlock == null) continue;

                                this.walletTip = next;
                                this.walletManager.ProcessBlock(nextBlock, next);
                            }
                        }

                        while (this.blocksQueue.TryDequeue(out Block block))
                        {
                            await this.ProcessAsync(block);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                , this.nodeLifetime.ApplicationStopping
                , repeatEvery: TimeSpans.Second
                , startAfter: TimeSpans.TenSeconds);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.logger.LogTrace("()");
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Produce(ITargetBlock<Block> target, Block block)
        {
            target.Post(block);
        }

        /// <inheritdoc />
        public async Task ConsumeAsync(ISourceBlock<Block> source)
        {
            while (await source.OutputAvailableAsync())
            {
                Block block = source.Receive();

                this.blocksQueue.Enqueue(block);
            }
        }

        /// <inheritdoc />
        public void QueueBlock(Block block)
        {
            Task.Run(() => this.ConsumeAsync(this.BlockBuffer));
            this.Produce(this.BlockBuffer, block);
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            this.QueueBlock(block);
        }

        private async Task ProcessAsync(Block block)
        {
            await Task.Run(() =>
            {
                Guard.NotNull(block, nameof(block));
                this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

                ChainedHeader newTip = this.chain.GetBlock(block.GetHash());
                if (newTip == null)
                {
                    this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                    return;
                }

                // If the new block's previous hash is the same as the
                // wallet hash then just pass the block to the manager.
                if (block.Header.HashPrevBlock != this.walletTip.HashBlock)
                {
                    // If previous block does not match there might have
                    // been a reorg, check if the wallet is still on the main chain.
                    ChainedHeader inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                    if (inBestChain == null)
                    {
                        // The current wallet hash was not found on the main chain.
                        // A reorg happened so bring the wallet back top the last known fork.
                        ChainedHeader fork = this.walletTip;

                        // We walk back the chained block object to find the fork.
                        while (this.chain.GetBlock(fork.HashBlock) == null)
                            fork = fork.Previous;

                        this.logger.LogInformation("Reorg detected, going back from '{0}' to '{1}'.", this.walletTip, fork);

                        this.walletManager.RemoveBlocks(fork);
                        this.walletTip = fork;

                        this.logger.LogTrace("Wallet tip set to '{0}'.", this.walletTip);
                    }

                    // The new tip can be ahead or behind the wallet.
                    // If the new tip is ahead we try to bring the wallet up to the new tip.
                    // If the new tip is behind we just check the wallet and the tip are in the same chain.

                    if (newTip.Height < this.walletTip.Height)
                    {
                        ChainedHeader findTip = this.walletTip.FindAncestorOrSelf(newTip);
                        if (findTip == null)
                        {
                            this.logger.LogTrace("(-)[NEW_TIP_BEHIND_NOT_IN_WALLET]");
                            return;
                        }

                        this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.walletTip, newTip);
                    }
                }

                this.logger.LogTrace("New block follows the previously known block '{0}'.", this.walletTip);

                this.walletTip = newTip;
                this.walletManager.ProcessBlock(block, newTip);

                this.logger.LogTrace("(-)");
            });           
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            this.logger.LogTrace("({0}:'{1}')", nameof(transaction), transaction.GetHash());

            this.walletManager.ProcessTransaction(transaction);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date)
        {
            this.logger.LogTrace("({0}:'{1::yyyy-MM-dd HH:mm:ss}')", nameof(date), date);

            int blockSyncStart = this.chain.GetHeightAtTime(date);
            this.SyncFromHeight(blockSyncStart);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void SyncFromHeight(int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            ChainedHeader chainedHeader = this.chain.GetBlock(height);
            this.walletTip = chainedHeader ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedHeader.HashBlock;

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Builds a list of Chain headers to be processed in batches of 50.
        /// </summary>
        /// <param name="blockHeader"></param>
        /// <param name="token"></param>
        private void ProcessBlockInBatches(CancellationToken token)
        {
            lock (this.queueLock)
            {
                var blocks = new ConcurrentHashSet<uint256>();

                while (this.hashBlocks.TryDequeue(out uint256 block))
                {
                    blocks.Add(block);
                }

                IList<Block> processedBlocks = this.blockStoreCache.GetBlockAsync(blocks.ToList()).GetAwaiter().GetResult();

                foreach (Block processedBlock in processedBlocks.OrderBy(b => b.Header.BlockTime))
                {
                    token.ThrowIfCancellationRequested();

                    ChainedHeader blockChainedHeader = this.chain.GetBlock(processedBlock.GetHash());

                    this.walletManager.WalletTipHash = blockChainedHeader.HashBlock;

                    // TODO: In QueueBlock the chainedHeader.Height > current.Height (line 789) - need to fix to allow batches of blocks to update
                    // Also add relevant unit tests around batches and the consumer producer function

                    this.walletManager.ProcessBlock(processedBlock, blockChainedHeader);
                }
            }
        }
    }
}
