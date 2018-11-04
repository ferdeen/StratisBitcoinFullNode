using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.PeerFlooding.Controllers
{
    [Route("api/[controller]")]
    public class PeerFloodingController : FeatureController
    {
        private readonly int totalruns;
        private readonly IEnumerable<KeyValuePair<string, InternalNetworkNodeWallet.NodeWallet>> nodeWallets;
        private readonly string floodFileName;

        public PeerFloodingController(NodeSettings nodeSettings, int totalruns = 10000)
        {
            this.totalruns = totalruns;
            this.nodeWallets = InternalNetworkNodeWallet.Map.Where(nw => nw.Value.HasFunds == true).Take(1);
            this.floodFileName = nodeSettings.DataFolder.RootPath + @"\flood.dat";
            new FileStream(this.floodFileName, FileMode.OpenOrCreate);
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
        public IActionResult SendTransactionsWithLowFee()
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

                    await this.SendTransactionAsync(hex);

                    System.IO.File.AppendAllText(this.floodFileName, hex + Environment.NewLine);
                }
            });

            var model = new PeerFloodingGeneralInfoModel()
            {
                FloodFilePath = this.floodFileName,
                Info = "Creating file.  Please reset the network before flooding it with 10,000 hex transations stored in this file."
            };

            return this.Json(model);
        }

        [Route("FloodNetwork")]
        [HttpGet]
        public async Task<IActionResult> FloodNetworkAsync()
        {
            var hexTransactions = this.ReadLines(this.floodFileName);

            foreach (var hexTransaction in hexTransactions)
            {
                await this.SendTransactionAsync(hexTransaction);
            }

            var model = new PeerFloodingGeneralInfoModel()
            {
                FloodFilePath = this.floodFileName,
                Info = "Once flooded check that this node has been banned."
            };

            return this.Json(model);
        }

        private async Task SendTransactionAsync(string hex)
        {
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

        private IEnumerable<string> ReadLines(string filePath)
        {
            using (StreamReader reader = System.IO.File.OpenText(filePath))
            {
                string line = string.Empty;

                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}
