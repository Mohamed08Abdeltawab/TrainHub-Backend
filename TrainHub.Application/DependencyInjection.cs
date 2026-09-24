using Microsoft.Extensions.DependencyInjection;

namespace TrainHub.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer services (use cases, validators, mapping profiles, etc.).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Future registrations:
        // - MediatR / service classes
        // - FluentValidation validators
        // - AutoMapper / Mapster profiles

        return services;
    }
}
