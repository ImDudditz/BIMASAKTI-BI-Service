using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Services;
using Bimasakti.BiService.Api.Services.Engines;
using Microsoft.AspNetCore.Authorization;

namespace Bimasakti.BiService.Api.Controllers.Reports
{
    [Authorize]
    [ApiController]
    [Route("api/reports/lmrx0220")]
    public class rpLmrx0220Controller : ControllerBase
    {
        private readonly IsvcLmrx0220 _service;
        public rpLmrx0220Controller(IsvcLmrx0220 service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetData()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            
            var data = await _service.GetDataAsync(companyIdClaim.ToUpperInvariant());
            return Ok(new { status = "success", data = data });
        }
    }
}

