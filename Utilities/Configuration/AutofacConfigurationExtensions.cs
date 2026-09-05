using System.Reflection;
using Autofac;
using Microsoft.Extensions.Hosting;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Configuration;
public static class AutofacConfigurationExtensions
{
    public static void AddCoreServices(this ContainerBuilder containerBuilder)
    {
        var assembliesToRegister = new Assembly[]
        {
                typeof(JwtService).Assembly,
        };

        containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
            .AssignableTo<IScopedDependency>()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
            .AssignableTo<ITransientDependency>()
            .AsImplementedInterfaces()
            .InstancePerDependency();

        containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
            .AssignableTo<ISingletonDependency>()
            .AsImplementedInterfaces()
            .SingleInstance();

        containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
           .AssignableTo<ISelfSingletonDependency>()
           .AsSelf()
           .SingleInstance();

        containerBuilder.RegisterAssemblyTypes(assembliesToRegister)
          .AssignableTo<IHostedDependency>()
          .As<IHostedService>()
          .SingleInstance();
    }
}