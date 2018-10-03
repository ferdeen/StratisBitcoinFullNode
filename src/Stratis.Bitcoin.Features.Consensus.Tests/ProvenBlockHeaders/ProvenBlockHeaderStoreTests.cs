﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        private readonly Network network = KnownNetworks.StratisTest;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly ConcurrentChain concurrentChain;
        private readonly ProvenBlockHeaderStore provenBlockHeaderStore;
        private IProvenBlockHeaderRepository provenBlockHeaderRepository;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly Mock<IAsyncLoopFactory> asyncLoopFactoryLoop;
        private readonly string Folder;
        private readonly NodeStats nodeStats;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.concurrentChain = this.GenerateChainWithHeight(3);
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeStats = new NodeStats(DateTimeProvider.Default);
            this.asyncLoopFactoryLoop = new Mock<IAsyncLoopFactory>();

            this.Folder = CreateTestDir(this);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.network, this.Folder, this.LoggerFactory.Object);

            this.provenBlockHeaderStore = new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default, this.LoggerFactory.Object,
                this.provenBlockHeaderRepository, this.nodeLifetime.Object, this.nodeStats, this.asyncLoopFactoryLoop.Object);
        }

        [Fact]
        public async Task InitialiseStoreToGenesisChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Genesis;

            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            var hashHeightPair = await this.provenBlockHeaderStore.CurrentTipHashHeightAsync().ConfigureAwait(false);

            hashHeightPair.Hash.Should().Be(this.network.GetGenesis().GetHash());
        }

        [Fact]
        public async Task LoadItemsAsync()
        {
            // Put 3 items to in the repository - items created in the constructor.
            var inItems = new List<ProvenBlockHeader>();

            var provenHeaderMock = CreateNewProvenBlockHeaderMock();

            ChainedHeader chainedHeader = this.concurrentChain.Tip;

            while(chainedHeader != null)
            {
                inItems.Add(provenHeaderMock);

                chainedHeader = chainedHeader.Previous;
            }

            await this.provenBlockHeaderRepository.PutAsync(inItems, new HashHeightPair(provenHeaderMock.GetHash(), inItems.Count - 1)).ConfigureAwait(false);

            // Then load them.
            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                var outItems = await store.GetAsync(0, inItems.Count).ConfigureAwait(false);

                outItems.Count.Should().Be(inItems.Count);
                
                foreach(var item in outItems)
                {
                    item.GetHash().Should().Be(provenHeaderMock.GetHash());
                }
            }
        }

        [Fact]
        public async Task AddToPending_Adds_To_Cache_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            // Add to pending (add to internal cache).
            var inHeader = CreateNewProvenBlockHeaderMock();
            this.provenBlockHeaderStore.AddToPending(inHeader, new HashHeightPair(inHeader.GetHash(), 1));

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingCache.GetMemberValue("Count");
            cacheCount.Should().Be(1);

            // Get item.
            var outHeader = await this.provenBlockHeaderStore.GetAsync(1).ConfigureAwait(false);
            outHeader.GetHash().Should().Be(inHeader.GetHash());

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1, 1).ConfigureAwait(false);
            outHeaderRepo.FirstOrDefault().Should().BeNull();
        }

        [Fact]
        public async Task AddToPending_Adds_To_Cache_Then_Save_To_Disk_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            // Add to pending (add to internal cache).
            var inHeader = CreateNewProvenBlockHeaderMock();
            this.provenBlockHeaderStore.AddToPending(inHeader, new HashHeightPair(inHeader.GetHash(), 0));

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingCache.GetMemberValue("Count");
            cacheCount.Should().Be(1);

            // Get item.
            var outHeader = await this.provenBlockHeaderStore.GetAsync(0).ConfigureAwait(false);
            outHeader.GetHash().Should().Be(inHeader.GetHash());

            // Call the internal save method to save cached item to disk.
            this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

            // when pendingTipHashHeight is null we can safely say the items were saved to the repository, based on the above SaveAsync.
            WaitLoop(() => {
                var pendingTipHashHeight = this.provenBlockHeaderStore.GetMemberValue("pendingTipHashHeight");
                return pendingTipHashHeight == null;
            });

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(0).ConfigureAwait(false);
            outHeaderRepo.GetHash().Should().Be(outHeader.GetHash());
        }

        [Fact]
        public async Task Add_2k_ProvenHeaders_ToPending_Cache_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            ProvenBlockHeader inHeader = null;

            // Add to pending (add to internal cache).
            for (int i = 0; i < 2_000; i++)
            {
                inHeader = CreateNewProvenBlockHeaderMock();
                this.provenBlockHeaderStore.AddToPending(inHeader, new HashHeightPair(inHeader.GetHash(), i));
            }

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingCache.GetMemberValue("Count");
            cacheCount.Should().Be(2_000);

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1, 1).ConfigureAwait(false);
            outHeaderRepo.FirstOrDefault().Should().BeNull();
        }

        [Fact]
        public async Task Add_2k_ProvenHeaders_ToPending_Cache_Save_Items_Cache_Should_Be_Empty_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            ProvenBlockHeader inHeader = null;

            // Add to pending (add to internal cache).
            for (int i = 0; i < 2_000; i++)
            {
                inHeader = CreateNewProvenBlockHeaderMock();
                this.provenBlockHeaderStore.AddToPending(inHeader, new HashHeightPair(inHeader.GetHash(), i));
            }

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingCache.GetMemberValue("Count");
            cacheCount.Should().Be(2_000);

            // Call the internal save method to save cached item to disk.
            this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

            // when pendingTipHashHeight is null we can safely say the items were saved to the repository, based on the above SaveAsync.
            WaitLoop(() => {
                var pendingTipHashHeight = this.provenBlockHeaderStore.GetMemberValue("pendingTipHashHeight");
                return pendingTipHashHeight == null;
            });

            // Check if it has been saved to disk.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1999).ConfigureAwait(false);
            outHeaderRepo.Should().NotBeNull();

            // Check items in cache - should now be empty.
            cacheCount = this.provenBlockHeaderStore.PendingCache.GetMemberValue("Count");
            cacheCount.Should().Be(0);
        }

        [Fact]
        public async Task GetAsync_MaxCacheItems_Gets_Items_From_Repository_Adds_To_Store_Cache_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            int maxCacheCount = (int)this.provenBlockHeaderStore
                .GetType()
                .GetField("CacheMaxItemsCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null);

            var inHeaders = new List<ProvenBlockHeader>();

            // Add maximum cache count items headers.
            for (int i = 0; i < maxCacheCount; i++)
                inHeaders.Add(CreateNewProvenBlockHeaderMock());

            // Add items to the repository.
            await this.provenBlockHeaderRepository.PutAsync(inHeaders, 
                new HashHeightPair(inHeaders.LastOrDefault().GetHash(), inHeaders.Count - 1)).ConfigureAwait(false);

            // Check Item in cache -  should be zero as not asked for headers from the store.
            var cacheCount = this.provenBlockHeaderStore.Cache.GetMemberValue("Count");
            cacheCount.Should().Be(0);

            // Asking for headers will check the cache store.  If the header is not in the cache store, then it will check the repository and add the cache store.
            var outHeaders = await this.provenBlockHeaderStore.GetAsync(0, inHeaders.Count - 1).ConfigureAwait(false);

            // Check Item in cache -  should now contain the cached items.
            cacheCount = this.provenBlockHeaderStore.Cache.GetMemberValue("Count");
            cacheCount.Should().Be(inHeaders.Count);

            // Get items returned from getAsync().
            outHeaders.Count.Should().Be(maxCacheCount);
        }

        [Fact]
        public async Task GetAsync_MaxCacheItems_Exceeds_Max_Size_Cache_Reduced_Async()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            var inHeaders = new List<ProvenBlockHeader>();

            // Add maximum cache count items headers.
            for (int i = 0; i < 10; i++)
                inHeaders.Add(CreateNewProvenBlockHeaderMock());

            // Reduce the MaxMemoryCacheSizeInBytes size for test.
            FieldInfo field = typeof(ProvenBlockHeaderStore).GetField("MaxMemoryCacheSizeInBytes", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(this.provenBlockHeaderStore, 1000);

            this.provenBlockHeaderStore.InvokeMethod("ManangeCacheSize");

            // Add items to the repository.
            await this.provenBlockHeaderRepository.PutAsync(inHeaders,
                new HashHeightPair(inHeaders.LastOrDefault().GetHash(), inHeaders.Count - 1)).ConfigureAwait(false);

            // Check Item in cache - should be zero as not asked for headers from the store.
            var cacheCount = this.provenBlockHeaderStore.Cache.GetMemberValue("Count");
            cacheCount.Should().Be(0);

            long cacheStoreSize = (long)this.provenBlockHeaderStore.InvokeMethod("MemoryCacheSize");
            cacheStoreSize.Should().Be(0);

            // Asking for headers will check the cache store.  If the header is not in the cache store, then it will check the repository and add the cache store.
            var outHeaders = await this.provenBlockHeaderStore.GetAsync(0, inHeaders.Count - 1).ConfigureAwait(false);

            this.provenBlockHeaderStore.InvokeMethod("ManangeCacheSize");
            cacheStoreSize = (long)this.provenBlockHeaderStore.InvokeMethod("MemoryCacheSize");
            cacheStoreSize.Should().BeLessThan(1000);
        }

        private IProvenBlockHeaderStore SetupStore(string folder)
        {
            return new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default,
                this.LoggerFactory.Object, this.provenBlockHeaderRepository,
                this.nodeLifetime.Object, new NodeStats(DateTimeProvider.Default), this.asyncLoopFactoryLoop.Object);
        }

        private ConcurrentChain GenerateChainWithHeight(int blockAmount)
        {
            var chain = new ConcurrentChain(this.network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private static void WaitLoop(Func<bool> act, string failureReason = "Unknown Reason", int retryDelayInMiliseconds = 1000, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken = cancellationToken == default(CancellationToken)
                ? new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 60 * 1000).Token
                : cancellationToken;

            while (!act())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(retryDelayInMiliseconds);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }
    }
}
