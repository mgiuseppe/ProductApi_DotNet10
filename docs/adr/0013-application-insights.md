# 0013. Application Insights with the Azure Monitor OpenTelemetry distro

- Status: Accepted
- Date: 2026-11-07

## Context
The API is instrumented with OpenTelemetry and exports over OTLP to the Aspire dashboard locally. On Azure it needs a backend, and a container that fails to start must leave logs somewhere.

## Decision
- One Log Analytics workspace and one Application Insights per environment.
- The API exports with the Azure Monitor distro when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (on Azure); otherwise OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- Ingestion uses Entra authentication: local authentication is off, the API identity has *Monitoring Metrics Publisher*.
- The Container Apps environment sends console and system logs to the same workspace.
- 30 days retention, daily cap of 0.1 GB.

## Consequences
- Requests, dependencies, exceptions, metrics and Live Metrics in Application Insights.
- An Azure-specific package in `Api`.
- Over the daily cap, telemetry stops until the next day.

## Alternatives considered
- **Managed OpenTelemetry agent of Container Apps**: no code change, but no metrics or Live Metrics in Application Insights.
- **OpenTelemetry Collector**: provider-neutral, one more component to run.
