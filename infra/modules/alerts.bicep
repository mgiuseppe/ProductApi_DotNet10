// Email alerts on the API of one environment: what the platform probes cannot fix by themselves.
// Metric alerts on Container Apps platform metrics, one time series each (no splitting by dimension): within the
// 10 free monitored time series per month of Azure Monitor. No replica (scaled to zero) means no data and no alert.
param appName string
param environmentName string
param tags object

@description('Resource id of the API container app.')
param containerAppId string

@description('Recipient of the alert emails, from a GitHub secret.')
param alertEmail string

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${appName}-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: take('${appName}-${environmentName}', 12) // max 12 characters, shown in the email
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

var alerts = [
  {
    name: 'restarts'
    description: 'The API restarted more than 3 times: liveness probe failures or crashes.'
    severity: 1
    windowSize: 'PT5M'
    // Cumulative per replica since it was created: Maximum is the restarts of the worst replica, whatever the window
    metricName: 'RestartCount'
    timeAggregation: 'Maximum'
    threshold: 3
    dimensions: []
  }
  {
    name: 'server-errors'
    description: 'The API returned 5xx responses.'
    severity: 2
    windowSize: 'PT5M'
    metricName: 'Requests'
    timeAggregation: 'Total'
    threshold: 0
    dimensions: [
      { name: 'statusCodeCategory', operator: 'Include', values: ['5xx'] }
    ]
  }
]

resource metricAlerts 'Microsoft.Insights/metricAlerts@2026-01-01' = [
  for alert in alerts: {
    name: 'alert-${appName}-${alert.name}-${environmentName}'
    location: 'global'
    tags: tags
    properties: {
      description: alert.description
      severity: alert.severity
      enabled: true
      scopes: [containerAppId]
      evaluationFrequency: 'PT5M'
      windowSize: alert.windowSize
      autoMitigate: true // resolves by itself when the metric goes back under the threshold
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: alert.metricName
            metricNamespace: 'Microsoft.App/containerApps'
            metricName: alert.metricName
            timeAggregation: alert.timeAggregation
            operator: 'GreaterThan'
            threshold: alert.threshold
            dimensions: alert.dimensions
          }
        ]
      }
      actions: [
        { actionGroupId: actionGroup.id }
      ]
    }
  }
]
