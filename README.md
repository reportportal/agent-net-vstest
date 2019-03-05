[![Build status](https://ci.appveyor.com/api/projects/status/0bgatrnrtl1r1prm/branch/master?svg=true)](https://ci.appveyor.com/project/nvborisenko/agent-net-vstest/branch/master)

# Installation
[![NuGet version](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger.svg)](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger)

Install **ReportPortal.VSTest.TestLogger** NuGet package into your project with tests.

# Configuration
Add new **ReportPortal.config.json** file into your project with *Copy if newer* value for *Copy to Output Directory* property.

Example of config file:
```json
{
  "$schema": "https://raw.githubusercontent.com/reportportal/agent-net-vstest/master/ReportPortal.VSTest.TestLogger/ReportPortal.config.schema",
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
# Tests execution
To execute tests with real-time reporting, specify `Logger` argument.

## vstest.console.exe
```cmd
vstest.console.exe MyTests.dll /TestAdapterPath:. /Logger:ReportPortal
```
## dotnet test
```cmd
dotnet test -l:ReportPortal
dotnet vstest MyTests.dll --logger:ReportPortal
```

# Parameters overriding
```cmd
--logger:ReportPortal;Launch.Name="My new launch name"
```

## Supported parameters
- `Launch.Name`
- `Launch.Description`
- `Launch.Tags` - comma-separated list
- `Launch.IsDebugMode` - true/false

- `Server.Project`
- `Server.Authentication.Uuid`

# Environment variables
It's possible to override parameters via environment variables.
```cmd
set reportportal_launch_name="My new launch name"
# execute tests
```

`reportportal_` prefix is used for naming variables, and `_` is used as delimeter. For example to override `Server.Authentication.Uuid` parameter, we need specify `ReportPortal_Server_Authentication_Uuid` in environment variables. 
