// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Quartz;
using System.Reflection;

namespace Cotton.Server.IntegrationTests.Common;

internal class NoOpSchedulerFactory : ISchedulerFactory
{
    private readonly IScheduler _scheduler =
        DispatchProxy.Create<IScheduler, SchedulerDispatchProxy>();

    public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IScheduler> schedulers = [_scheduler];
        return Task.FromResult(schedulers);
    }

    public Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_scheduler);
    }

    public Task<IScheduler?> GetScheduler(string schedName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IScheduler?)_scheduler);
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
            object? fallbackValue = GetDefaultValue(genericArgument);

            if (genericType == typeof(Task<>))
            {
                MethodInfo fromResultMethod = typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(genericArgument);
                return fromResultMethod.Invoke(null, [fallbackValue]);
            }

            if (genericType == typeof(ValueTask<>))
            {
                return Activator.CreateInstance(returnType, fallbackValue);
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
