using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PoeFilterX.Extensions
{
    /// <summary>
    /// Extends <see cref="IServiceCollection"/>
    /// </summary>
    internal static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers both the Implementation of type T
        /// </summary>
        /// <param name="services"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IServiceCollection AddLazyTransient(this IServiceCollection services, Type type)
        {
            // Define an expression that contains a reference to the method to call
            Expression<Action> expression = () => AddLazyTransient<object>(null);
            return services.AddLazy(type, expression);
        }

        public static IServiceCollection AddLazySingleton(this IServiceCollection services, Type type)
        {
            // Define an expression that contains a reference to the method to call
            Expression<Action> expression = () => AddLazySingleton<object>(null);
            return services.AddLazy(type, expression);
        }

        public static IServiceCollection AddLazyScoped(this IServiceCollection services, Type type)
        {
            // Define an expression that contains a reference to the method to call
            Expression<Action> expression = () => AddLazyScoped<object>(null);
            return services.AddLazy(type, expression);
        }

        private static IServiceCollection AddLazy(this IServiceCollection services, Type type, Expression<Action> expression)
        {
            // Extract the method from the expression
            var addLazyTransientOfObject = ((MethodCallExpression)expression.Body).Method;

            // Convert it to a generic type definition.
            var addLazyTransientOfT = addLazyTransientOfObject.GetGenericMethodDefinition();

            // Convert it to the exact closed method definition we require
            var addLazyTransientOfType = addLazyTransientOfT.MakeGenericMethod(type);

            // Call the method with its required parameter.
            addLazyTransientOfType.Invoke(null, new[] { services });

            return services;
        }

        public static IServiceCollection AddLazyTransient<T>(this IServiceCollection services)
            where T : class
        {
            return services
                .AddTransient<T>()
                .AddTransient<Func<T>>(s => s.GetRequiredService<T>);
        }

        public static IServiceCollection AddLazyTransient<TService, TImplementation>(this IServiceCollection services)
             where TService : class
             where TImplementation : class, TService
        {
            return services
                .AddTransient<TService, TImplementation>()
                .AddTransient<Func<TService>>(s => s.GetRequiredService<TService>);
        }

        public static IServiceCollection AddLazySingleton<T>(this IServiceCollection services)
            where T : class
        {
            return services
                .AddSingleton<T>()
                .AddSingleton<Func<T>>(s => s.GetRequiredService<T>);
        }

        public static IServiceCollection AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            return services
                .AddSingleton<TService, TImplementation>()
                .AddSingleton<Func<TService>>(s => s.GetRequiredService<TService>);
        }

        public static IServiceCollection AddLazyScoped<T>(this IServiceCollection services)
            where T : class
        {
            return services
                .AddScoped<T>()
                .AddScoped<Func<T>>(s => s.GetRequiredService<T>);
        }

        public static IServiceCollection AddLazyScoped<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            return services
                .AddScoped<TService, TImplementation>()
                .AddScoped<Func<TService>>(s => s.GetRequiredService<TService>);
        }
    }
}
