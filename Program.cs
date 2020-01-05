using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Web;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Extensions.Logging;


//https://docs.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-3.1&tabs=visual-studio-code
//try add Okta oauth2.... 
//basically, we need to pop up a browser so the user can login into Okta
//fire up a localhost:8080 web api to handle the callback from Okta at http://localhost:8080/authorization-code/callback
//get the response back and cache the token in ~/.smash/profile.json
//this one should have the username and when the token expires...
//if it expires, the tool should ask for reauthentication.
//okta is at ***REMOVED***  ***REMOVED***
namespace cliplay
{
    class Program
    {
        static void Main(string[] args)
        {
            var cleanupCmd = new Command("cleanup", "CleanUp") {
                new Option(new[] {"--subscription", "-s"}, "Subscription Name") { Argument = new Argument<string>() },
                new Option(new[] {"--environment", "-e"}, "InvoiceSmash Environment") { Argument = new Argument<string>() }
            };
            cleanupCmd.Handler = CommandHandler.Create<string, string>(Cleanup);

            var rootCommand = new RootCommand("My First CLI")
            {
                cleanupCmd
            };
            rootCommand.Handler = CommandHandler.Create(RootCommandHandler);

            rootCommand.InvokeAsync(args).GetAwaiter().GetResult();
        }

        public static void RootCommandHandler()
        {
            Console.WriteLine("My First CLI");
        }

        public static void Cleanup(string subscription, string environment)
        {
            if (string.IsNullOrWhiteSpace(subscription)) return;

            Console.WriteLine($"Clean up invoked for subscription: '{subscription}' in environment: '{environment}'.");

           if (!HasValidUserProfile()) 
           {
               // force authentication here...
               StartOAuthCallbackHandler();
           }
        }

        public static void StartOAuthCallbackHandler()
        {
            var collection = HttpUtility.ParseQueryString(string.Empty);
            collection["response_type"] = "code";
            collection["client_id"] = "***REMOVED***";
            collection["redirect_uri"] = "http://localhost:8080/authorization-code/callback";
            collection["state"] = $"{Guid.NewGuid()}";
            collection["scope"] = "openid";

            var uri = new UriBuilder("***REMOVED***/oauth2/default/v1/authorize") { Query = collection.ToString() }.Uri;
            Console.WriteLine("Authenticating to OKTA...");
            //Console.WriteLine(uri.ToString());
            OpenBrowser(uri.ToString());

            MiniHost = CreateHostBuilder(new [] {""}).Build();
            MiniHost.Run();

            Console.WriteLine("Continuing with clean up...");
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

    public class StartUp
    {
        public IConfiguration Configuration { get; }

        public StartUp(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services) 
        {
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
