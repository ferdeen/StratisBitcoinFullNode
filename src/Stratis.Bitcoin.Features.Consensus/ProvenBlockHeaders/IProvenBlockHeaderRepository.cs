﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface for database <see cref="ProvenBlockHeader"></see> repository.
    /// </summary>
    public interface IProvenBlockHeaderRepository : IDisposable
    {
        /// <summary>Loads <see cref="ProvenBlockHeader"/> items from the database.</summary>
        /// <param name="blockHash">BlockId to initial the database with.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync(uint256 blockHash = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary> Retrieves the block hash of the current <see cref="ProvenBlockHeader"/> tip.</summary>
        /// <returns>Block hash of the current tip of the <see cref="ProvenBlockHeader"/>.</returns>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items from the database.
        /// </summary>
        /// <param name="stakeItems">Proof of stake items which includes <see cref="ProvenBlockHeader"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task GetAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Persists <see cref="ProvenBlockHeader"/> items to the database.</summary>
        /// <param name="stakeItems">Proof of stake items which includes <see cref="ProvenBlockHeader"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task PutAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken));
    }
}
