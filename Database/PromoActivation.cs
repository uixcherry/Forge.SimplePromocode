using Newtonsoft.Json;
using System;

namespace Forge.SimplePromocode.Database
{
    public class PromoActivation
    {
        [JsonProperty("steam_id")]
        public ulong SteamId { get; set; }

        [JsonProperty("promo_name")]
        public string PromoName { get; set; }

        [JsonProperty("activation_date")]
        public DateTime ActivationDate { get; set; }
    }

    public class TemporaryActivation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("steam_id")]
        public ulong SteamId { get; set; }

        [JsonProperty("promo_name")]
        public string PromoName { get; set; }

        [JsonProperty("activation_date")]
        public DateTime ActivationDate { get; set; }

        [JsonProperty("expiry_date")]
        public DateTime ExpiryDate { get; set; }

        [JsonProperty("is_revoked")]
        public bool IsRevoked { get; set; }

        public TemporaryActivation()
        {
            Id = Guid.NewGuid().ToString("N");
            IsRevoked = false;
        }
    }
}