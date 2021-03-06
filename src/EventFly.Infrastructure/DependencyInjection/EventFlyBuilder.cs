using System;
using System.Linq;
using System.Reflection;
using EventFly.Definitions;
using EventFly.ExternalEventPublisher;
using EventFly.Infrastructure.Definitions;
using EventFly.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventFly.Infrastructure.DependencyInjection
{
    public sealed class EventFlyBuilder
    {
        public IServiceCollection Services { get; }
        public IApplicationDefinition ApplicationDefinition { get; private set; }

        public EventFlyBuilder(IServiceCollection services)
        {
            Services = services;
            ApplicationDefinition = new ApplicationDefinition();
            Services
                .AddSingleton(ApplicationDefinition)
                .AddSingleton<IDefinitionToManagerRegistry, DefinitionToManagerRegistry>();
        }

        public EventFlyBuilder WithContext<TContext>(Func<IServiceCollection, IServiceCollection> myDependencies = null)
            where TContext : ContextDefinition, new()
        {
            var context = new TContext();

            if (myDependencies == null)
                context.DI(Services);
            else
                myDependencies(Services);

            ((ApplicationDefinition)ApplicationDefinition).RegisterContext(context);
            RegisterValidators(Services);
            return this;
        }

        public EventFlyBuilder AddExternalEventPublisher<TContext>(Func<IServiceProvider, IExternalEventPublisher> externalEventResolver)
            where TContext : IContextDefinition
        {
            Services.AddScoped(sp =>
            {
                var appDefinition = sp.GetService<IApplicationDefinition>();
                var contextDefinition = (TContext)appDefinition.Contexts.First(ctx => ctx.GetType() == typeof(TContext));

                return new Subscribers.ExternalEventPublisher<TContext>(contextDefinition, externalEventResolver(sp));
            });
            ((ApplicationDefinition)ApplicationDefinition).RegisterDomainEventSubscriber<Subscribers.ExternalEventPublisher<TContext>>();
            return this;
        }

        private static void RegisterValidators(IServiceCollection services)
        {
            //bug workaround, see: https://developercommunity.visualstudio.com/content/problem/738856/could-not-load-file-or-assembly-microsoftintellitr.html
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.FullName.StartsWith("Microsoft."));
            foreach (var assembly in assemblies) RegisterValidatorsInAssembly(assembly, services);
        }

        private static void RegisterValidatorsInAssembly(Assembly assembly, IServiceCollection services)
        {
            try
            {
                var validatorTypes = assembly
                    .GetTypes()
                    .SelectMany(i => i.GetCustomAttributes<ValidatorAttribute>()
                    .Select(j => new { Type = i, j.ValidatorType }))
                    .Distinct();
                foreach (var item in validatorTypes)
                {
                    services.TryAddSingleton(typeof(IValidator<>).MakeGenericType(item.Type), item.ValidatorType);
                }
            }
            catch
            {
                //ignore
            }
        }
    }
}