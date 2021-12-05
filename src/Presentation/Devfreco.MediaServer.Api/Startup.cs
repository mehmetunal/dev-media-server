using Dev.Core.IO;
using Dev.Core.IoC;
using Dev.Framework.Exceptions;
using Dev.Framework.Extensions;
using Dev.Framework.Helper.ModelStateResponseFactory;
using Dev.Framework.Security.Model;
using Dev.Mongo.Extensions;
using Dev.Mongo.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Dev.Services;

namespace Devfreco.MediaServer
{
    public class Startup
    {
        protected string SwaggerTitle = "My API";
        protected string SwaggerVersion = "v1";
        protected ApiTokenOptions TokenOptions;
        protected IConfiguration Configuration { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            SwaggerTitle = Configuration.GetSection("SwaggerTitle")?.Value;
            SwaggerVersion = $"v{Configuration.GetSection("ApiVersion:MajorVersion")?.Value}";
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var tokenOptionsConfiguration = Configuration.GetSection("TokenOptions");

            services.Configure<ApiTokenOptions>(tokenOptionsConfiguration);

            TokenOptions = tokenOptionsConfiguration.Get<ApiTokenOptions>();

            services.AddControllers().AddJsonOptionsConfig();
            
            services.AddMvc();

            services.AddControllersWithViews();
            
            services.AddWebEncoders();

            services.AddAdminApiCors(TokenOptions);

            services.AddCors();

            services.AddApiVersioningConfig(Configuration);

            services.AddHttpContextAccessor();

            services.AddSwaggerGenConfig(TokenOptions);
            
            services.RegisterAll<IService>();

            services.Configure<ApiBehaviorOptions>(options => { options.InvalidModelStateResponseFactory = ctx => new ModelStateFeatureFilter(); });

            services.AddMongoDbConfig(Configuration);

            services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
            services.AddScoped(typeof(IGridFsRepository), typeof(GridFsRepository));

            services.AddSingleton<IFileManagerProviderBase>(
                new FileManagerProviderBase(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

            services.AddScoped<IFilesManager, FilesManager>();

            services.AddScoped<IDevFileProvider, DevFileProvider>();

            // If using Kestrel:
            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            // If using IIS:
            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddHostedService<QueueService>();
            services.AddSingleton<IBackgroundQueueService, BackgroundQueueService>();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.IsLocal())
                    {
                        // Forbidden http status code
                        context.Response.StatusCode = 403;
                        return;
                    }

                    await next.Invoke();
                });
            }

            app.UseStaticFiles();
            
            app.UseSwaggerUIConfig(TokenOptions);

            app.UseRouting();

            app.UseCorsConfig();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.ConfigureRequestPipeline();
        }
    }
}