// SPDX-License-Identifier: MIT
// Copyright: 2023 Econolite Systems, Inc.

using Econolite.Ode.Authorization.Extensions;
using Econolite.Ode.Monitoring.Events.Extensions;
using Econolite.Ode.Monitoring.Metrics.Extensions;
using Econolite.Ode.Persistence.Mongo;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Monitoring.AspNet.Metrics;
using System.Text.Json.Serialization;
using static Common.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
const string allOrigins = "_allOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: allOrigins,
        policy =>
        {
            policy.AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin();
        });
});
builder.Host.AddODELogging();
builder.Services.AddMetrics(builder.Configuration, "System Health Api")
    .ConfigureRequestMetrics(c =>
    {
        c.RequestCounter = "Requests";
        c.ResponseCounter = "Responses";
    })
    .AddUserEventSupport(builder.Configuration, _ =>
    {
        _.DefaultSource = "System Health Api";
        _.DefaultLogName = Econolite.Ode.Monitoring.Events.LogName.SystemEvent;
        _.DefaultCategory = Econolite.Ode.Monitoring.Events.Category.Server;
        _.DefaultTenantId = Guid.Empty;
    });
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddEndpointsApiExplorer()
    .AddAuthenticationJwtBearer(builder.Configuration, builder.Environment.IsDevelopment())
    .AddSwaggerGen(c =>
    {
        c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            Description =
                "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = JwtBearerDefaults.AuthenticationScheme,
                    },
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    Name = JwtBearerDefaults.AuthenticationScheme,
                    In = ParameterLocation.Header,
                },
                new List<string>()
            },
        });
    }).AddHealthChecksUI(setup =>
    {
        setup.SetHeaderText("Storage providers demo");
        //Maximum history entries by endpoint
        setup.MaximumHistoryEntriesPerEndpoint(50);
    })
    .AddSqlServerStorage(builder.Configuration.GetConnectionString("HealthChecksDb")!)
    .Services
    .AddMongo();

builder.Services
    .AddHealthChecks()
    .AddProcessAllocatedMemoryHealthCheck(maximumMegabytesAllocated: 1024, name: "Process Allocated Memory",
        tags: new[] {"memory"});

var app = builder.Build();
app.UseCors(allOrigins);
if (!app.Environment.IsDevelopment())
{
    app
        .UseHsts()
        .UseHttpsRedirection();
}
app.UseRouting();

app
    .UseSwagger()
    .UseSwaggerUI()
    .UseAuthentication()
    .UseAuthorization();
app.UseHealthChecksPrometheusExporter("/metrics");
app.AddRequestMetrics();
app.UseEndpoints(endpoints =>
{
    endpoints.MapHealthChecks("/healthz", new HealthCheckOptions()
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    endpoints.MapControllers();
    endpoints.MapHealthChecksUI().RequireAuthorization();

    endpoints.MapGet("/", async context => await context.Response.WriteAsync("Hello World!"));
});

app
    .AddUnhandledExceptionLogging()
    .LogStartup()
    .Run();