// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Quartz;
using System.Reflection;

namespace Cotton.Server.IntegrationTests.Common;

internal sealed class NoOpSchedulerFactory : ISchedulerFactory
{
    private readonly IScheduler _scheduler =
        DispatchProxy.Create<IScheduler, SchedulerDispatchProxy>();

    public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IScheduler> schedulers = new[] { _scheduler };
        return Task.FromResult(schedulers);
    }

    public Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_scheduler);
    }

    public Task<IScheduler?> GetScheduler(string schedName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_scheduler);
    }
}

public class SchedulerDispatchProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            return null;
        }

        Type returnType = targetMethod.ReturnType;
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType == typeof(ValueTask))
        {
            return ValueTask.CompletedTask;
        }

        if (returnType.IsGenericType)
        {
            Type genericType = returnType.GetGenericTypeDefinition();
            Type genericArgument = returnType.GetGenericArguments()[0];
            object? defaultValue = GetDefaultValue(genericArgument);

            if (genericType == typeof(Task<>))
            {
                MethodInfo fromResultMethod = typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(genericArgument);
                return fromResultMethod.Invoke(null, new[] { defaultValue });
            }

            if (genericType == typeof(ValueTask<>))
            {
                return Activator.CreateInstance(returnType, defaultValue);
            }
        }

        if (returnType == typeof(string))
        {
            return string.Empty;
        }

        return GetDefaultValue(returnType);
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType
            ? Activator.CreateInstance(type)
            : null;
    }
}
