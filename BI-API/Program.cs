using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Bimasakti.BiService.Api.Services;
using Bimasakti.BiService.Api.Services.Engines;
using Bimasakti.BiService.Api.Models;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Api.Controllers;
using Scalar.AspNetCore;

namespace Bimasakti.BiService.Api
{
    public class Program
    {
        private static readonly System.Net.Http.HttpClient SharedHttpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };


        public static async Task Main(string[] args)
        {


            EnsureJwtSecret();

            string secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("CRITICAL: Missing JWT_SECRET");
            if (secretKey.Length < 32) throw new Exception("CRITICAL: JWT_SECRET too short");

            var (host, ports) = GetServerConfig();

            var builder = WebApplication.CreateBuilder(args);

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")))
            {
                var urls = ports.Select(p => $"http://{host}:{p}").ToArray();
                builder.WebHost.UseUrls(urls);
            }

            ConfigureServices(builder, secretKey);

            var app = builder.Build();

            ConfigurePipeline(app);

            await app.RunAsync();
        }



        private static (string host, int[] ports) GetServerConfig()
        {
            string host = "0.0.0.0";
            var portsList = new List<int> { 8001 };
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("Server", out var server))
                    {
                        if (server.TryGetProperty("Host", out var h)) host = h.GetString() ?? host;
                        
                        if (server.TryGetProperty("Ports", out var pArray) && pArray.ValueKind == JsonValueKind.Array)
                        {
                            portsList.Clear();
                            foreach (var item in pArray.EnumerateArray())
                            {
                                if (item.TryGetInt32(out var intP)) portsList.Add(intP);
                            }
                        }
                        else if (server.TryGetProperty("Port", out var p) && p.TryGetInt32(out var parsedPort))
                        {
                            portsList.Clear();
                            portsList.Add(parsedPort);
                        }
                    }
                }
                catch { /* Use defaults */ }
            }
            
            if (portsList.Count == 0) portsList.Add(8001);
            return (host, portsList.Distinct().ToArray());
        }

        private static void ConfigureServices(WebApplicationBuilder builder, string secretKey)
        {
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            int accessExpireMinutes = builder.Configuration.GetValue<int>("Jwt:AccessTokenExpireMinutes", 15);
            int cookieExpireHours = builder.Configuration.GetValue<int>("Jwt:CookieExpireHours", 1);

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = async context =>
                    {
                        if (context.Request.Cookies.TryGetValue("access_token", out var token) && !string.IsNullOrEmpty(token))
                        {
                            try
                            {
                                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                                var valParams = options.TokenValidationParameters.Clone();
                                valParams.ValidateLifetime = false;

                                var principal = handler.ValidateToken(token, valParams, out var validated);
                                if (validated is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt && jwt.ValidTo < DateTime.UtcNow)
                                {
                                    // Auto-renew expired token if cookie is still alive
                                    var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);

                                    if (claims.TryGetValue(System.Security.Claims.ClaimTypes.Name, out var user) &&
                                        claims.TryGetValue("company_id", out var compId))
                                    {
                                        bool isCompanyActive = false;
                                        try
                                        {
                                            string centralDbPath = builder.Configuration.GetValue<string>("Config:CentralDbPath", "");
                                            if (string.IsNullOrEmpty(centralDbPath))
                                            {
                                                string assetsDir = DbUtils.GetAssetsDirectory();
                                                centralDbPath = Path.Combine(assetsDir, "BMS_BI_Central.db");
                                            }
                                            else
                                            {
                                                centralDbPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, centralDbPath));
                                            }

                                            if (System.IO.File.Exists(centralDbPath))
                                            {
                                                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={centralDbPath}");
                                                await connection.OpenAsync();
                                                using var command = connection.CreateCommand();
                                                command.CommandText = "SELECT is_active FROM companies WHERE company_id = @companyId LIMIT 1;";
                                                command.Parameters.AddWithValue("@companyId", compId);
                                                var result = await command.ExecuteScalarAsync();
                                                if (result != null && result != DBNull.Value)
                                                {
                                                    isCompanyActive = Convert.ToInt32(result) > 0;
                                                }
                                            }
                                        }
                                        catch { }
                                        if (!isCompanyActive) { context.Fail("Inactive company."); return; }

                                        using var tDb = new CompanyDbContext(DbUtils.GetSafeDbPath(compId));
                                        var dbUser = await tDb.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == user.ToUpper() && u.CompanyId.ToUpper() == compId.ToUpper());
                                        if (dbUser == null || !dbUser.IsActive) { context.Fail("Inactive user."); return; }

                                        var authSvc = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationService>();
                                        string newToken = authSvc.CreateAccessToken(claims, secretKey, accessExpireMinutes);

                                        context.Response.Cookies.Append("access_token", newToken, new CookieOptions
                                        {
                                            HttpOnly = true,
                                            Secure = context.HttpContext.Request.IsHttps,
                                            SameSite = SameSiteMode.Lax,
                                            MaxAge = TimeSpan.FromHours(cookieExpireHours),
                                            Path = "/"
                                        });
                                        context.Token = newToken;
                                    }
                                }
                                else { context.Token = token; }
                            }
                            catch { context.Token = token; }
                        }
                    }
                };
            });

            // Register Services
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IsvcGlrx0310, svcGlrx0310>();
            builder.Services.AddScoped<IsvcCbrx7000, svcCbrx7000>();
            builder.Services.AddScoped<IDashboardAnalyticsService, DashboardAnalyticsService>();
            builder.Services.AddScoped<IsvcAprx0100, svcAprx0100>();
            builder.Services.AddScoped<IsvcArrx0100, svcArrx0100>();
            builder.Services.AddScoped<IsvcCbrx0200, svcCbrx0200>();
            builder.Services.AddScoped<IsvcIcrx0700, svcIcrx0700>();
            builder.Services.AddScoped<IsvcLmrx0220, svcLmrx0220>();
            builder.Services.AddScoped<IsvcLmrx0400, svcLmrx0400>();
            builder.Services.AddScoped<IsvcLmrx0710, svcLmrx0710>();
            builder.Services.AddScoped<IsvcLmrx0800, svcLmrx0800>();
            builder.Services.AddScoped<IsvcLmrx1000, svcLmrx1000>();
            builder.Services.AddScoped<IsvcLmrx2100, svcLmrx2100>();
            builder.Services.AddScoped<IsvcPjrx0100, svcPjrx0100>();
            builder.Services.AddScoped<IsvcPjrx1100, svcPjrx1100>();
            builder.Services.AddScoped<IsvcSsrx7100, svcSsrx7100>();
            builder.Services.AddScoped<IsvcSsrx7200, svcSsrx7200>();


            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").GetChildren().Select(c => c.Value).OfType<string>().ToArray();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BimasaktiCorsPolicy", policy =>
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrEmpty(origin)) return false;
                        if (allowedOrigins.Contains(origin)) return true;
                        try { var uri = new Uri(origin); return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "0.0.0.0" || uri.Host.StartsWith("192.168."); }
                        catch { return false; }
                    }).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                });
            });
        }

        private static void ConfigurePipeline(WebApplication app)
        {
            if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseCors("BimasaktiCorsPolicy");

            // ISO 27001 & INDO LAW SAFE Middleware
            app.Use(async (context, next) =>
            {
                var wibZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");
                var wibTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wibZone);

                context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Append("Content-Security-Policy", "default-src 'self';");
                context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

                string path = context.Request.Path.Value ?? "";
                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
                string qs = context.Request.QueryString.Value ?? "";

                // Indo Law PII Masking & XSS Sanitization Logging
                if (qs.Contains("@") || qs.Contains("ktp") || qs.Contains("phone") || qs.Contains("<script>"))
                {
                    qs = "?[MASKED_PII_OR_SANITIZED]";
                }

                Console.WriteLine($"[ACCESS - {wibTime:yyyy-MM-dd HH:mm:ss} WIB] {ip} {context.Request.Method} {path}{qs}");
                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            string assetsPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "assets"));
            Directory.CreateDirectory(assetsPath);
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assetsPath), RequestPath = "/assets" });

            app.MapControllers();

            app.UseSwagger();
            app.MapScalarApiReference("/docs", options => options.WithTitle("Bimasakti Financial Reports API Reference").WithTheme(ScalarTheme.Moon).WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
        }

        private static void EnsureJwtSecret()
        {
            string? key = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (!string.IsNullOrEmpty(key) && key.Length >= 32) return;

            string envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envPath))
            {
                try
                {
                    var match = File.ReadAllLines(envPath).FirstOrDefault(l => l.Trim().StartsWith("JWT_SECRET="));
                    if (match != null && match.Length > 40)
                    {
                        Environment.SetEnvironmentVariable("JWT_SECRET", match.Split('=')[1].Trim('"', '\'', ' '));
                        return;
                    }
                }
                catch { }
            }

            string newSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)).Replace("/", "_").Replace("+", "-").TrimEnd('=');
            try
            {
                File.AppendAllText(envPath, $"\nJWT_SECRET=\"{newSecret}\"");
                Environment.SetEnvironmentVariable("JWT_SECRET", newSecret);
                Console.WriteLine($"[Config] Generated secure JWT_SECRET and saved to {envPath}");
            }
            catch
            {
                Environment.SetEnvironmentVariable("JWT_SECRET", newSecret);
                Console.WriteLine("[Config] Warning: Failed to write .env. Using in-memory secret.");
            }
        }
    }
}
