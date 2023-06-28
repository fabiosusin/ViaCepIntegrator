using Microsoft.AspNetCore.Mvc;
using ViaCepIntegrator.Service;

namespace ViaCepIntegrator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly ViaCepApiService ViaCepApiService;

        public AddressController() => ViaCepApiService = new ViaCepApiService();

        [HttpGet, Route("{zipCode}")]
        public async Task<IActionResult> FindByZipCodeAsync(string zipCode)
        {
            try { return Ok(await ViaCepApiService.FindByZipCode(zipCode)); }
            catch { return Ok("CEP informado está no formato correto!"); }
        }

        [HttpGet, Route("{uf}/{city}/{street}")]
        public async Task<IActionResult> FindByAddressAsync(string uf, string city, string street)
        {
            try { return Ok(await ViaCepApiService.FindByAddress(uf, city, street)); }
            catch { return Ok("Endereço informado está no formato correto!"); }
        }
    }
}