using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.PeerFlooding.Controllers
{
    [Route("api/[controller]")]
    public class PeerFloodingController : Controller
    {
        private readonly int totalruns;
        private readonly IEnumerable<KeyValuePair<string, InternalNetworkNodeWallet.NodeWallet>> nodeWallets;

        public PeerFloodingController(int totalruns = 10000)
        {
            this.totalruns = totalruns;
            this.nodeWallets = InternalNetworkNodeWallet.Map.Where(nw => nw.Value.HasFunds == true).Take(10);
        }

        [Route("OverrideMempoolSettings")]
        [HttpPost]
        public void OverrideMempoolSettings()
        {
            var count = this.nodeWallets.Count();

            /* TODO - Pass params to override mempool contants
            MempoolBehaviour    InventoryBroadcastMax
            MempoolValidator    DefaultAncestorLimit
            MempoolValidator    DefaultAncestorSizeLimit
            MempoolValidator    DefaultDescendantLimit
            MempoolValidator    DefaultDescendantSizeLimit
            */
        }

        [Route("RecoverWallets")]
        [HttpGet]
        public void RecoverWallets()
        {
            Parallel.ForEach(this.nodeWallets, async (nodeWallet) =>
            {
                string nodeWalletKey = nodeWallet.Key;

                var response = await $"http://localhost:37221/api/wallet/recover".PostJsonAsync(new WalletRecoveryRequest
                {
                    Name = InternalNetworkNodeWallet.Map[nodeWalletKey].WalletName,
                    Password = InternalNetworkNodeWallet.Map[nodeWalletKey].Password,
                    Passphrase = InternalNetworkNodeWallet.Map[nodeWalletKey].Password,
                    Mnemonic = InternalNetworkNodeWallet.Map[nodeWalletKey].MnemonicPhrase,
                    CreationDate = new DateTime(2017, 10, 13)
                }).ReceiveString();
            });
        }

        /* TODO - create next api endpoints.

        [Route("SendTransactionWithFee")]
        [HttpGet]
        public void SendTransactionWithLowFee()
        {
            // for each wallet

            // send 1,000 txn (and save hex values)
            // every 5 seconds


            // return true
        }

        [Route("FloodNetwork")]
        [HttpGet]
        public void FloodNetwork()
        {
            // gets hex values from disk 
            // if it doesn't exist return fail

            // for each hex send

            // check desintation recieved funds

            // check NodeEA this node (has been banned).
        }

        */
    }
}
