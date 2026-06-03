using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BMS_BI_SERVICE.Core.Services
{
    public interface IsvcAuthenticationService
    {
        string GetPasswordHash(string password);
        bool VerifyPassword(string plainPassword, string hashedPassword);
        string CreateAccessToken(Dictionary<string, string> claimMap, string secretKey, int expireMinutes);
    }

    public class svcAuthenticationService : IsvcAuthenticationService
    {
        public string GetPasswordHash(string password)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public bool VerifyPassword(string plainPassword, string hashedPassword)
        {
            return GetPasswordHash(plainPassword) == hashedPassword;
        }

        public string CreateAccessToken(
            Dictionary<string, string> claimMap, 
            string secretKey, 
            int expireMinutes)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            byte[] key = Encoding.UTF8.GetBytes(secretKey);

            var claimList = new List<Claim>();
            foreach (var keyValuePair in claimMap)
            {
                claimList.Add(new Claim(keyValuePair.Key, keyValuePair.Value));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claimList),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
