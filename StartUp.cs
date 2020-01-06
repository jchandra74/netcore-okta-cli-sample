
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace cliplay
{
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

            //Silence up the Console log a bit...
            services.AddLogging(config => {
                config.ClearProviders();
                config.AddConfiguration(Configuration.GetSection("Logging"));
                config.AddDebug();
                config.AddEventSourceLogger();
            });

            //The following does not work for whatever reason... frustrating...
            // services.Configure<OAuth2Configuration>(options => {
            //     Configuration.GetSection("Okta").Bind(options);
            //     options.Metadata = Program.LoadOAuth2Metadata(options).GetAwaiter().GetResult();
            //     });
            services.AddTransient<OAuth2Provider>();
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
