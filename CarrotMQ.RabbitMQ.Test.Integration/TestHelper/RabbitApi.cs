using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CarrotMQ.RabbitMQ.Configuration;
using JsonSerializer = System.Text.Json.JsonSerializer;
#if !NET
using System.Net.Http;
#endif

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public class RabbitApi : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _vHost;

    public RabbitApi(BrokerConnectionOptions brokerOptions, int rabbitHttpPort)
    {
        var firstEndPoint = brokerOptions.BrokerEndPoints.FirstOrDefault() ?? throw new NullReferenceException();

        var host = firstEndPoint.Host;
        var port = rabbitHttpPort;
        var baseUri = new Uri($"http://{host}:{port}/");
        _httpClient = new HttpClient { BaseAddress = baseUri };

        var encoded = Convert.ToBase64String(
            Encoding.GetEncoding("ISO-8859-1")
                .GetBytes(brokerOptions.UserName + ":" + brokerOptions.Password));

        _httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encoded);

        _vHost = brokerOptions.VHost;
    }

    public async Task CreateVHostAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/vhosts/{_vHost}");
        await _httpClient.SendAsync(request).ConfigureAwait(false);
    }

    public async Task DeleteVHostAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/vhosts/{_vHost}");
        await _httpClient.SendAsync(request).ConfigureAwait(false);
    }

    public async Task PurgeQueueAsync(string queue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/queues/{_vHost}/{queue}/contents");
        var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
        Console.WriteLine(responseMessage.StatusCode);
    }

    public async Task<IList<Exchange>?> GetExchanges()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/exchanges/{_vHost}");
        var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
        Console.WriteLine(responseMessage.StatusCode);
        string responsePayload = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<Exchange>>(responsePayload);
    }

    public async Task<IList<Queue>?> GetQueues()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/queues/{_vHost}");
        var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
        Console.WriteLine(responseMessage.StatusCode);
        string responsePayload = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<Queue>>(responsePayload);
    }

    public async Task<IList<Bindings>?> GetExchangeBindings(string exchangeName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/exchanges/{_vHost}/{exchangeName}/bindings/source");
        var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
        Console.WriteLine(responseMessage.StatusCode);
        string responsePayload = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<Bindings>>(responsePayload);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task AwaitNumberOfMessagesInQueueAsync(string queueName, int expectedMessageCount, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/queues/{_vHost}/{queueName}");
            var responseMessage = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
            var jsonContent = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            var match = Regex.Match(jsonContent, "\"messages\":\\s?(?<messageCount>[0-9]*),", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

            if (match.Success)
            {
                var messageCount = int.Parse(match.Groups["messageCount"].Value);
                Console.WriteLine($"There are {messageCount} messages in queue {queueName}");

                if (messageCount == expectedMessageCount) break;
            }
        }

        token.ThrowIfCancellationRequested();
    }

    public class Exchange
    {
        [JsonPropertyName("arguments")]
        public IDictionary<string, object>? Arguments { get; set; }

        [JsonPropertyName("auto_delete")]
        public bool AutoDelete { get; set; }

        [JsonPropertyName("durable")]
        public bool Durable { get; set; }

        [JsonPropertyName("internal")]
        public bool Internal { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        public string? UserWhoPerformedAction { get; set; }

        [JsonPropertyName("vhost")]
        public string? Vhost { get; set; }
    }

    public class Queue
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("arguments")]
        public IDictionary<string, object>? Arguments { get; set; }

        [JsonPropertyName("auto_delete")]
        public bool AutoDelete { get; set; }

        [JsonPropertyName("durable")]
        public bool Durable { get; set; }

        [JsonPropertyName("exclusive")]
        public bool Exclusive { get; set; }

        [JsonPropertyName("delivery_limit")]
        public int DeliveryLimit { get; set; }
    }

    public class Bindings
    {
        [JsonPropertyName("source")]

        public string? Source { get; set; }

        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("routing_key")]
        public string? RoutingKey { get; set; }

        [JsonPropertyName("arguments")]
        public IDictionary<string, object>? Arguments { get; set; }
    }
}