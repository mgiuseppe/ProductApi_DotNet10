# 0020. Application Insights with the Azure Monitor OpenTelemetry distro

- Status: Accepted
- Date: 2026-11-05

## Context
The API is instrumented with OpenTelemetry and exports over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (the Aspire dashboard locally). On Azure nothing is set, so traces, metrics and logs are lost, and the Container Apps environment has no log destination: a container that fails to start leaves no trace.

## Decision
**One Log Analytics workspace and one workspace-based Application Insights per environment; the API exports to it with the Azure Monitor OpenTelemetry distro (`Azure.Monitor.OpenTelemetry.AspNetCore`).**
- The distro is enabled only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (Bicep sets it on Azure); locally OTLP to the Aspire dashboard stays as it is.
- Entra authentication for ingestion: local authentication is disabled on Application Insights, the API identity has *Monitoring Metrics Publisher* on it. The connection string is not a secret.
- The Container Apps environment sends console and system logs to the same workspace (destination `azure-monitor` with a diagnostic setting, no shared key). This also covers the migrator, which has no OpenTelemetry.
- Workspace: pay per GB, 30 days retention, daily cap of 0.1 GB per environment.

## Consequences
- Requests, dependencies, exceptions, traces, metrics and Live Metrics in Application Insights; system logs in the workspace.
- An Azure-specific package in `Api`; Application and Infrastructure are unchanged.
- The deployment identities may now grant two roles instead of one: the ABAC condition in `scripts/cd-setup.sh` allows *Key Vault Secrets User* and *Monitoring Metrics Publisher*. The script must be run again on existing setups.
- API logs are stored twice (OpenTelemetry logs and console logs): accepted, the daily cap bounds the cost.
- Over the daily cap ingestion stops until the next day: telemetry can be missing on busy days.

## Alternatives considered
- **Managed OpenTelemetry agent of Container Apps** (no code change, OTLP from the app): sends only traces and logs to Application Insights, no metrics and no Live Metrics, and is configured per environment rather than per app.
- **OpenTelemetry Collector with OTLP to any backend**: vendor neutral, but one more component to run, and no other backend is planned.
- **Only environment logs in Log Analytics**: no traces or metrics from the app.
