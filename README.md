[![Build status](https://ci.appveyor.com/api/projects/status/0bgatrnrtl1r1prm/branch/master?svg=true)](https://ci.appveyor.com/project/nvborisenko/agent-net-vstest/branch/master)

# Installation
[![NuGet version](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger.svg)](https://badge.fury.io/nu/ReportPortal.VSTest.TestLogger)

Install **ReportPortal.VSTest.TestLogger** NuGet package into your project with tests.

# Configuration
Add new **ReportPortal.config.json** file into your project with *Copy if newer* value for *Copy to Output Directory* property.

Example of config file:
```json
{
  "$schema": "https://raw.githubusercontent.com/reportportal/agent-net-vstest/master/src/ReportPortal.VSTest.TestLogger/ReportPortal.config.schema",
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

## Visual Studio Test Explorer
Add the `*.runsettings` file into solution with the following minimal content
```xml
<?xml version="1.0" encoding="UTF-8"?>
<RunSettings>
  <RunConfiguration>
    <TestAdaptersPaths>.</TestAdaptersPaths>
  </RunConfiguration>>
  <LoggerRunSettings>
    <Loggers>
      <Logger friendlyName="ReportPortal">
        <Configuration>
          <Launch.Description>Ran from Visual Studio Test Explorer</Launch.Description>
        </Configuration>
      </Logger>
    </Loggers>
  </LoggerRunSettings>
</RunSettings>

```

In `Test Explorer` window select this file as run configuration (menu Test -> Test Settings -> Select Test Settings File).

Now you can execute tests in Visual Studio and see results on the server. `Launch.Description` property in xml is provided as an exmple how to put configuration properties for logger. 

# Parameters overriding
```cmd
--logger:ReportPortal;Launch.Name="My new launch name"
```

## Supported parameters
- `Launch.Name`
- `Launch.Description`
- `Launch.Tags` - comma-separated list
- `Launch.DebugMode` - true/false

- `Server.Project`
- `Server.Authentication.Uuid`

# Environment variables
It's possible to override parameters via environment variables.
```cmd
set reportportal_launch_name="My new launch name"
# execute tests
```

`reportportal_` prefix is used for naming variables, and `_` is used as delimeter. For example to override `Server.Authentication.Uuid` parameter, we need specify `ReportPortal_Server_Authentication_Uuid` in environment variables. To override launch tags we need specify `ReportPortal_Launch_Tags` with `tag1;tag2` value (`;` used as separator for list of values).

# Integrate logger framework
- [NLog](https://github.com/reportportal/logger-net-nlog)
- [log4net](https://github.com/reportportal/logger-net-log4net)
- [Serilog](https://github.com/reportportal/logger-net-serilog)
- [System.Diagnostics.TraceListener](https://github.com/reportportal/logger-net-tracelistener)

# Useful extensions
- [SourceBack](https://github.com/nvborisenko/reportportal-extensions-sourceback)
