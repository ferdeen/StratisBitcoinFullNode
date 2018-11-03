using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.PeerFlooding.Controllers
{
    [Route("api/[controller]")]
    public class PeerFloodingController : Controller
    {
        private readonly int totalruns;
        private readonly IEnumerable<KeyValuePair<string, InternalNetworkNodeWallet.NodeWallet>> nodeWallets;
        private readonly List<string> transactionHexList;

        public PeerFloodingController(int totalruns = 10000)
        {
            this.totalruns = totalruns;
            this.nodeWallets = InternalNetworkNodeWallet.Map.Where(nw => nw.Value.HasFunds == true).Take(10);
            this.transactionHexList = new List<string>();
        }

        [Route("RecoverWallets")]
        [HttpGet]
        public void RecoverWallets()
        {
            Parallel.ForEach(this.nodeWallets, async (nodeWallet) =>
            {
                string nodeWalletKey = nodeWallet.Key;

                await $"http://localhost:37221/api/wallet/recover".PostJsonAsync(new WalletRecoveryRequest
                {
                    Name = InternalNetworkNodeWallet.Map[nodeWalletKey].WalletName,
                    Password = InternalNetworkNodeWallet.Map[nodeWalletKey].Password,
                    Passphrase = InternalNetworkNodeWallet.Map[nodeWalletKey].Password,
                    Mnemonic = InternalNetworkNodeWallet.Map[nodeWalletKey].MnemonicPhrase,
                    CreationDate = new DateTime(2017, 10, 13)
                });
            });
        }

        [Route("SendTransactionsWithLowFee")]
        [HttpGet]
        public void SendTransactionsWithLowFee()
        {
            Parallel.ForEach(this.nodeWallets, async (nodeWallet) =>
            {
                string nodeWalletKey = nodeWallet.Key;

                var unusedAddress = await "http://localhost:37221/api/wallet/unusedAddress"
                    .SetQueryParams(new { walletName = InternalNetworkNodeWallet.Map[nodeWalletKey].WalletName, accountName = "account 0" })
                    .GetJsonAsync<string>();

                var request = new BuildTransactionRequest
                {
                    WalletName = InternalNetworkNodeWallet.Map[nodeWalletKey].WalletName,
                    AccountName = "account 0",
                    AllowUnconfirmed = true,
                    Amount = "1",
                    FeeType = "low",
                    Password = InternalNetworkNodeWallet.Map[nodeWalletKey].Password,
                    DestinationAddress = unusedAddress.ToString()
                };

                for (int i = 0; i <= 1000; i++)
                {
                    var newTransaction = await "http://localhost:37221/api/wallet/build-transaction"
                        .PostJsonAsync(request)
                        .ReceiveJson();

                    string hex = (newTransaction as ExpandoObject).FirstOrDefault(x => x.Key == "hex").Value.ToString();

                    // Save transaction hex, for flooding the network after the network has been reset.
                    this.transactionHexList.Add(hex);

                    try
                    {
                        var response = await "http://localhost:37221/api/wallet/send-transaction"
                            .PostJsonAsync(new SendTransactionRequest(hex));

                        Thread.Sleep(5000);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("code 400"))
                        {
                            Thread.Sleep(60000);
                        }
                    }
                }
            });
        }

        /*
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
