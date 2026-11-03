using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>The same RustFS as in compose, with the bucket created the way rustfs-init does (PUT bucket).</summary>
public sealed class RustFsContainer : IAsyncLifetime
{
    public const string AccessKey = "halcyon-test";
    public const string SecretKey = "halcyon-test-secret";
    public const string Bucket = "halcyon-attachments";

    private readonly IContainer container = new ContainerBuilder("rustfs/rustfs:1.0.0")
        .WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
        .WithEnvironment("RUSTFS_VOLUMES", "/data")
        .WithEnvironment("RUSTFS_ADDRESS", ":9000")
        .WithPortBinding(9000, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/health")))
        .Build();

    public string ServiceUrl => $"http://{container.Hostname}:{container.GetMappedPublicPort(9000)}";

    public Dictionary<string, string?> Settings =>
        new()
        {
            ["Storage:ServiceUrl"] = ServiceUrl,
            ["Storage:AccessKey"] = AccessKey,
            ["Storage:SecretKey"] = SecretKey,
            ["Storage:Bucket"] = Bucket,
        };

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                ServiceURL = ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        );
        await s3.PutBucketAsync(Bucket);
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
