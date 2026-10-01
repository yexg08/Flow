using Flow.Application.Booking;
using Flow.Application.Business;
using Flow.Application.Catalog;
using Flow.Application.Public;
using Flow.Application.Team;
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
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITimeOffService, TimeOffService>();
        services.AddScoped<AvailabilityEngine>();
        services.AddScoped<IPublicBookingService, PublicBookingService>();
        services.AddScoped<IAppointmentsService, AppointmentsService>();
        return services;
    }
}
