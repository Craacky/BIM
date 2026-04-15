using BIM.Application.Common.Behaviour;
using BIM.Application.Common.Strategy;
using BIM.Application.Common.Validator;
using FluentValidation;
using MediatR.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BIM.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddMediatR(conf =>
            {
                conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                conf.NotificationPublisher = new ParallelNoWaitPublisher();
                conf.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
                conf.AddOpenBehavior(typeof(ValidateBehaviour<,>));
                conf.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
                conf.AddOpenBehavior(typeof(RequestExceptionProcessorBehavior<,>));
            });

            services.AddScoped<LoginVMValidator>();
            services.AddScoped<LabelStarCodeValidator>();

            return services;
        }
    }
}
