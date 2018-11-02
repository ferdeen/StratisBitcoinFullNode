using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.PeerFlooding.Controllers
{
    internal class InternalNetworkNodeWallet
    {
        /// <summary>
        /// Internal Mainnet Wallets. The information is taken from https://github.com/stratisproject/InternalTestnet/tree/master/Documentation/Wallets.xlsx.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, NodeWallet> Map = new Dictionary<string, NodeWallet>()
        {
            { "NodeAA", new NodeWallet("192.168.98.100", "Wallet-AAA", "render share trim sea scrub version assume lunch border dance rib invest", "node", "TU2gsxokHYVHvTewpyXuxae42Zvbkv9AC3") },
            { "NodeAB", new NodeWallet("192.168.98.101", "Wallet-ABA", "brick expose stable afraid approve shine aisle session knock rebuild term sadness", "node", "TSng2vftTK6siFpXkNNcrwfRt6747NhtpR") },
            { "NodeAC", new NodeWallet("192.168.98.102", "Wallet-ACA", "define arrive bench order acid loud cook law lend only exit torch", "node", "TVVSt4sguuzXA9oRnsrGwp8YKZ5J1N7rMs") },
            { "NodeAD", new NodeWallet("192.168.98.103", "Wallet-ADA", "eternal ahead unlock crystal seminar horse expand fossil first anchor dream view", "node", "TFhpS7BdpZmhhJDuZv4BZXY1qgakJ6yDmD") },
            { "NodeBA", new NodeWallet("192.168.98.110", "Wallet-BAA", "arena fitness spirit design model doll drum brown grid wise unlock foster", "node", "TXXNjC3cVagRaSEeBEDiCG3GpvrYT81dbT") },
            { "NodeCA", new NodeWallet("192.168.98.120", "Wallet-CAA", "slight gravity program female apple happy unfold galaxy satisfy vehicle more cycle", "node", "TCyuQ56qPZ9L4qP64yh5py3nPKUXQAjdXd") },
            { "NodeCB", new NodeWallet("192.168.98.121", "Wallet-CBA", "duty nurse spin garlic chimney tunnel denial draft rich link protect zebra", "node", "TE9vhhMo2UYVTNqwQkbEzeEq6d6anC7K8M") },
            { "NodeDA", new NodeWallet("192.168.98.130", "Wallet-DAA", "rely crucial leader gadget miracle curve theme champion tortoise theme tiger word", "node", "TNF181TUTqTHXcvk6H8YShvGnFKUTA7P9y", true) },
            { "NodeDB", new NodeWallet("192.168.98.131", "Wallet-DBA", "venue market forest replace moment side sell nominee pretty choose vocal young", "node", "TD33Q4PvfoKZV7wpM31ei7bjCXBq4xQteK", true) },
            { "NodeEA", new NodeWallet("192.168.98.140", "Wallet-EAA", "adjust step kind head save profit push joy question upon proof police", "node", "TRB4ynV6kPUZEp53HghgBeL8BxVGyWGVum", true) },
            { "NodeFA", new NodeWallet("192.168.98.150", "Wallet-FAA", "honey patch blouse rug car work test any fortune kiwi trade hobby", "node", "TYW5px1h5bX1RWKa6G6DLk9ZpVp6TBKijY") },
            { "NodeFB", new NodeWallet("192.168.98.151", "Wallet-FBA", "stage maze topic radio return metal tell profit edit corn arm diary", "node", "TLAU7QkpKd1jRf6EXQHJCVasLRvJnt2ASD", true) },
            { "NodeFC", new NodeWallet("192.168.98.152", "Wallet-FCA", "baby exile blame focus fiction smooth visa whip chimney enact amount pond", "node", "TVKNvwsGwTebUiTx11yAzzh8if4ev82b72", true) },
            { "NodeFD", new NodeWallet("192.168.98.153", "Wallet-FDA", "split phone produce found judge armor tornado adult security stairs tourist script", "node", "TFecdmTejH2hcbuwhypDdcRFgNjrA3uaRw", true) },
            { "NodeGA", new NodeWallet("192.168.98.160", "Wallet-GAA", "once wood keep mean oak suggest great alter differ tumble kit drop", "node", "TKQH8CZ6s4ib5LP2ZNosFZVvk1DusC4UmZ") },
            { "NodeGE", new NodeWallet("192.168.98.164", "Wallet-GEA", "wave glass song present memory amount smoke obey mansion joy icon battle", "node", "TDV4RjT6RM1Y8gBEuuc4YW7xJykWNcPMDq", true) },
            { "NodeGB", new NodeWallet("192.168.98.161", "Wallet-GBA", "explain monster mix drip acquire two senior riot shy raw canal table",  "node", "TP1N8hfsNVZDHHc8PDheCdubu2R2hH1ozc") },
            { "NodeGC", new NodeWallet("192.168.98.162", "Wallet-GCA", "health attract property still funny later whip mass aerobic tank glad salute", "node", "TRa7isTMB34wG9M9FPpWKwweav8gRjUVC2") },
            { "NodeGD", new NodeWallet("192.168.98.163", "Wallet-GDA", "riot game fire exist verb clutch swim kidney ill distance utility high", "node", "TAmg8tLj5fCT6F3DCaCypWNJy1qV4Adc1Z") },
            { "NodeHA", new NodeWallet("192.168.98.170", "Wallet-HAA", "youth breeze lab wide jar scene nephew mule open ladder cream define", "node", "TUWrMboRNKUx6phvm4p92z64BWvobhPbqh") },
            { "NodeHB", new NodeWallet("192.168.98.171", "Wallet-HBA", "neutral course control razor help blood grain devote father naive job grocery", "node", "TExXDEpxVqJtNbA6ew8iiex9yUZP8CqeBq") },
            { "NodeHC", new NodeWallet("192.168.98.172", "Wallet-HCA", "anger scene cinnamon design grape humor adjust world seed appear frame message", "node", "TKnv7hJdHxQJpuw9x5ixt9vUZ8G7exk8kJ") },
            { "NodeHD", new NodeWallet("192.168.98.173", "Wallet-HDA", "general ramp ramp transfer wheat page matter unit fiction rocket copper hunt", "node", "TYXoySmFU73oRYXA74M6cKdRjShi5KKLdz") },
            { "NodeIA", new NodeWallet("192.168.98.180", "Wallet-IAA", "nerve panic cabbage glide main wise castle universe oblige midnight giggle deliver", "node", "TK9mo7eqAf96SfFsFptVmtRHDYgfbgu7Ph", true) },
            { "NodeIB", new NodeWallet("192.168.98.181", "Wallet-IBA", "lady faith dish indicate win always office lyrics trend virus audit thought", "node", "TRftTFjV3ghdSbU9zi5bMMaNW7wDdBnndH") },
            { "NodeIE", new NodeWallet("192.168.98.184", "Wallet-IEA", "flame window trust dragon leave top under install strong project casual open", "node", "TYexb2QTCzk2cmzVCd85RQzgsQqL3yvhV3", true) },
            { "NodeIC", new NodeWallet("192.168.98.182", "Wallet-ICA", "veteran reward lend shed huge again phrase budget universe next tired scrub", "node", "TKZypKhRLyWtEt62cAiS7C972Wrj64EfvL") },
            { "NodeID", new NodeWallet("192.168.98.183", "Wallet-IDA", "swarm report tourist depth remain access path burger report patch drink lesson", "node", "TKFAnsKKhkxpXqxB56Cc1njTvULeHBiW4T") },
            { "NodeJA", new NodeWallet("192.168.98.190", "Wallet-JAA", "struggle image hub release erosion ahead rhythm laugh doll stick puppy gravity", "node", "TUurVanRDjs1otSTx5yAhmPsYEyMH2Ddwc") },
            { "NodeJB", new NodeWallet("192.168.98.191", "Wallet-JBA", "absurd rent half clap later wheat brother team tonight license wage beef", "node", "TTQyg35jy31iHDpd6cvgxQLbJ9j7o3V1XU") },
            { "NodeJC", new NodeWallet("192.168.98.192", "Wallet-JCA", "hidden quantum ready stuff twenty ecology roast purchase frame visual effort daring", "node", "TGWKsxmJVGLeaiJBWqgyWTYoErmuQcVwsA", true) },
            { "NodeJD", new NodeWallet("192.168.98.193", "Wallet-JDA", "giraffe undo oven age hill fringe share game filter manage inner clerk", "node", "TLHeEAVRoMrciXT7Uq7VvUktfL9S9tR1Um") },
            { "NodeJE", new NodeWallet("192.168.98.194", "Wallet-JEA", "credit balance winner item dance wife air solve soon slight song hungry", "node", "TAn1iSbffZJpBTcXTt2cy688uAxhcYUQut") },
            { "NodeJF", new NodeWallet("192.168.98.195", "Wallet-JFA", "elite gain smooth avoid boil genre series right ketchup mutual pigeon depend", "node", "TT7GM81HpmDTS9zi11nny1Yf5rp2UZwCZV") }
        };

        internal class NodeWallet
        {
            public NodeWallet(string ipAddress, string walletName, string menmonicPhrase, string password, string transactionAddress, bool hasFunds = false)
            {
                this.IPAddress = ipAddress;
                this.WalletName = walletName;
                this.MnemonicPhrase = menmonicPhrase;
                this.Password = password;
                this.TransactionAddress = transactionAddress;
                this.HasFunds = hasFunds;
            }

            public string IPAddress { get; }

            public string WalletName { get; }

            public string MnemonicPhrase { get; }

            public string Password { get; }

            public string TransactionAddress { get; }

            public bool HasFunds { get; }
        }
    }
}
