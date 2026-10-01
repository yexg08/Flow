using Flow.Application.Business;
using Flow.Application.Catalog;
using Flow.Application.Public;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IPublicBusinessService, PublicBusinessService>();
        return services;
    }
}
