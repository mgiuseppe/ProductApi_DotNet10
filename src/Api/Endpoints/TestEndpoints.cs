using System;

namespace Api.Endpoints;

public static class TestEndpoints
{
    public static WebApplication MapTestEndpointsInDevelopment(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // Temporary demo endpoints: remove after the demo
            app.MapGet("/slow", async (int? ms, CancellationToken ct) =>
            {
                var delay = ms ?? Random.Shared.Next(50, 1500);
                await Task.Delay(delay, ct);
                return Results.Ok(new { delayMs = delay });
            });

            app.MapGet("/boom", () =>
            {
                throw new InvalidOperationException("boom");
            });
        }

        return app;
    }
}
