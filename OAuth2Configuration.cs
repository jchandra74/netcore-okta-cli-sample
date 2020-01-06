namespace cliplay
{
    public class OAuth2Configuration
    {
        public string HostName { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string AuthServer { get; set; }

        public OAuth2ProviderMetadata Metadata { get; set; }
    }
}
