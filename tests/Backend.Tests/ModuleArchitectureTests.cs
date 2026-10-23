using System.Reflection;
using backend.Controllers;
using backend.Features.Common;
using backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Backend.Tests;

public sealed class ModuleArchitectureTests
{
    public static IEnumerable<object[]> Controllers()
    {
        yield return [typeof(ProductsController)];
        yield return [typeof(OrdersController)];
        yield return [typeof(CasesController)];
        yield return [typeof(UsersController)];
        yield return [typeof(ApplicationsController)];
        yield return [typeof(LearningStructureController)];
        yield return [typeof(RoadMapsController)];
        yield return [typeof(SubscriptionsController)];
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void HttpAdaptersDependOnOneFeatureServiceNotDatabase(Type controller)
    {
        var constructor = Assert.Single(controller.GetConstructors());
        var dependency = Assert.Single(constructor.GetParameters());
        Assert.NotEqual(typeof(ApplicationDbContext), dependency.ParameterType);
        Assert.StartsWith("backend.Features.", dependency.ParameterType.Namespace!);
        Assert.Empty(controller.GetNestedTypes(BindingFlags.Public));
    }

    [Fact]
    public void ManagementServicesDoNotExposeHttpResultsOrContexts()
    {
        var services = typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(x => x.IsClass && x.IsPublic && x.Name.EndsWith("Service") &&
                x.Namespace?.EndsWith(".Management") == true);
        Assert.Equal(8, services.Count());
        foreach (var service in services)
            foreach (var method in service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var result = method.ReturnType;
                if (result.IsGenericType && result.GetGenericTypeDefinition() == typeof(Task<>))
                    result = result.GenericTypeArguments[0];
                Assert.True(result.IsGenericType && result.GetGenericTypeDefinition() == typeof(ModuleOutcome<>));
                Assert.Contains(method.GetParameters(), p => p.ParameterType == typeof(ModuleActor));
                Assert.DoesNotContain(method.GetParameters(), p => typeof(ControllerBase).IsAssignableFrom(p.ParameterType) ||
                    p.ParameterType.Namespace?.StartsWith("Microsoft.AspNetCore") == true);
            }
    }
}
