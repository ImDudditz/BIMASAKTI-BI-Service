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
using System.Net;
using System.Net.Sockets;
using BiPortal.FinancialReports.Backend.Services;
using Scalar.AspNetCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using BiPortal.FinancialReports.Backend.Engines;
using Microsoft.EntityFrameworkCore;

namespace BiPortal.FinancialReports.Backend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            EnsureJwtSecret();
            if (args.Length > 0 && args[0] == "--sync")
            {
                string companyId = args.Length > 1 ? args[1] : "ASHMD";
                Console.WriteLine($"Starting standalone database sync for company: {companyId}...");
                var syncService = new svcDatabaseSyncService();
                var (success, message) = await syncService.SyncCompanyDatabaseAsync(companyId);
                if (success)
                {
                    Console.WriteLine($"Sync Success: {message}");
                    Environment.Exit(0);
                }
                else
                {
                    Console.Error.WriteLine($"Sync Failed: {message}");
                    Environment.Exit(1);
                }
            }

            string? secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
            {
                Console.Error.WriteLine("CRITICAL: The JWT_SECRET environment variable is missing or insufficiently secure (must be at least 32 characters long). The application will not start.");
                Environment.Exit(1);
            }

            // Dynamically resolve an available port from appsettings.json or automatic scanning
            string host = "0.0.0.0";
            int startingPort = 8001;
            string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (File.Exists(appSettingsPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(appSettingsPath);
                    var rootNode = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    if (rootNode != null)
                    {
                        var serverNode = rootNode["Server"];
                        if (serverNode != null)
                        {
                            host = serverNode["Host"]?.ToString() ?? "0.0.0.0";
                            if (int.TryParse(serverNode["Port"]?.ToString(), out var parsedPort))
                            {
                                startingPort = parsedPort;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fallback to default
                }
            }

            int port = GetAvailablePort(host, startingPort);
            Console.WriteLine($"[Server Startup] Binding to host {host} on auto-detected available port {port}");

            // Overwrite selected port in appsettings.json so frontend Vite can dynamically read it
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(appSettingsPath);
                    var rootNode = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    if (rootNode != null)
                    {
                        var serverObj = rootNode["Server"] as System.Text.Json.Nodes.JsonObject;
                        if (serverObj == null)
                        {
                            serverObj = new System.Text.Json.Nodes.JsonObject();
                            rootNode["Server"] = serverObj;
                        }
                        serverObj["Host"] = host;
                        serverObj["Port"] = port;

                        var writeOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        string updatedJson = rootNode.ToJsonString(writeOptions);
                        File.WriteAllText(appSettingsPath, updatedJson);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server Startup] Warning: Failed to write selected port to appsettings.json: {ex.Message}");
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

            // 1.5 Register Authentication
            int accessTokenExpireMinutes = builder.Configuration.GetValue<int>("Jwt:AccessTokenExpireMinutes", 15);
            int cookieExpireHours = builder.Configuration.GetValue<int>("Jwt:CookieExpireHours", 1);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
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
                
                // Extract token from HttpOnly cookie and renew if mathematically valid but expired
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = async context =>
                    {
                        if (context.Request.Cookies.ContainsKey("access_token"))
                        {
                            var token = context.Request.Cookies["access_token"];
                            if (!string.IsNullOrEmpty(token))
                            {
                                try
                                {
                                    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                                    var validationParameters = new TokenValidationParameters
                                    {
                                        ValidateIssuerSigningKey = true,
                                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                                        ValidateIssuer = false,
                                        ValidateAudience = false,
                                        ValidateLifetime = false, // Relax lifetime validation to check signature/claims first
                                        ClockSkew = TimeSpan.Zero
                                    };

                                    // Validates the signature and security of the token strictly
                                    var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                                    var jwtToken = validatedToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

                                    if (jwtToken != null && jwtToken.ValidTo < DateTime.UtcNow)
                                    {
                                        // The JWT signature is mathematically valid, but the 15-minute token has expired.
                                        // Since the browser still sent the HttpOnly cookie, the 1-hour cookie session is still alive!
                                        // Let's perform a silent auto-renewal of the token.
                                        var claimMap = new Dictionary<string, string>();
                                        foreach (var claim in principal.Claims)
                                        {
                                            claimMap[claim.Type] = claim.Value;
                                        }

                                        // SECURITY: Verify if the user account still exists and is active in the database
                                        if (claimMap.TryGetValue(System.Security.Claims.ClaimTypes.Name, out var username) &&
                                            claimMap.TryGetValue("company_id", out var userCompanyId))
                                        {
                                            string userDbPath = svcDbUtils.GetSafeDbPath(userCompanyId);
                                            using (var dbContext = new TenantDbContext(userDbPath))
                                            {
                                                var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == username.ToUpper() && u.CompanyId.ToUpper() == userCompanyId.ToUpper());
                                                if (dbUser == null || !dbUser.IsActive)
                                                {
                                                    context.Fail("User account is deactivated or does not exist.");
                                                    return;
                                                }
                                            }
                                        }

                                        var authService = context.HttpContext.RequestServices.GetRequiredService<IsvcAuthenticationService>();
                                        string newJsonWebToken = authService.CreateAccessToken(claimMap, secretKey, accessTokenExpireMinutes);

                                        // Append renewed token back to cookies with 1 hour lifetime
                                        context.Response.Cookies.Append("access_token", newJsonWebToken, new CookieOptions
                                        {
                                            HttpOnly = true,
                                            Secure = true,
                                            SameSite = SameSiteMode.Lax,
                                            MaxAge = TimeSpan.FromHours(cookieExpireHours),
                                            Path = "/"
                                        });

                                        context.Token = newJsonWebToken;
                                    }
                                    else
                                    {
                                        // Token is still valid (not yet expired)
                                        context.Token = token;
                                    }
                                }
                                catch (Exception)
                                {
                                    // Token signature is invalid or tampered with. Pass it to the standard validator
                                    // which will trigger the standard 401 Unauthorized response flow.
                                    context.Token = token;
                                }
                            }
                        }
                    }
                };
            });

            // 2. Register Application Business Services for Dependency Injection
            builder.Services.AddScoped<IsvcAuthenticationService, svcAuthenticationService>();
            builder.Services.AddScoped<IsvcGlrx0310, svcGlrx0310>();
            builder.Services.AddScoped<IsvcCbrx7000, svcCbrx7000>();
            builder.Services.AddScoped<IsvcDashboardAnalyticsService, svcDashboardAnalyticsService>();
            builder.Services.AddScoped<IsvcDatabaseSyncService, svcDatabaseSyncService>();

            // 3. Configure CORS policy based on appsettings.json (supports HttpOnly credentials/cookies)
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
                .GetChildren()
                .Select(c => c.Value)
                .OfType<string>()
                .ToArray();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BimasaktiCorsPolicy", policy =>
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrEmpty(origin)) return false;
                        
                        // Trust standard configured origins
                        if (allowedOrigins.Contains(origin)) return true;
                        
                        try
                        {
                            var uri = new Uri(origin);
                            // Dynamically trust any loopback/local developer origin on any port
                            return uri.Host == "localhost" || 
                                   uri.Host == "127.0.0.1" || 
                                   uri.Host == "0.0.0.0" || 
                                   uri.Host.StartsWith("192.168.");
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
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

            app.UseAuthentication();
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

            // 8. Launch C# backend dynamically on the resolved host and port
            app.Run($"http://{host}:{port}");
        }

        private static int GetAvailablePort(string host, int startingPort)
        {
            int port = startingPort;
            while (port < 65535)
            {
                if (IsPortAvailable(host, port))
                {
                    return port;
                }
                port++;
            }
            throw new Exception("No available port found.");
        }

        private static bool IsPortAvailable(string host, int port)
        {
            try
            {
                var ipAddress = host == "0.0.0.0" ? System.Net.IPAddress.Any : System.Net.IPAddress.Parse(host);
                var listener = new System.Net.Sockets.TcpListener(ipAddress, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
        }

        private static void EnsureJwtSecret()
        {
            string? secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (!string.IsNullOrEmpty(secretKey) && secretKey.Length >= 32)
            {
                return;
            }

            // Look for a .env file
            string[] potentialPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "backend", ".env"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".env"), // walk up from bin/Debug/net8.0
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", ".env")
            };

            string? foundEnvPath = null;
            foreach (var path in potentialPaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        foundEnvPath = fullPath;
                        break;
                    }
                }
                catch { }
            }

            if (foundEnvPath != null)
            {
                try
                {
                    var lines = File.ReadAllLines(foundEnvPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("JWT_SECRET="))
                        {
                            var val = trimmed.Substring("JWT_SECRET=".Length).Trim('"', '\'', ' ');
                            if (val.Length >= 32)
                            {
                                Environment.SetEnvironmentVariable("JWT_SECRET", val);
                                Console.WriteLine($"[Config] Loaded JWT_SECRET from .env file: {foundEnvPath}");
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Config] Warning: Failed to read .env file at {foundEnvPath}: {ex.Message}");
                }
            }

            // If we are here, we need to generate one
            // Determine where to save it: prefer backend directory or current working directory
            string targetEnvPath;
            string currentDir = Directory.GetCurrentDirectory();
            if (Directory.Exists(Path.Combine(currentDir, "backend")))
            {
                targetEnvPath = Path.Combine(currentDir, "backend", ".env");
            }
            else if (Path.GetFileName(currentDir).Equals("backend", StringComparison.OrdinalIgnoreCase))
            {
                targetEnvPath = Path.Combine(currentDir, ".env");
            }
            else
            {
                // Fallback: check if we can write to the base directory's ancestor (which is the actual project backend dir)
                string root = AppDomain.CurrentDomain.BaseDirectory;
                // Go up from bin/Debug/net8.0 to backend root
                string possibleBackend = Path.GetFullPath(Path.Combine(root, "..", "..", ".."));
                if (Directory.Exists(possibleBackend) && Path.GetFileName(possibleBackend).Equals("backend", StringComparison.OrdinalIgnoreCase))
                {
                    targetEnvPath = Path.Combine(possibleBackend, ".env");
                }
                else
                {
                    targetEnvPath = Path.Combine(currentDir, ".env");
                }
            }

            try
            {
                string newSecret = GenerateSecureSecret(48); // 48 bytes is 64 base64 characters
                
                // Read existing lines if any, or create a new file
                List<string> fileLines = new();
                if (File.Exists(targetEnvPath))
                {
                    fileLines.AddRange(File.ReadAllLines(targetEnvPath));
                }

                bool replaced = false;
                for (int i = 0; i < fileLines.Count; i++)
                {
                    if (fileLines[i].Trim().StartsWith("JWT_SECRET="))
                    {
                        fileLines[i] = $"JWT_SECRET=\"{newSecret}\"";
                        replaced = true;
                        break;
                    }
                }

                if (!replaced)
                {
                    fileLines.Add($"JWT_SECRET=\"{newSecret}\"");
                }

                File.WriteAllLines(targetEnvPath, fileLines);
                Environment.SetEnvironmentVariable("JWT_SECRET", newSecret);
                Console.WriteLine($"[Config] Successfully generated a secure JWT_SECRET and saved to {targetEnvPath}");
            }
            catch (Exception ex)
            {
                // Fallback to in-memory generation if writing file fails
                string fallbackSecret = GenerateSecureSecret(48);
                Environment.SetEnvironmentVariable("JWT_SECRET", fallbackSecret);
                Console.WriteLine($"[Config] Warning: Failed to save generated JWT_SECRET to file: {ex.Message}. Using in-memory secret.");
            }
        }

        private static string GenerateSecureSecret(int length)
        {
            byte[] bytes = new byte[length];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .TrimEnd('=');
        }
    }
}
