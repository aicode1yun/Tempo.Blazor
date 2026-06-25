using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Demo.Api.Data;

namespace Tempo.Blazor.Demo.Api.Endpoints;

public static class GanttEndpoints
{
    public static void MapGanttEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gantt").WithTags("Gantt");

        // ── Reset (test isolation) ────────────────────────────────

        group.MapPost("/reset", (MockGanttStore s) => { s.Reset(); return Results.Ok(); });

        // ── Read ──────────────────────────────────────────────────

        group.MapGet("/tasks",              (MockGanttStore s) => Results.Ok(s.GetTasks()));
        group.MapGet("/dependencies",       (MockGanttStore s) => Results.Ok(s.GetDependencies()));
        group.MapGet("/baselines",          (MockGanttStore s) => Results.Ok(s.GetBaselines()));
        group.MapGet("/custom-fields",      (MockGanttStore s) => Results.Ok(s.GetCustomFields()));
        group.MapGet("/history",            (MockGanttStore s) => Results.Ok(s.GetHistory()));
        group.MapGet("/resource-calendars", (MockGanttStore s) => Results.Ok(s.GetResourceCalendars()));
        group.MapGet("/reports",            (MockGanttStore s) => Results.Ok(s.GetReports()));
        group.MapGet("/working-schedule",   (MockGanttStore s) => Results.Ok(s.GetWorkingSchedule()));
        group.MapGet("/assignees",          (MockGanttStore s) => Results.Ok(s.GetAssignees()));

        // ── Task CRUD ─────────────────────────────────────────────

        group.MapPost("/tasks", (TmWorkItem task, MockGanttStore s) =>
        {
            var created = s.AddTask(task);
            return Results.Created($"/api/gantt/tasks/{created.Id}", created);
        });

        group.MapPut("/tasks/{id}", (string id, TmWorkItem task, MockGanttStore s) =>
            s.UpdateTask(id, task) ? Results.NoContent() : Results.NotFound());

        group.MapDelete("/tasks/{id}", (string id, MockGanttStore s) =>
            s.RemoveTask(id) ? Results.NoContent() : Results.NotFound());

        // ── Dependencies ──────────────────────────────────────────

        group.MapPost("/dependencies", (GanttDependency dep, MockGanttStore s) =>
        {
            var created = s.AddDependency(dep);
            return Results.Created($"/api/gantt/dependencies/{created.Id}", created);
        });

        group.MapDelete("/dependencies/{id}", (string id, MockGanttStore s) =>
            s.RemoveDependency(id) ? Results.NoContent() : Results.NotFound());

        // ── Baselines ─────────────────────────────────────────────

        group.MapPost("/baselines", (GanttBaseline baseline, MockGanttStore s) =>
        {
            var created = s.AddBaseline(baseline);
            return Results.Created($"/api/gantt/baselines/{created.Id}", created);
        });

        // ── Resource Calendars ────────────────────────────────────

        group.MapPut("/resource-calendars/{assigneeId}", (string assigneeId, GanttResourceCalendar calendar, MockGanttStore s) =>
        {
            calendar.AssigneeId = assigneeId;
            s.UpdateResourceCalendar(calendar);
            return Results.NoContent();
        });

        // ── History ───────────────────────────────────────────────

        group.MapPost("/history", (TmActivityEntry entry, MockGanttStore s) =>
        {
            var created = s.AddHistory(entry);
            return Results.Created($"/api/gantt/history/{created.Id}", created);
        });
    }
}
