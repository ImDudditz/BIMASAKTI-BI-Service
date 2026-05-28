# Comprehensive Penetration Test & Security Audit Report

## Executive Summary
A static application security testing (SAST) and architecture review was conducted on the **BIMASAKTI BI Service** workspace. The objective was to identify vulnerabilities within the backend APIs, frontend framework, and authentication mechanisms. 

The audit uncovered several **CRITICAL** and **HIGH** severity vulnerabilities, primarily centering around Broken Access Control, hardcoded backdoors, and insecure default cryptographic keys. Immediate remediation is strongly advised before any production deployment.

---

## Vulnerability Findings

### 1. [CRITICAL] Broken Access Control & IDOR (Insecure Direct Object Reference)
**Location:** `backend/engines/dsAdminDashboard.cs` and `backend/engines/dsLm.cs`

**Description:**
The application fails to enforce authentication globally or at the controller level. The `Program.cs` file configures `app.UseAuthorization()`, but none of the API controllers (e.g., `dsAdminDashboard`, `dsLm`) utilize the `[Authorize]` attribute. 

Furthermore, endpoints suffer from severe Insecure Direct Object Reference (IDOR):
- `GET /api/dashboard/my-widgets` and `GET /api/dashboard/my-reports` accept `company_id` and `username` as query parameters. An unauthenticated attacker can supply any username/company ID and view other users' sensitive dashboards.
- `POST /api/admin/users/{userId}/permissions` accepts an `admin_username` query parameter. It looks up the admin user in the database but **fails to verify if the caller is actually authenticated as that admin**. An attacker can pass `?admin_username=admin` and arbitrarily elevate privileges or alter permissions for any user in the system.

**Remediation:**
- Apply the `[Authorize]` attribute to all API controllers to ensure requests are authenticated.
- Rely on the `ClaimsPrincipal` (e.g., `HttpContext.User.Identity.Name`) from the JWT token to identify the user, rather than trusting `username` or `admin_username` query parameters.

### 2. [CRITICAL] Hardcoded Backdoor / "God Mode" Credentials
**Location:** `backend/engines/svcAuth.cs` (Lines 60-84)

**Description:**
The login endpoint contains a hidden "God Mode" backdoor. If a file named `.god_mode_enabled` exists in the environment and the environment is set to `Development`, an attacker can log in with the hardcoded credentials `admin` / `admin123`. The system will automatically create this admin account if it does not exist. While checked against the `Development` environment, leaving backdoor code in a production codebase risks severe supply-chain or misconfiguration exploits.

**Remediation:**
- Remove the backdoor logic entirely from `svcAuth.cs`. 
- Use standard database seeding mechanisms during deployment to provision an initial administrator account with a securely generated, one-time password.

### 3. [HIGH] Weak / Predictable JWT Secret Key
**Location:** `backend/engines/svcAuth.cs` (Line 23)

**Description:**
The application retrieves the JWT Secret from an environment variable but falls back to a hardcoded string: 
`private static readonly string SecretKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "super-secret-production-key-change-me";`
If the production server is misconfigured and the environment variable is missing, an attacker can use this known string to cryptographically sign their own JWT tokens, granting themselves administrative access to the system.

**Remediation:**
- Remove the fallback string.
- The application should immediately fail to start (throw an Exception) if the `JWT_SECRET` environment variable is null or insufficiently complex (e.g., less than 32 bytes).

### 4. [MEDIUM] Information Disclosure via Exception Handling
**Location:** `backend/engines/svcAuth.cs`, `backend/engines/dsAdminDashboard.cs`

**Description:**
Across multiple endpoints, `try-catch` blocks return the raw `Exception.Message` directly to the client:
`return StatusCode(500, new { detail = $"Backend Error: {exception.Message}" });`
This can expose sensitive internal system structures, database schema details, or file paths to malicious actors, aiding in further exploitation.

**Remediation:**
- Log the actual exception details securely on the server (e.g., using `ILogger`).
- Return a generic, safe error message to the client (e.g., "An unexpected system error occurred.").

### 5. [MEDIUM] Permissive CORS Policy Fallback
**Location:** `backend/Program.cs` (Lines 63-88)

**Description:**
The CORS policy allows any origin (`SetIsOriginAllowed(origin => true)`) combined with `AllowCredentials()` if the `AllowedOrigins` array in `appsettings.json` is empty or contains `*`. While currently restricted in `appsettings.json`, any accidental misconfiguration will result in a globally permissive CORS policy that accepts credentials, leading to cross-origin data exposure.

**Remediation:**
- Explicitly fail or restrict the application from launching if wildcards `*` are used alongside `AllowCredentials()` in production environments, as this violates secure browser standards.

---

## Positive Security Controls Noted
- **Secure Cookie Management:** The backend properly issues the `access_token` as an `HttpOnly`, `Secure`, and `SameSite=Lax` cookie, protecting the JWT from frontend Cross-Site Scripting (XSS) theft.
- **SQL Injection Prevention:** Database queries (e.g., in `dsLm.cs` and `svcDbUtils.cs`) correctly utilize parameterized queries (`cmd.Parameters.AddWithValue`) or strictly sanitize inputs to prevent SQL Injection attacks.
- **Password Hashing:** Passwords are appropriately hashed using `SHA256` in `svcAuthenticationService.cs`. *(Note: Upgrading to a more robust algorithm like `Argon2` or `BCrypt` with unique salts is recommended for long-term security).*
