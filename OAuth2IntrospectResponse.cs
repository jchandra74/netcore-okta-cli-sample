using System.Text.Json.Serialization;

namespace cliplay
{
    public class OAuth2IntrospectResponse
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("username")]
        public string UserName { get; set; }

        [JsonPropertyName("exp")]
        public long Exp { get; set; }
    }
}