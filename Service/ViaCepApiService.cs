using System.Net;
using ViaCepIntegrator.Models.Enum;
using ViaCepIntegrator.Models.Output;

namespace ViaCepIntegrator.Service
{
    internal class ViaCepApiService
    {
        private readonly ApiDispatcher _apiDispatcher;
        private static readonly string BaseUrl = "https://viacep.com.br/ws";

        public ViaCepApiService() => _apiDispatcher = new ApiDispatcher();

        public async Task<ViaCepAddressOutput> FindByZipCode(string zipcode) =>
            await SendRequest<ViaCepAddressOutput>(RequestMethodEnum.GET, $"{BaseUrl}/{WebUtility.UrlEncode(zipcode)}/json/");

        public async Task<List<ViaCepAddressOutput>> FindByAddress(string uf, string city, string street) =>
            await SendRequest<List<ViaCepAddressOutput>>(RequestMethodEnum.GET, $"{BaseUrl}/{WebUtility.UrlEncode(uf)}/{Uri.EscapeDataString(city)}/{WebUtility.UrlEncode(street)}/json/");

        private async Task<T> SendRequest<T>(RequestMethodEnum method, string url, object body = null)
        {
            try { return await _apiDispatcher.DispatchWithResponseAsync<T>(url, method, body); } catch { throw; }
        }
    }
}
