// SPDX-License-Identifier: MIT
// Copyright: 2023 Econolite Systems, Inc.

using HealthChecks.UI.Core;
using HealthChecks.UI.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.System.Health.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemHealthController : ControllerBase
{
    private readonly HealthChecksDb _db;
    private readonly ILogger<SystemHealthController> _logger;

    public SystemHealthController(HealthChecksDb db, ILogger<SystemHealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("/HealthCheckConfigurations")]
    public IEnumerable<HealthCheckConfiguration> Get()
    {
        return _db.Configurations.ToArray();
    }
    
    [HttpGet("/ServiceHealthStatus")]
    public async Task<IEnumerable<HealthCheckExecution>> GetServiceStatusAsync()
    {
        var healthChecks = await _db.Configurations.ToListAsync();

        var healthChecksExecutions = new List<HealthCheckExecution>();

        foreach (var item in healthChecks.OrderBy(h => h.Id).ToArray())
        {
            var execution = await _db.Executions
                .Include(le => le.Entries)
                .Where(le => le.Name == item.Name)
                .AsNoTracking()
                .SingleOrDefaultAsync();

            if (execution != null)
            {
                execution.History = await _db.HealthCheckExecutionHistories
                    .Where(eh => EF.Property<int>(eh, "HealthCheckExecutionId") == execution.Id)
                    .OrderByDescending(eh => eh.On)
                    .Take(50)
                    .ToListAsync();

                healthChecksExecutions.Add(execution);
            }
        }

        return healthChecksExecutions;
    }

    [HttpPost("/UIHealthReports")]
    public async Task AddReportAsync([FromBody] UIHealthReport report)
    {
        //Response.Headers.Location = new Uri($"/healthchecks/{report.Entries..Name}", UriKind.Relative);
        await Task.CompletedTask;
    }
}