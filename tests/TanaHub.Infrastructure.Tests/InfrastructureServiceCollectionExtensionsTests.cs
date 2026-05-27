using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Services;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.DependencyInjection;

namespace TanaHub.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTanaHubInfrastructure_RegistersApplicationServices()
    {
        var services = new ServiceCollection();

        services.AddTanaHubInfrastructure();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMediaCatalogService)
            && descriptor.ImplementationFactory is not null
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(InMemoryMediaCatalogService)
            && descriptor.ImplementationType == typeof(InMemoryMediaCatalogService)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IUserLibraryService)
            && descriptor.ImplementationFactory is not null
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
