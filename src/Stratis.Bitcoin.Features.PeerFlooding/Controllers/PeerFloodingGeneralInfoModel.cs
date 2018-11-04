using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.PeerFlooding.Controllers
{
    public class PeerFloodingGeneralInfoModel
    {
        [JsonProperty(PropertyName = "floodFilePath")]
        public string FloodFilePath { get; set; }

        [JsonProperty(PropertyName = "info")]
        public string Info { get; set; }
    }
}
