using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pulumi;

namespace BadBort.Pulumi;

public class PulumiObservability(ActivitySource activitySource) : IDisposable
{
    private readonly ConcurrentDictionary<string, Activity> _activities = new();

    private ResourceHook Before(string eventType)
    {
        return new ResourceHook($"otel-before-{eventType}", (args, ct) =>
        {
            // args.Operation, args.Urn, args.NewOutputs are available here.
            var urn = args.Urn;
            var name = $"{eventType}:{urn}";

            var span = activitySource.StartActivity(name);

            if (span == null)
                return Task.CompletedTask;

            span.SetTag("pulumi.resource.urn", urn);
            span.SetTag("pulumi.resource.id", args.Id);
            _activities[name] = span;

            return Task.CompletedTask;
        });
    }

    private ResourceHook After(string eventType)
    {
        return new ResourceHook($"otel-after-{eventType}", (args, ct) =>
        {
            var name = $"{eventType}:{args.Urn}";

            if (_activities.TryRemove(name, out var activity))
            {
                activity.Dispose();
            }

            return Task.CompletedTask;
        });
    }

    public CustomResourceOptions AddOtelHooks(CustomResourceOptions options)
    {
        if (TestUtility.IsInTest())
            return options;

        options.Hooks.BeforeCreate.Add(Before("Create"));
        options.Hooks.AfterCreate.Add(After("Create"));

        options.Hooks.BeforeUpdate.Add(Before("Update"));
        options.Hooks.AfterUpdate.Add(After("Update"));

        options.Hooks.BeforeDelete.Add(Before("Delete"));
        options.Hooks.AfterDelete.Add(After("Delete"));

        return options;
    }

    public CustomResourceOptions GetOptions() => AddOtelHooks(new CustomResourceOptions());

    void IDisposable.Dispose()
    {
        foreach (var (_, activity) in _activities)
        {
            activity.Dispose();
        }

        _activities.Clear();
    }

    public static IDisposable InitializeTracing(ActivitySource activitySource)
    {
        var builder =
            Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName: activitySource.Name))
                .AddSource(activitySource.Name)
                .AddHttpClientInstrumentation()
                .AddOtlpExporter();
        // .AddConsoleExporter();

        var aiConnectinStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        
        // if (!string.IsNullOrEmpty(aiConnectinStr))
        // {
            builder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = aiConnectinStr;
                options.SamplingRatio = 1;
            });
        // }

        return builder.Build();
    }
}