using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GGroupp.Infra;

internal static partial class HttpExtensions
{
    internal static async ValueTask<Result<DbDocumentGetOut<T>, Failure<DbDocumentGetFailureCode>>> SendAsync<T>(
        this HttpMessageHandler handler, CosmosDbApiOption option, DbDocumentGetIn input, CancellationToken cancellationToken)
    {
        var resourceId = $"dbs/{Encode(option.DatabaseId)}/colls/{Encode(input.ContainerId)}/docs/{input.DocumentId}";
        using var hashAlgorithm = CreateHashAlgorithm(option.MasterKey);

        using var httpClient = InnerCreateHttpClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, resourceId);

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (httpResponse.IsSuccessStatusCode is false)
        {
            return CreateHttpFailure(httpResponse.StatusCode, body).MapFailureCode(MapStatusCode);
        }

        return DeserializeOrFailure(body);

        HttpClient InnerCreateHttpClient()
            =>
            CreateHttpClient(handler, option.BaseAddress)
            .AddCosmosDbCommonHeaders(hashAlgorithm, HttpMethod.Get.Method, resourceId, "docs")
            .AddPartitionKeyHeader(input.PartitionKey);

        static DbDocumentGetFailureCode MapStatusCode(HttpStatusCode statusCode)
            =>
            statusCode switch
            {
                HttpStatusCode.NotFound => DbDocumentGetFailureCode.NotFound,
                _ => default
            };
        
        static Result<DbDocumentGetOut<T>, Failure<DbDocumentGetFailureCode>> DeserializeOrFailure(string body)
        {
            try
            {
                var document = Deserialize<T>(body);
                if(document is null)
                {
                    return Failure.Create(DbDocumentGetFailureCode.Unknown, $"Cannot deserialize response body: {body}");
                }

                return new DbDocumentGetOut<T>(document);
            }
            catch (JsonException ex)
            {
                return Failure.Create(DbDocumentGetFailureCode.Unknown, $"An error occurred during deserialization response body: {body}, error: {ex.Message}");
            }
        }
    }
}