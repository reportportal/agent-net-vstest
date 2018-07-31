[![Build status](https://ci.appveyor.com/api/projects/status/0bgatrnrtl1r1prm/branch/master?svg=true)](https://ci.appveyor.com/project/nvborisenko/agent-net-vstest/branch/master)

# Installation
[![NuGet version](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger.svg)](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger)

Install **ReportPortal.VSTest.TestLogger** NuGet package into your project with tests.

# Configuration
The plugin has *ReportPortal.conf* file with configuration of the integration.

Example of config file:
```json
{
  "enabled": true,
  "server": {
    "url": "https://rp.epam.com/api/v1/",
    "project": "default_project",
    "authentication": {
      "uuid": "7853c7a9-7f27-43ea-835a-cab01355fd17"
    }
  },
  "launch": {
    "name": "VS Test Demo Launch",
    "description": "this is description",
    "debugMode": true,
    "tags": [ "t1", "t2" ]
  }
}
```
# Results publishing
To publish test results in real-time to the ReportPortal specify `Logger` argument.

## For vstest.console.exe
```cmd
vstest.console.exe MyTests.dll /TestAdapterPath:. /Logger:ReportPortal
```
## For dotNet CLI
```cmd
dotnet vstest MyTests.dll --logger:ReportPortal
```

Some parameters can be overrided:
```cmd
--logger:ReportPortal;Launch.Name="My new launch name"
```

