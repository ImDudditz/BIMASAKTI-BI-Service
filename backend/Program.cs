using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using BimasaktiReports.FinancialReports.Backend.Services;
using Scalar.AspNetCore;
using System.Threading.Tasks;

namespace BimasaktiReports.FinancialReports.Backend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--sync")
            {
                string companyId = args.Length > 1 ? args[1] : "ASHMD";
                Console.WriteLine($"Starting standalone database sync for company: {companyId}...");
                var syncService = new svcDatabaseSyncService();
                var result = await syncService.SyncCompanyDatabaseAsync(companyId);
                if (result.Success)
                {
                    Console.WriteLine($"Sync Success: {result.Message}");
                    Environment.Exit(0);
                }
                else
                {
                    Console.Error.WriteLine($"Sync Failed: {result.Message}");
                    Environment.Exit(1);
                }
            }

            var builder = WebApplication.CreateBuilder(args);

            // 1. Add Controllers with standard JSON camelCase formatting
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DictionaryKeyPolicy = null; // Preserve exact dictionary keys (like PascalCase section names)
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // 2. Register Application Business Services for Dependency Injection
            builder.Services.AddScoped<IsvcAuthenticationService, svcAuthenticationService>();
            builder.Services.AddScoped<IsvcGLRX0310, svcGLRX0310>();
            builder.Services.AddScoped<IsvcCBRX7000, svcCBRX7000>();
            builder.Services.AddScoped<IsvcDashboardAnalyticsService, svcDashboardAnalyticsService>();
            builder.Services.AddScoped<IsvcDatabaseSyncService, svcDatabaseSyncService>();

            // 3. Configure highly permissive CORS policy for development (supports HttpOnly credentials/cookies)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BimasaktiCorsPolicy", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true) // Permissive for development (allows any origin)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials(); // Essential for transmitting secure HttpOnly access_token cookies
                });
            });

            var app = builder.Build();

            // 4. Configure HTTP pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            // Apply permissive CORS rules before Authorization
            app.UseCors("BimasaktiCorsPolicy");

            app.UseAuthorization();

            // 5. Mount Static Assets under /assets route
            string assetsPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "assets"));
            if (!Directory.Exists(assetsPath))
            {
                Directory.CreateDirectory(assetsPath);
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(assetsPath),
                RequestPath = "/assets"
            });

            // 6. Map all Controllers
            app.MapControllers();

            // 7. Mount Scalar OpenAPI documentation UI at /docs, explicitly pointing to Swagger's endpoint
            app.UseSwagger();
            app.MapScalarApiReference("/docs", options =>
            {
                options.WithTitle("Bimasakti Financial Reports API Reference")
                       .WithTheme(ScalarTheme.Moon)
                       .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
            });

            // 8. Launch C# backend on port 8001
            app.Run("http://0.0.0.0:8001");
        }
    }
}
