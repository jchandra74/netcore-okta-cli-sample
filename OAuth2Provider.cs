using System;
using System.Web;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

namespace cliplay
{
    public class OAuth2Provider
    {
        private readonly OAuth2Configuration _configuration;
        private readonly IHttpClientFactory _factory;

        public OAuth2Provider(
            IOptions<OAuth2Configuration> options, 
            IHttpClientFactory factory)
        {
            _factory = factory;

            //Can't figure out why IOptions is not working for injecting the configuration when called from the OAuth2Controller...
            //It worked just fine when called from Program.Main flow
            
            //HACK!! For now, just use the static cached variable in Program.OAuth2Configuration as fallback....
            var config = options.Value;
            _configuration = (string.IsNullOrWhiteSpace(config.AuthServer)) ? Program.OAuth2Configuration : config;

        }

        public async Task<OAuth2AccessTokenResponse> GetOAuth2AccessTokenFor(string responseCode)
        {  
            var request = new HttpRequestMessage(HttpMethod.Post, _configuration.Metadata.TokenEndpoint);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "authorization_code",
                ["code"] = responseCode,
                ["redirect_uri"] = "http://localhost:8080/authorization-code/callback",
                ["client_id"] = _configuration.ClientId,
                ["client_secret"] = _configuration.ClientSecret
            });
            request.Headers.Add("Accept", "application/json");

            var client = _factory.CreateClient("okta");
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("GetOAuth2AccessTokenFor failed.");

            var result = await JsonSerializer.DeserializeAsync<OAuth2AccessTokenResponse>(await response.Content.ReadAsStreamAsync());

            //Console.WriteLine(result.AccessToken);
            //Console.WriteLine(result.ExpiresIn);
            //Console.WriteLine(result.TokenType);

            return result;
        }

        public async Task<OAuth2IntrospectResponse> GetOAuth2IntrospectionResultFor(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _configuration.Metadata.IntrospectionEndpoint);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["token"] = accessToken,
                ["client_id"] = _configuration.ClientId,
                ["client_secret"] = _configuration.ClientSecret
            });            
            request.Headers.Add("Accept", "application/json");
            var client = _factory.CreateClient("okta");
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("GetOAuth2IntrospectionResultFor failed.");

            var result = await JsonSerializer.DeserializeAsync<OAuth2IntrospectResponse>(await response.Content.ReadAsStreamAsync());

            //Console.WriteLine(result.UserName);
            //Console.WriteLine(result.Active);

            return result;
        }

        public Uri GetAuthorizationTokenUri(string state)
        {
            var collection = HttpUtility.ParseQueryString(string.Empty);
            collection["response_type"] = "code";
            collection["client_id"] = _configuration.ClientId;
            collection["redirect_uri"] = "http://localhost:8080/authorization-code/callback";
            collection["state"] = state;
            collection["scope"] = "openid";

            var uri = new UriBuilder(_configuration.Metadata.AuthorizationEndpoint) { Query = collection.ToString() }.Uri;

            //Console.WriteLine(uri.ToString());
            
            return uri;
        }
    }
}