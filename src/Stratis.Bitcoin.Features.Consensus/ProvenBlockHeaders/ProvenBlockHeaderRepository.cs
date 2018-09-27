﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Persistent implementation of the <see cref="ProvenBlockHeader"></see> DBreeze repository.
    /// </summary>
    public class ProvenBlockHeaderRepository : IProvenBlockHeaderRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>Specification of the network the node runs on - RegTest/TestNet/MainNet.</summary>
        private readonly Network network;

        /// <summary>Database key under which the <see cref="ProvenBlockHeader"/> item is stored.</summary>
        private static readonly byte[] provenBlockHeaderKey = new byte[0];

        /// <summary>Database key under which the block hash of the <see cref="ProvenBlockHeader"/> tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// <summary>DBreeze table names.</summary>
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashTable = "BlockHash";

        /// <summary>Hash of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.</summary>
        private uint256 blockHash;
        
        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;
        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderRepository(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
            : this(network, dataFolder.ProvenBlockHeaderPath, dateTimeProvider, loggerFactory, nodeStats)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderStore"/> folder path to the DBreeze database files.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderRepository(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // Create the ProvenBlockHeaderStore if it doesn't exist.
            Directory.CreateDirectory(folder);

            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <inheritdoc />
        public Task InitializeAsync(uint256 blockHash = null)
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogInformation("Initializing {0}.", nameof(ProvenBlockHeaderRepository));
                
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    if (this.GetTipHash(transaction) == null)
                    {
                        uint256 blockId = blockHash ?? this.network.GetGenesis().GetHash();

                        this.SetTipHash(transaction, blockId);

                        transaction.Commit();
                    }
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task<List<StakeItem>> GetAsync(IEnumerable<uint256> blockIds)
        {
            Guard.NotNull(blockIds, nameof(blockIds));

            Task<List<StakeItem>> task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(blockIds), blockIds.Count());

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    List<StakeItem> stakeItems = new List<StakeItem>();

                    transaction.SynchronizeTables(ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
                    {
                        foreach (uint256 blockId in blockIds)
                        {
                            this.logger.LogTrace("Loading ProvenBlockHeader hash '{0}' from the database.", blockId);

                            Row<byte[], ProvenBlockHeader> row =
                                transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockId.ToBytes(false));

                            if (row.Exists)
                            {
                                stakeItems.Add(new StakeItem
                                {
                                    BlockId = blockId,
                                    ProvenBlockHeader = row.Value,
                                    InStore = true,
                                });
                            }
                        }
                    }

                    transaction.ValuesLazyLoadingIsOn = true;

                    this.logger.LogTrace("(-)");

                    return stakeItems;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(IEnumerable<StakeItem> stakeItems)
        {
            Guard.NotNull(stakeItems, nameof(stakeItems));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashTable, ProvenBlockHeaderTable);

                    using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                    {
                        this.InsertProvenHeaders(transaction, stakeItems);

                        transaction.Commit();
                    }
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetTipHashAsync()
        {
            Task<uint256> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                uint256 tipHash;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    tipHash = this.GetTipHash(transaction);
                }

                this.logger.LogTrace("(-):'{0}'", tipHash);

                return tipHash;
            });

            return task;
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(uint256 blockId)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            Guard.NotNull(blockId, nameof(blockId));

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    Row<byte[], ProvenBlockHeader> blockRow = 
                        transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockId.ToBytes(false));

                    this.logger.LogTrace("(-):{0}", blockRow.Exists);

                    return blockRow.Exists;
                }
            });

            this.logger.LogTrace("(-)");

            return task;
        }

        /// <inheritdoc />
        public Task DeleteAsync(uint256 newTip, List<uint256> blockIds)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newTip), newTip, nameof(blockIds), nameof(blockIds.Count), blockIds?.Count);

            Guard.NotNull(newTip, nameof(newTip));

            Guard.NotNull(blockIds, nameof(blockIds));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashTable, ProvenBlockHeaderTable);

                    transaction.ValuesLazyLoadingIsOn = false;

                    foreach (uint256 blockId in blockIds)
                        transaction.RemoveKey<byte[]>(ProvenBlockHeaderTable, blockId.ToBytes(false));

                    this.SetTipHash(transaction, newTip);

                    transaction.Commit();

                    transaction.ValuesLazyLoadingIsOn = true;
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");

            return task;
        }

        /// <summary>
        /// Obtains a block hash of the current tip.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <returns>Hash of blocks current tip.</returns>
        private uint256 GetTipHash(DBreeze.Transactions.Transaction transaction)
        {
            if (this.blockHash == null)
            {
                transaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], uint256> row = transaction.Select<byte[], uint256>(BlockHashTable, blockHashKey);

                if (row.Exists)
                    this.blockHash = row.Value;

                transaction.ValuesLazyLoadingIsOn = false;
            }

            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip to a new block hash.  ### re word ###
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="blockId">Hash of the block to become the new tip.</param>
        private void SetTipHash(DBreeze.Transactions.Transaction transaction, uint256 blockId)
        {
            Guard.NotNull(blockId, nameof(blockId));

            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            this.blockHash = blockId;

            transaction.Insert<byte[], uint256>(BlockHashTable, blockHashKey, blockId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/>s from <see cref="StakeItem"/>s, and adds them to the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="stakeItems">List of <see cref="StakeItem"/>s.</param>
        private void InsertProvenHeaders(DBreeze.Transactions.Transaction transaction, IEnumerable<StakeItem> stakeItems)
        {
            this.logger.LogTrace("({0}.Count():{1})", nameof(stakeItems), stakeItems.Count());

            IEnumerable<StakeItem> sortedStakeItems = this.SortProvenHeaders(transaction, stakeItems);

            foreach (StakeItem stakeItem in sortedStakeItems)
            {
                if (!stakeItem.InStore)
                {
                    transaction.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, stakeItem.BlockId.ToBytes(false), stakeItem.ProvenBlockHeader);
                    stakeItem.InStore = true;
                }

                this.SetTipHash(transaction, stakeItem.BlockId);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sorts <see cref="ProvenBlockHeader"/>s.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="stakeItems">List of <see cref="StakeItem"/>s.</param>
        /// <returns><see cref="StakeItem"/> enumerator.</returns>
        private IEnumerable<StakeItem> SortProvenHeaders(DBreeze.Transactions.Transaction transaction, IEnumerable<StakeItem> stakeItems)
        {
            var stakeDict = new Dictionary<uint256, StakeItem>();

            foreach(StakeItem item in stakeItems)
                stakeDict[item.BlockId] = item;

            List<KeyValuePair<uint256, StakeItem>> stakeItemList = stakeDict.ToList();

            stakeItemList.Sort((pair1, pair2) => pair1.Value.Height.CompareTo(pair2.Value.Height));

            transaction.ValuesLazyLoadingIsOn = false;

            foreach (KeyValuePair<uint256, StakeItem> stakeItem in stakeItemList)
            {
                StakeItem outStakeItem = stakeItem.Value;

                // Check if the header already exists in the database.
                Row<byte[], ProvenBlockHeader> headerRow = transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, outStakeItem.BlockId.ToBytes());

                if (!headerRow.Exists)
                {
                    yield return outStakeItem;
                }
            }

            transaction.ValuesLazyLoadingIsOn = true;
        }

        /// <summary>
        /// Checks whether a <see cref="ProvenBlockHeader"/> exists in the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="blockId">Block hash key to search on.</param>
        /// <returns>True if the items exists in the database.</returns>
        private bool ProvenBlockHeaderExists(DBreeze.Transactions.Transaction transaction, uint256 blockId)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            transaction.ValuesLazyLoadingIsOn = false;

            Row<byte[], ProvenBlockHeader> row = transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockId.ToBytes());

            transaction.ValuesLazyLoadingIsOn = true;
        
            this.logger.LogTrace("(-):{0}", row.Exists);

            return row.Exists;
        }

        private List<ProvenBlockHeader> GetProvenBlockHeadersByBlockId(DBreeze.Transactions.Transaction transaction, List<uint256> blockIds)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockIds), nameof(blockIds.Count), blockIds?.Count);

            var results = new Dictionary<uint256, ProvenBlockHeader>();

            // Access hash keys in sorted order.
            var byteListComparer = new ByteListComparer();

            List<(uint256, byte[])> keys = blockIds.Select(hash => (hash, hash.ToBytes())).ToList();

            keys.Sort((key1, key2) => byteListComparer.Compare(key1.Item2, key2.Item2));

            foreach ((uint256, byte[]) key in keys)
            {
                Row<byte[], ProvenBlockHeader> blockRow = transaction.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, key.Item2);

                if (blockRow.Exists)
                {
                    results[key.Item1] = blockRow.Value;

                    this.logger.LogTrace("Block hash '{0}' loaded from the store.", key.Item1);
                }
                else
                {
                    results[key.Item1] = null;

                    this.logger.LogTrace("Block hash '{0}' not found in the store.", key.Item1);
                }
            }

            this.logger.LogTrace("(-):{0}", results.Count);

            // Return the result in the order that the hashes were presented.
            return blockIds.Select(hash => results[hash]).ToList();
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            this.logger.LogTrace("()");

            benchLog.AppendLine("======ProvenBlockHeaderRepository Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                benchLog.AppendLine(snapShot.ToString());
            else
                benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze?.Dispose();
        }
    }
}
