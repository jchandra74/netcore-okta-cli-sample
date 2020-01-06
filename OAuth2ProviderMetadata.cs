using System.Text.Json.Serialization;

namespace cliplay
{
    public class OAuth2ProviderMetadata
    {
        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; }

        [JsonPropertyName("introspection_endpoint")]
        public string IntrospectionEndpoint { get; set; }
    }
}