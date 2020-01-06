using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

namespace cliplay
{
    [ApiController]
    [Route("authorization-code")]
    public class OAuth2Controller : ControllerBase
    {
        private readonly OAuth2Provider _oauth2Provider;

        public OAuth2Controller(OAuth2Provider oauth2Provider)
        {
            _oauth2Provider = oauth2Provider;
        }

        [Route("callback")]
        public async Task<ActionResult> Callback([FromQuery] OAuth2Response response)
        {
            //Ensure the state we pass we got back in the callback to prevent injection attack
            if (response.State != Program.OAuth2RequestState) return BadRequest();

            var accessTokenResponse = await _oauth2Provider.GetOAuth2AccessTokenFor(response.Code);
            var introspectResponse = await _oauth2Provider.GetOAuth2IntrospectionResultFor(accessTokenResponse.AccessToken);

            Console.WriteLine($"Logged in as {introspectResponse.UserName}.");

            //TODO: Add code here to cache the token etc. into ~/.cliplay/profile.json

            //Hack to stop the Web Server Host fire and forget style
            //since we don't need it anymore after handling the callback.
            _ = Task.Run(async () => {
                await Program.OAuth2CallbackHost.StopAsync();
            }).ConfigureAwait(false);

            return Ok("You can close this browser now...");
        }
    }
}