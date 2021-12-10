using Dev.Core.IO;
using Dev.Core.IoC;
using Dev.Data.Mongo;
using Dev.Framework.Extensions;
using Dev.Framework.Helper.ModelStateResponseFactory;
using Dev.Framework.Security.Model;
using Dev.Mongo.Extensions;
using Dev.Mongo.Repository;
using Dev.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

namespace Devfreco.MediaServer
{
    public class Startup
    {
        protected string SwaggerTitle = "Devfreco Media Server API";
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
            Console.WriteLine("ConfigureServices START...");

            var tokenOptionsConfiguration = Configuration.GetSection("TokenOptions");

            services.Configure<ApiTokenOptions>(tokenOptionsConfiguration);
            var mediaSetting = Configuration.GetSection("MediaSetting");
            if (mediaSetting != null)
                services.AddSingleton(typeof(MediaSetting), mediaSetting.Get<MediaSetting>());

            // services.ConfigureStartupConfig<MediaSetting>(mediaSetting);
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
            
            services.AddScoped(typeof(IGridFsRepository), typeof(GridFsRepository));
            services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));

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
            Console.WriteLine("ConfigureServices END...");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Console.WriteLine("Configure START...");

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

            app.UseExceptionHandler(c => c.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerPathFeature>().Error;
                var response = new { error = exception.Message };
                await context.Response.WriteAsJsonAsync(response);
            }));

            app.UseStaticFiles();

            app.UseSwaggerUIConfig(TokenOptions);

            app.UseRouting();

            app.UseCorsConfig();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.ConfigureRequestPipeline();

            Console.WriteLine("Configure END...");
        }
    }
}