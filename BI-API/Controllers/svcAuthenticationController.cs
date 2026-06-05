using Bimasakti.BiService.Api.Controllers;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace Bimasakti.BiService.Api.Controllers
{
    // Models are defined in CompanyModels.cs

    // --- ROUTER / CLASS ---

    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class svcAuthenticationController : ControllerBase
    {
        private readonly IsvcAuthenticationService _authenticationService;
        private readonly IConfiguration _configuration;
        private static readonly string SecretKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET is missing.");

        private int AccessTokenExpireMinutes => _configuration.GetValue<int>("Jwt:AccessTokenExpireMinutes", 15);
        private int CookieExpireHours => _configuration.GetValue<int>("Jwt:CookieExpireHours", 1);

        public svcAuthenticationController(IsvcAuthenticationService authenticationService, IConfiguration configuration)
        {
            _authenticationService = authenticationService;
            _configuration = configuration;
        }

        public class LoginSpecification
        {
            [System.Text.Json.Serialization.JsonPropertyName("username")]
            public string Username { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("password")]
            public string Password { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("company_id")]
            public string CompanyId { get; set; } = "";
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginSpecification loginRequest)
        {
            try
            {
                string companyId = loginRequest.CompanyId.ToUpperInvariant();

                // Gatekeeper check: Ensure company is active in Central DB
                bool isCompanyActive = false;
                try
                {
                    using var hc = new System.Net.Http.HttpClient();
                    var response = await hc.GetAsync($"http://localhost:8003/api/internal/companies/{companyId}/status");
                    if (response.IsSuccessStatusCode)
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        if (doc.RootElement.TryGetProperty("isActive", out var p) && p.GetBoolean())
                        {
                            isCompanyActive = true;
                        }
                    }
                }
                catch { }

                if (!isCompanyActive)
                {
                    return Unauthorized(new { detail = "Company is inactive or does not exist." });
                }

                string databasePath = svcDbUtils.GetSafeDbPath(companyId);

                using (var dbContext = new CompanyDbContext(databasePath))
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == loginRequest.Username.ToUpper());

                    // Normal authentication with strict guard clause early return
                    if (user == null || !_authenticationService.VerifyPassword(loginRequest.Password, user.PasswordHash))
                    {
                        return Unauthorized(new { detail = "Incorrect username or password" });
                    }

                    // Strict active account guard check
                    if (!user.IsActive)
                    {
                        return BadRequest(new { detail = "Inactive user account" });
                    }

                    // Fetch company name dynamically
                    string companyName = "Unknown Company";
                    try
                    {
                        await dbContext.Database.OpenConnectionAsync();
                        using (var nameCommand = dbContext.Database.GetDbConnection().CreateCommand())
                        {
                            string tableName = svcDbUtils.GetGlrxTableName(databasePath);
                            nameCommand.CommandText = $"SELECT company_name FROM {tableName} WHERE company_id = @companyId LIMIT 1;";
                            var parameter = nameCommand.CreateParameter();
                            parameter.ParameterName = "@companyId";
                            parameter.Value = companyId;
                            nameCommand.Parameters.Add(parameter);

                            var queryResult = await nameCommand.ExecuteScalarAsync();
                            if (queryResult != null && queryResult != DBNull.Value)
                            {
                                companyName = queryResult.ToString() ?? "Unknown Company";
                            }
                        }
                    }
                    catch
                    {
                        // Safe fallback on reflective tables exceptions
                    }
                    finally
                    {
                        try
                        {
                            await dbContext.Database.CloseConnectionAsync();
                        }
                        catch { }
                    }

                    var claimMap = new Dictionary<string, string>
                    {
                        { ClaimTypes.Name, user.Username },
                        { "sub", user.Username },
                        { "company_id", user.CompanyId },
                        { "company_name", companyName },
                        { "role", user.Role }
                    };

                    string jsonWebToken = _authenticationService.CreateAccessToken(claimMap, SecretKey, AccessTokenExpireMinutes);

                    // =====================================================================
                    // SECURITY CRITICAL: Set HttpOnly, Secure, SameSite Cookie
                    // =====================================================================
                    Response.Cookies.Append("access_token", jsonWebToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromHours(CookieExpireHours),
                        Path = "/"
                    });

                    return Ok(new
                    {
                        status = "success",
                        message = "Authenticated successfully",
                        user = new
                        {
                            username = user.Username,
                            company_id = user.CompanyId,
                            company_name = companyName,
                            role = user.Role
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Securely log the exception internally here in a real production system
                // Temporarily expose the error for debugging in the production environment
                return StatusCode(500, new { detail = $"An unexpected system error occurred: {ex.Message} \n {ex.StackTrace}" });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Evict cookie immediately
            Response.Cookies.Append("access_token", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.Zero,
                Expires = DateTimeOffset.UnixEpoch,
                Path = "/"
            });

            return Ok(new { status = "success", message = "Successfully logged out and session cleared" });
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var backendPort = _configuration.GetValue<int>("Server:Port", 8001);
            int frontendPort = 8002;
            try
            {
                string current = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(current))
                {
                    if (Directory.Exists(Path.Combine(current, "BMS-BI-APP")) && Directory.Exists(Path.Combine(current, "BI-API-API")))
                    {
                        string possibleVite = Path.Combine(current, "BMS-BI-APP", "vite.config.js");
                        if (System.IO.File.Exists(possibleVite))
                        {
                            string content = System.IO.File.ReadAllText(possibleVite);
                            var match = System.Text.RegularExpressions.Regex.Match(content, @"port:\s*(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int p))
                            {
                                frontendPort = p;
                                break;
                            }
                        }
                    }
                    string? parent = Path.GetDirectoryName(current);
                    if (parent == current || string.IsNullOrEmpty(parent)) break;
                    current = parent;
                }
            }
            catch { }

            return Ok(new { backendPort, frontendPort });
        }
    }
}
