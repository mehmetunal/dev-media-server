using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dev.Core.IO;
using Dev.Core.IoC;
using Dev.Framework.Exceptions;
using Dev.Framework.Extensions;
using Dev.Framework.Helper.ModelStateResponseFactory;
using Dev.Framework.Security.Model;
using Dev.Framework.Systems;
using Dev.Mongo.Extensions;
using Dev.Mongo.Repository;
using Devfreco.MediaServer.Mapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

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

            services.AddAdminApiCors(TokenOptions);

            services.AddApiVersioningConfig(Configuration);

            services.AddHttpContextAccessor();

            services.AddSwaggerGenConfig(TokenOptions);

            services.RegisterAll<IService>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.Configure<ApiBehaviorOptions>(options => { options.InvalidModelStateResponseFactory = ctx => new ModelStateFeatureFilter(); });

            services.AddAutoMapperConfig(p => p.AddProfile<AutoMapping>(), typeof(Startup));

            services.AddMongoDbConfig(Configuration);

            services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));

            services.RegisterAll<IService>();

            services.AddSingleton<IFileManagerProviderBase>(
                new FileManagerProviderBase(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

            services.AddScoped<IFilesManager, FilesManager>();

            services.AddScoped<IDevFileProvider, DevFileProvider>();
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

            //app.UseStatusCodePages(new StatusCodePagesOptions()
            //{
            //    HandleAsync = (ctx) =>
            //    {
            //        if (ctx.HttpContext.Response.StatusCode == 404)
            //        {
            //            throw new NotFoundException($"Not Found Page");
            //        }

            //        return Task.FromResult(0);
            //    }
            //});

            app.ConfigureRequestPipeline();

        }
    }
}