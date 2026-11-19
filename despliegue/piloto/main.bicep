targetScope = 'resourceGroup'

@description('Región de la oferta gratuita de SQL de esta suscripción; confirmar antes de crear.')
param location string = 'canadacentral'
@minLength(3)
@maxLength(24)
param suffix string
@secure()
param sqlAdminPassword string
@secure()
param sqlAppPassword string
@description('IP pública exacta del operador para instalar la base; quitar al terminar.')
param operatorIp string

var webName = 'motherson-piloto-${suffix}'
var sqlName = 'motherson-piloto-${suffix}'
var dataRoot = 'D:\\home\\motherson-piloto'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${webName}-f1'
  location: location
  sku: { name: 'F1', tier: 'Free', capacity: 1 }
  properties: { reserved: false }
}
resource sql 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlName
  location: location
  properties: {
    administratorLogin: 'piloto_instalador'
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}
resource db 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sql
  name: 'Motherson_L1'
  location: location
  sku: { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    maxSizeBytes: 34359738368
    minCapacity: json('0.5')
    autoPauseDelay: 15
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    requestedBackupStorageRedundancy: 'Local'
  }
}
resource installerFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sql
  name: 'OperadorTemporal'
  properties: { startIpAddress: operatorIp, endIpAddress: operatorIp }
}
resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: webName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      use32BitWorkerProcess: true
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      // Cierre inicial: solo el operador accede hasta aprobar y aprovisionar participantes.
      ipSecurityRestrictionsDefaultAction: 'Deny'
      ipSecurityRestrictions: [
        { name: 'OperadorTemporal', ipAddress: '${operatorIp}/32', action: 'Allow', priority: 100 }
      ]
      scmIpSecurityRestrictionsUseMain: false
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Pilot__Enabled', value: 'true' }
        { name: 'MVP_SQL_TRANSACCIONAL', value: 'true' }
        { name: 'MVP_AZURE_SERVER', value: '${sqlName}.database.windows.net' }
        { name: 'MVP_BASE_COMPARTIDA', value: 'Motherson_L1' }
        { name: 'MVP_ROOT', value: dataRoot }
        { name: 'MVP_KEYS_PATH', value: '${dataRoot}\\keys' }
        { name: 'MVP_IMAGES_PATH', value: '${dataRoot}\\imagenes' }
        { name: 'AllowedHosts', value: '*.azurewebsites.net' }
        { name: 'PowerBI__Mode', value: 'AppOwnsData' }
        { name: 'PowerBI__CapacityReady', value: 'false' }
        { name: 'PowerBI__AllowTrialTokens', value: 'false' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'ConnectionStrings__Sql', value: 'Server=tcp:${sqlName}.database.windows.net,1433;Database=Motherson_L1;User Id=portal_app;Password="${replace(sqlAppPassword,'"','""')}";Encrypt=True;TrustServerCertificate=False;Connect Timeout=90' }
      ]
    }
  }
}
output webAppName string = app.name
output candidateUrl string = 'https://${app.properties.defaultHostName}'
output sqlServerName string = sql.name
// No se muestran cadenas ni claves. El backend no tendrá acceso SQL hasta permitir sus IP exactas.
