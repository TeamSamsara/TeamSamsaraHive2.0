using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot;

// Simple test orchestrator to verify Core infrastructure works
public class TestOrchestrator : BaseOrchestrator
{
    public override string SystemName => "Test";
    
    public TestOrchestrator(ILogger<TestOrchestrator> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);
        LogInfo("✅ Test system initialized successfully!");
    }
    
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);
        LogInfo("✅ Test system started successfully!");
        LogInfo("✅ Core layer is working!");
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await base.StopAsync(cancellationToken);
        LogInfo("✅ Test system stopped successfully!");
    }
}