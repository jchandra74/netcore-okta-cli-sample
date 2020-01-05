using System;
using System.IO;
using System.Web;
using System.CommandLine;
using System.Diagnostics;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

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

            Services = new ServiceCollection()
                .AddHttpClient()
                .BuildServiceProvider();

            Okta = await InitializeOktaIntegration();

            return await SetupCommandLineParsing().InvokeAsync(args);
        }

        public static OktaConfiguration Okta;

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

        public static void RootCommandHandler()
        {
            Console.WriteLine("Okta enabled .NET Core CLI Sample");
        }

        public static int CleanupCommandHandler(string subscription, string environment)
        {
            if (string.IsNullOrWhiteSpace(subscription)) return -1;

            Console.WriteLine($"Clean up invoked for subscription: '{subscription}' in environment: '{environment}'.");

           if (!HasValidUserProfile()) 
           {
               // force authentication here...
               StartOAuthCallbackHandler();
           }

           return 0;
        }

        public static void StartOAuthCallbackHandler()
        {
            StartOktaAuthentication();
            
            MiniHost = CreateHostBuilder(new [] {""}).Build();
            MiniHost.Run();

            Console.WriteLine("Continuing with clean up...");
        }

        public static async Task<OktaConfiguration> InitializeOktaIntegration()
        {
            var config = LoadOktaConfiguration();

            config.Metadata = await LoadOktaMetadata(config);

            return config;
        }

        public static OktaConfiguration LoadOktaConfiguration()
        {
            return new OktaConfiguration {
                HostName = Configuration["Okta:HostName"],
                ClientId = Configuration["Okta:ClientId"],
                ClientSecret = Configuration["Okta:ClientSecret"],
                AuthServer = Configuration["Okta:AuthServer"]
            };
        }

        public static void StartOktaAuthentication()
        {
            var collection = HttpUtility.ParseQueryString(string.Empty);
            collection["response_type"] = "code";
            collection["client_id"] = Okta.ClientId;
            collection["redirect_uri"] = "http://localhost:8080/authorization-code/callback";
            collection["state"] = $"{Guid.NewGuid()}";
            collection["scope"] = "openid";

            var uri = new UriBuilder(Okta.Metadata.AuthorizationEndpoint) { Query = collection.ToString() }.Uri;
            Console.WriteLine("Authenticating to OKTA...");
            //Console.WriteLine(uri.ToString());
            OpenBrowser(uri.ToString());  
        }

        public static async Task<OktaMetadata> LoadOktaMetadata(OktaConfiguration okta)
        {
            //Should Prep Okta here... probably best to query the metadata endpint as shown in
            //https://developer.okta.com/blog/2018/07/16/oauth-2-command-line

            var factory = Services.GetService<System.Net.Http.IHttpClientFactory>();

            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{okta.HostName}/oauth2/{okta.AuthServer}/.well-known/oauth-authorization-server");

            var client = factory.CreateClient("okta");
            var rsp = await client.SendAsync(req);
            var json = await rsp.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<OktaMetadata>(json);
        }

        public static IHost MiniHost { get; set; }
    
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
                ".testapp/profile.json");

            if (!File.Exists(profilePath)) return false;

            return true;
        }
    }

    public class OktaConfiguration
    {
        public string HostName { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string AuthServer { get; set; }

        public OktaMetadata Metadata { get; set; }
    }
    
    public class StartUp
    {
        public IConfiguration Configuration { get; }

        public StartUp(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services) 
        {
            services.AddHttpClient();
            services.AddControllers();
            services.AddLogging(config => {
                config.ClearProviders();
                config.AddConfiguration(Configuration.GetSection("Logging"));
                config.AddDebug();
                config.AddEventSourceLogger();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }
    }
}
