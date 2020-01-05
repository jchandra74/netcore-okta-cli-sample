using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace cliplay
{
    [ApiController]
    [Route("authorization-code")]
    public class OAuth2Controller : ControllerBase
    {
        public OAuth2Controller()
        {
        }

        [Route("callback")]
        public ActionResult Callback([FromQuery] OAuth2Response rsp)
        {
            //Console.WriteLine(JsonSerializer.Serialize(rsp));

            // Check if rsp.State is the same as the request state... but how? expose it as public variable?

            var req = new HttpRequestMessage(HttpMethod.Post, "***REMOVED***/oauth2/default/v1/token");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "authorization_code",
                ["code"] = rsp.Code,
                ["redirect_uri"] = "http://localhost:8080/authorization-code/callback",
                ["client_id"] = "***REMOVED***",
                ["client_secret"] = "***REMOVED***"
            });
            req.Headers.Add("Accept", "application/json");

            var client = new HttpClient();
            var rsp2 = client.SendAsync(req).GetAwaiter().GetResult();

            var json = rsp2.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            //Console.WriteLine(json);

            var result = JsonSerializer.Deserialize<OAuthAccessTokenResponse>(json);
            //Console.WriteLine(result.AccessToken);
            //Console.WriteLine(result.ExpiresIn);
            //Console.WriteLine(result.TokenType);

            //Can call this endpoint to see if the access token is still active and it will return back the user info?
            //Can query the response Active prop to see if the user is still logged in?
            //We can use this to check if the user is still logged in?
            //Also to get the newly authenticated user info back (UserName property)
            var req2 = new HttpRequestMessage(HttpMethod.Post, "***REMOVED***/oauth2/default/v1/introspect");
            req2.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["token"] = result.AccessToken,
                ["client_id"] = "***REMOVED***",
                ["client_secret"] = "***REMOVED***"
            });            
            req2.Headers.Add("Accept", "application/json");

            var rsp3 = client.SendAsync(req2).GetAwaiter().GetResult();
            json = rsp3.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            //Console.WriteLine(json);
            var result2 = JsonSerializer.Deserialize<OAuthIntrospectResponse>(json);
            Console.WriteLine($"Logged in as {result2.UserName}.");
            //Console.WriteLine(result2.Active);

            Task.Run(async () => {
                await Program.MiniHost.StopAsync();
            });

            return Ok("You can close this browser now...");
        }
    }

    public class OAuthAccessTokenResponse
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }

    public class OAuthIntrospectResponse
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("username")]
        public string UserName { get; set; }

        [JsonPropertyName("exp")]
        public long Exp { get; set; }
    }

    public class OAuth2Response
    {
        public string Code { get; set; }
        public string State { get; set; }
    }
}