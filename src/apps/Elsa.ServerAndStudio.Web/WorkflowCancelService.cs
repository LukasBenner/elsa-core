using Elsa.Alterations.AlterationTypes;
using Elsa.Alterations.Core.Contracts;
using Elsa.Alterations.Core.Models;
using Elsa.Alterations.Core.Results;
using Elsa.Workflows;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Entities;
using Elsa.Workflows.Runtime.Filters;

public class WorkflowCancelService : BackgroundService
{
    private readonly ILogger<WorkflowCancelService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkflowCancelService(IServiceScopeFactory scopeFactory, ILogger<WorkflowCancelService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        IWorkflowInstanceFinder workflowInstanceFinder = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceFinder>();
        IBookmarkStore bookmarkStore = scope.ServiceProvider.GetRequiredService<IBookmarkStore>();
        IAlterationRunner runner = scope.ServiceProvider.GetRequiredService<IAlterationRunner>();


        _logger.LogInformation("Searching for workflows suspended workflows");

        AlterationWorkflowInstanceFilter workflowFilter = new()
        {
            SubStatuses = [WorkflowSubStatus.Suspended]
        };

        IEnumerable<string> instances = await workflowInstanceFinder.FindAsync(workflowFilter, stoppingToken);

        _logger.LogInformation("Found {count} matching instances", instances.Count());

        // IAlteration alteration = new CancelActivity
        // {
            
        // };

        // ICollection<RunAlterationsResult> results = await runner.RunAsync(instances, [alteration], stoppingToken);

        // foreach (RunAlterationsResult result in results)
        //     _logger.LogInformation(result.Log.ToString());

        // _logger.LogInformation("Applied alterations");
    }
}