using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.CommandLine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/*
GOALS:
1. Create a CLI in .NET Core that work similarly to the dotnet CLI, where you can call the app with specific command and paramters
    such as tool_name cleanup --subscription something --environment DEV
2. This CLI should be integrated with Okta where Okta is responsible for authenticating the user if there is currently no valid
    user logged in (which we will cache in the ~/.toolname/profile.json)
3. There will be a mini web server that is responsible to handle the OAUTH2 callback from OKTA.
    Once the request is handled and the authentication step is completed, it should shutdown itself and continue
    with whatever task the CLI command is supposed to do.
*/

namespace cliplay
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Debug.json", optional: true)
                .Build();

            //Has to do this here since there is no way that I know of to inject a ServiceProvider or ServiceCollection to
            //the OAuth2CallbackWebServer Startup.cs, so we have to maintain 2 DI container for now...?
            //Anyone know a better way to do this?
            Services = new ServiceCollection()
                .AddHttpClient()
                .Configure<OAuth2Configuration>(options => {
                    Configuration.GetSection("Okta").Bind(options);
                    options.Metadata = LoadOAuth2Metadata(options).GetAwaiter().GetResult();

                    //Cache it in static variable for use by the Mini Service.
                    OAuth2Configuration = options;
                })
                .AddTransient<OAuth2Provider>()
                .BuildServiceProvider();

            return await SetupCommandLineParsing().InvokeAsync(args);
        }

        // Hack... I don't like this but I can't figure out why Startup.cs can't configure this value.
        // So just going to configure it in Program.cs for now and then pass it into Startup...
        public static OAuth2Configuration OAuth2Configuration;

        //Exposing this as public so we can use it from OAuth2Controller later on to stop the Callback Webhost.
        public static IHost OAuth2CallbackHost { get; set; }

        //Exposing this as public so we can use it from OAuth2Controller later on.
        public static string OAuth2RequestState;

        public static IConfiguration Configuration;

        public static IServiceProvider Services;

        public static RootCommand SetupCommandLineParsing()
        {
            var cleanupCmd = new Command("cleanup", "CleanUp") {
                new Option(new[] {"--subscription", "-s"}, "Subscription Name") { Argument = new Argument<string>() },
                new Option(new[] {"--environment", "-e"}, "InvoiceSmash Environment") { Argument = new Argument<string>() }
            };
            cleanupCmd.Handler = CommandHandler.Create<string, string>(CleanupCommandHandler);

            var rootCommand = new RootCommand()
            {
                //Subcommands
                cleanupCmd
            };
            rootCommand.Handler = CommandHandler.Create(RootCommandHandler);

            return rootCommand;
        }

        public static Task RootCommandHandler()
        {
            Console.WriteLine("Okta enabled .NET Core CLI Sample");
            return Task.FromResult(0);
        }

        public static async Task<int> CleanupCommandHandler(string subscription, string environment)
        {
            if (string.IsNullOrWhiteSpace(subscription)) return -1;

            Console.WriteLine($"Clean up invoked for subscription: '{subscription}' in environment: '{environment}'.");

           if (!HasValidUserProfile()) 
           {
               // force authentication here...
               await StartOAuth2CallbackHandler();
           }

           return 0;
        }

        public static async Task StartOAuth2CallbackHandler()
        {
            Console.WriteLine("Starting OAuth2 Authentication...");
            
            StartOAuth2Authentication();
 
            OAuth2CallbackHost = CreateHostBuilder(new [] {""}).Build();
            await OAuth2CallbackHost.RunAsync();

            Console.WriteLine("Continuing with clean up...");
        }

        public static void StartOAuth2Authentication()
        {
            //Hacky... we need this in the OAuth2 Callback Web API to validate
            //So caching it in Program.OAuth2RequestState static variable
            //We'll use this in the Web API callback handler to see if the state matches.
            OAuth2RequestState = Guid.NewGuid().ToString();

            var oauth2Provider = Services.GetService<OAuth2Provider>();
            var uri = oauth2Provider.GetAuthorizationTokenUri(OAuth2RequestState);

            OpenBrowser(uri.ToString());
        }

        public static async Task<OAuth2ProviderMetadata> LoadOAuth2Metadata(OAuth2Configuration configuration)
        {
            //Don't go fetching it again if we already have it.
            if (configuration.Metadata != null) return configuration.Metadata;

            //Should Prep Okta here... probably best to query the metadata endpint as shown in
            //https://developer.okta.com/blog/2018/07/16/oauth-2-command-line

            var factory = Services.GetService<IHttpClientFactory>();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{configuration.HostName}/oauth2/{configuration.AuthServer}/.well-known/oauth-authorization-server");

            var client = factory.CreateClient("okta");
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("LoadOAuth2Metadata failed.");

            return await JsonSerializer.DeserializeAsync<OAuth2ProviderMetadata>(await response.Content.ReadAsStreamAsync());
        }
    
        //From https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
        public static void OpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    System.Diagnostics.Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    System.Diagnostics.Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    System.Diagnostics.Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => 
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configHost => {
                    configHost.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile("appsettings.Debug.json", optional: true);
                })
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.CaptureStartupErrors(true);
                    webBuilder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");
                    webBuilder.UseUrls("http://*:8080");
                    webBuilder.UseStartup<StartUp>();
                });

        public static bool HasValidUserProfile()
        {
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                ".cliplay/profile.json");

            if (!File.Exists(profilePath)) return false;

            //TODO: Add more stuff here..
            //I.e. to reduce call to the OAuth2 provider endpoint, we can check if the token has expired (the expiry timestamp should be cached in profile.json)
            //if it is, then call the inspection endpoint, if not, assume everything is still fine and just grab the username for logging...
            //I.e. call the OAuth2Provider inspection endpoint given existing access token that is found in the profile.json
            //and see if the user login has expired or not...
            //and return response.Active instead.

            return true;
        }
    }
}
