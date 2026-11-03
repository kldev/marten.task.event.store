using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Halcyon.App.Storage;

/// <summary>The bytes of attachments. The record of which file belongs to which ticket is the ticket's events.</summary>
public interface IAttachmentStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Null when the object is not there.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);

    /// <summary>For the readiness check: the bucket answers.</summary>
    Task CheckAsync(CancellationToken ct);
}

public sealed class S3AttachmentStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IAttachmentStorage
{
    private string Bucket => options.Value.Bucket;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct) =>
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            ct
        );

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectAsync(Bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct) => await s3.DeleteObjectAsync(Bucket, key, ct);

    public async Task CheckAsync(CancellationToken ct) =>
        await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, MaxKeys = 1 }, ct);
}

public sealed class StorageHealthCheck(IAttachmentStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await storage.CheckAsync(timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
            when (exception is AmazonServiceException or HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Object storage (attachments bucket) does not answer.", exception);
        }
    }
}

public static class StorageSetup
{
    public static WebApplicationBuilder AddHalcyonStorage(this WebApplicationBuilder builder, string readyTag)
    {
        builder
            .Services.AddOptions<StorageOptions>()
            .Bind(builder.Configuration.GetSection(StorageOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IAmazonS3>(services =>
        {
            var o = services.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(o.AccessKey, o.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = o.ServiceUrl,
                    AuthenticationRegion = o.Region,
                    // RustFS/MinIO: bucket in the path, not in the host name.
                    ForcePathStyle = true,
                    // The SDK's default CRC32 trailing checksums (since 3.7.412) are not understood by every
                    // S3-compatible server; SigV4 still signs the payload.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                }
            );
        });
        // TryAdd: the integration tests register an in-memory storage before the host's services.
        builder.Services.TryAddSingleton<IAttachmentStorage, S3AttachmentStorage>();
        builder.Services.AddHealthChecks().AddCheck<StorageHealthCheck>("storage", tags: [readyTag]);
        return builder;
    }
}
