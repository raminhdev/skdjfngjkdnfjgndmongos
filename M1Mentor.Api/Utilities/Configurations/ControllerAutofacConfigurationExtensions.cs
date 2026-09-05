using System.Reflection;
using Autofac;
using M1Mentor.Domain.Repositories.Contracts;
using M1Mentor.Services._Log;
using static Utilities.Constants.RegisterMode;


namespace M1Mentor.Api.Utilities.Configurations
{
    public static class ControllerAutofacConfigurationExtensions
    {
        public static void AddControllerServices(this ContainerBuilder containerBuilder)
        {
            var assembliesToRegister = new Assembly[]
            {
                typeof(ILogRepository).Assembly,
                typeof(ILogService).Assembly,
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
}
