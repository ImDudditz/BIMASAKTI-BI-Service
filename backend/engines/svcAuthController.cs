using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using BimasaktiReports.FinancialReports.Backend.Services;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    // Models are defined in TenantModels.cs

    // --- ROUTER / CLASS ---

    [ApiController]
    [Route("api/auth")]
    public class svcAuthController : ControllerBase
    {
        private readonly IsvcAuthenticationService _authenticationService;
        private static readonly string SecretKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET is missing.");

        private const int AccessTokenExpireMinutes = 15;

        public svcAuthController(IsvcAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
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
                string databasePath = svcDbUtils.GetSafeDbPath(companyId);

                using (var dbContext = new TenantDbContext(databasePath))
                {
                    // Equivalent to SQLAlchemy's create_all
                    await dbContext.Database.EnsureCreatedAsync();

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
                        catch {}
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
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromHours(1),
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
            catch (Exception)
            {
                // Securely log the exception internally here in a real production system
                return StatusCode(500, new { detail = "An unexpected system error occurred." });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Evict cookie immediately
            Response.Cookies.Append("access_token", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.Zero,
                Expires = DateTimeOffset.UnixEpoch,
                Path = "/"
            });

            return Ok(new { status = "success", message = "Successfully logged out and session cleared" });
        }
    }
}
