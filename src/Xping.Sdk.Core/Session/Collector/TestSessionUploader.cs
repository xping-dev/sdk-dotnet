/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.Http.Headers;
using Xping.Sdk.Core.Clients.Http;
using Xping.Sdk.Core.Session.Serialization;

namespace Xping.Sdk.Core.Session.Collector;

internal class TestSessionUploader(
    ITestSessionSerializer serializer,
    IHttpClientFactory factory) : ITestSessionUploader
{
    public async Task<UploadResult> UploadAsync(
        TestSession testSession,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = factory.CreateClient(HttpClientFactoryConfiguration.HttpClientUploadSession);
            
            // Add API key to request headers if provided
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
            
            using var form = new MultipartFormDataContent();
            using MemoryStream memoryStream = new();
            
            serializer.Serialize(testSession, memoryStream, SerializationFormat.XML);
            memoryStream.Position = 0; // Reset position for reading
            
            using var fileContent = new StreamContent(memoryStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            form.Add(fileContent, name: "file", fileName: "test-session.xml");
            
            var response = await httpClient.PostAsync(
                // TODO: For Production, we should configure the upload base URL at the HttpClient level
                new Uri("https://localhost:7100/api/upload"), form, cancellationToken).ConfigureAwait(false);
            
            // Update test session with upload timestamp if successful
            if (response.IsSuccessStatusCode)
            {
                testSession.MarkAsUploaded(DateTimeOffset.UtcNow);
                return UploadResult.Success(response.StatusCode);
            }
            
            return UploadResult.Failure(
                $"Upload failed with status code: {response.StatusCode}", 
                statusCode: response.StatusCode);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return UploadResult.Failure("Upload was cancelled", ex);
        }
        catch (HttpRequestException ex)
        {
            return UploadResult.Failure("Network error occurred during upload", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return UploadResult.Failure("Upload request timed out", ex);
        }
        catch (Exception ex)
        {
            return UploadResult.Failure($"Unexpected error during upload: {ex.Message}", ex);
        }
    }
}
