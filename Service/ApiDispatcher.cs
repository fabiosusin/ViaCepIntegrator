using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ViaCepIntegrator.Models.Enum;

namespace ViaCepIntegrator.Service
{
    public class ApiDispatcher
    {
        internal readonly JsonSerializerSettings _serializerSettings;

        public ApiDispatcher(JsonSerializerSettings customSettings = null)
        {
            _serializerSettings = customSettings ?? new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async Task<string> DispatchWithResponseUnDeserializeAsync(
            string url,
            RequestMethodEnum method,
            object body = null,
            Tuple<HttpRequestHeader, string>[] headers = null,
            Tuple<string, string>[] customHeaders = null)
        {
            return await SendRequestAsync(url, method, body, headers, customHeaders);
        }

        public async Task<T> DispatchWithResponseAsync<T>(
            string url,
            RequestMethodEnum method,
            object body = null,
            Tuple<HttpRequestHeader, string>[] headers = null,
            Tuple<string, string>[] customHeaders = null)
        {
            var result = await SendRequestAsync(url, method, body, headers, customHeaders);
            return JsonConvert.DeserializeObject<T>(result, _serializerSettings);
        }

        public async Task<T> DispatchUrlEncodedBodyWithResponseAsync<T>(string url, RequestMethodEnum method, Dictionary<string, string> body, Tuple<string, string>[] headers) =>
            JsonConvert.DeserializeObject<T>(await CreateHttpRequestMessageAsync(method, url, body, headers), _serializerSettings);

        private static async Task<string> CreateHttpRequestMessageAsync(RequestMethodEnum method, string url, Dictionary<string, string> body, Tuple<string, string>[] headers)
        {
            var requestMessage = GetHttpRequestMessage(method, url, body);

            if (headers?.Any() ?? false)
                foreach (var item in headers)
                    requestMessage.Headers.Add(item.Item1, item.Item2);

            return await SendHttpRequestMessageAsync(requestMessage);
        }

        private static HttpRequestMessage GetHttpRequestMessage(RequestMethodEnum method, string url, Dictionary<string, string> body)
        {
            var requestMessage = new HttpRequestMessage(new HttpMethod(method.ToString()), url);
            requestMessage.Headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("Xplay")));
            requestMessage.Headers.ExpectContinue = false;

            if (body?.Any() ?? false)
                requestMessage.Content = new FormUrlEncodedContent(body);

            return requestMessage;
        }

        private static async Task<string> SendHttpRequestMessageAsync(HttpRequestMessage requestMessage)
        {
            var client = new HttpClient();
            using var response = await client.SendAsync(requestMessage);
            return await response.Content.ReadAsStringAsync();
        }

        private static HttpWebRequest CreateRequest(
            string url,
            RequestMethodEnum method,
            Tuple<HttpRequestHeader, string>[] headers = null,
            string contentType = null,
            Tuple<string, string>[] customHeaders = null)
        {
            if (url.StartsWith("https://"))
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            }

            var request = (HttpWebRequest)WebRequest.Create(url);
            string userAgent = null;
            if (headers != null)
                foreach (var header in headers)
                {
                    if (header.Item1 == HttpRequestHeader.ContentType) { contentType ??= header.Item2; continue; }
                    if (header.Item1 == HttpRequestHeader.UserAgent) { userAgent ??= header.Item2; continue; }
                    if (header.Item1 == HttpRequestHeader.Accept)
                        request.Accept = header.Item2;
                    else
                        request.Headers.Add(header.Item1, header.Item2);
                }

            if (customHeaders != null)
                foreach (var header in customHeaders)
                {
                    if (header.Item1.ToLower() == "content-type") { contentType ??= header.Item2; continue; }
                    if (header.Item1.ToLower() == "user-agent") { userAgent ??= header.Item2; continue; }
                    request.Headers[header.Item1] = header.Item2;
                }

            request.ContentType = contentType ?? "application/json; charset=UTF-8";
            request.Method = method.ToString();
            request.UserAgent = userAgent;
            request.AutomaticDecompression = DecompressionMethods.GZip;

            return request;
        }

        private async Task<string> SendRequestAsync(
            string url,
            RequestMethodEnum method,
            object body,
            Tuple<HttpRequestHeader, string>[] headers,
            Tuple<string, string>[] customHeaders = null)
        {
            var request = CreateRequest(url, method, headers, null, customHeaders);
            await WriteRequestBodyAsync(request, body);

            try
            {
                using var response = (HttpWebResponse)(await request.GetResponseAsync());
                using var responseStream = response.GetResponseStream();
                using var streamReader = new StreamReader(responseStream);
                return streamReader.ReadToEnd();
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    if (((int)((HttpWebResponse)e.Response).StatusCode) == 429)
                    {
                        await Task.Delay(5000);
                        return await SendRequestAsync(url, method, body, headers);
                    }

                    if (((int)((HttpWebResponse)e.Response).StatusCode) == 404 || ((int)((HttpWebResponse)e.Response).StatusCode) == 422)
                        return string.Empty;
                }

                throw new Exception(UnwrapWebException(e));
            }
        }

        private async Task WriteRequestBodyAsync(
            HttpWebRequest request,
            object body)
        {
            if (body == null)
                return;

            string requestBody;
            if (request.ContentType.Contains("json"))
            {
                requestBody = JsonConvert.SerializeObject(
                body,
                Formatting.Indented,
                _serializerSettings);
            }
            else
                requestBody = body.ToString();

            using var requestStream = await request.GetRequestStreamAsync();
            var data = new UTF8Encoding().GetBytes(requestBody);
            requestStream.Write(data, 0, data.Length);
        }

        private static string UnwrapWebException(WebException ex)
        {
            try
            {
                var webResponse = ex.Response;
                var statusCode = (int)((HttpWebResponse)webResponse)?.StatusCode;
                if (webResponse == null)
                    return ex.Message ?? $"WebException sem mensagem. StatusCode: {statusCode}";

                var reader = webResponse.GetResponseStream();
                var content = new StreamReader(reader).ReadToEnd();
                if (string.IsNullOrEmpty(content))
                    return $"Conteúdo em branco. StatusCode: {statusCode}";

                var responseJson = JsonConvert.DeserializeObject(content);
                return responseJson?.ToString();
            }
            catch
            {
                return ex?.Message ?? "WebException nulo ou sem mensagem";
            }
        }
    }
}
