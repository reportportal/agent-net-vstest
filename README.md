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
    "attributes": [ "t1", "os:win10" ]
  }
}
```

[More](https://github.com/reportportal/commons-net/blob/develop/docs/Configuration.md) about configuration.

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
  </RunConfiguration>
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

Complex parameters such as `Launch.Attributes` can be overriden as `k1:v1,k2:v2`. `,` is reserved as a delimiter of launch attributes.

# Environment variables
It's possible to override parameters via environment variables.
```cmd
set reportportal_launch_name="My new launch name"
# execute tests
```

`reportportal_` prefix is used for naming variables, and `_` is used as delimeter. For example to override `Server.Authentication.Uuid` parameter, we need specify `ReportPortal_Server_Authentication_Uuid` in environment variables. To override launch tags we need specify `ReportPortal_Launch_Attributes` with `tag1;os:win7` value (`;` used as separator for list of values).

# Integrate logger framework
- [NLog](https://github.com/reportportal/logger-net-nlog)
- [log4net](https://github.com/reportportal/logger-net-log4net)
- [Serilog](https://github.com/reportportal/logger-net-serilog)
- [System.Diagnostics.TraceListener](https://github.com/reportportal/logger-net-tracelistener)

And [how](https://github.com/reportportal/commons-net/blob/master/docs/Logging.md) you can improve your logging experience with attachments or nested steps.


# Useful extensions
- [SourceBack](https://github.com/nvborisenko/reportportal-extensions-sourceback) adds piece of test code where test was failed
- [Insider](https://github.com/nvborisenko/reportportal-extensions-insider) brings more reporting capabilities without coding like methods invocation as nested steps


# License
ReportPortal is licensed under [Apache 2.0](https://github.com/reportportal/agent-net-vstest/blob/master/LICENSE)

We use Google Analytics for sending anonymous usage information as library's name/version and the agent's name/version when starting launch. This information might help us to improve integration with ReportPortal. Used by the ReportPortal team only and not for sharing with 3rd parties. You are able to [turn off](https://github.com/reportportal/commons-net/blob/master/docs/Configuration.md#analytics) it if needed.
