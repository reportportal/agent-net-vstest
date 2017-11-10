[![Build status](https://ci.appveyor.com/api/projects/status/0bgatrnrtl1r1prm/branch/master?svg=true)](https://ci.appveyor.com/project/nvborisenko/agent-net-vstest/branch/master)

# Installation
[![NuGet version](https://badge.fury.io/nu/reportportal.vstest.testadapter.svg)](https://badge.fury.io/nu/reportportal.vstest.testadapter)

Install **ReportPortal.VSTest.TestAdapter** NuGet package into your project with tests.

# Configuration
The plugin has *ReportPortal.VSTest.TestAdapter.dll.config* file with configuration of the integration.

Example of config file:
```xml
<configuration>
  <configSections>
    <section name="reportPortal" type="ReportPortal.VSTest.TestAdapter.ReportPortalSection, ReportPortal.VSTest.TestAdapter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"/>
  </configSections>
  <reportPortal enabled="true" logConsoleOutput="true">
    <server url="https://rp.epam.com/api/v1/" project="default_project">
      <authentication username="default" password="45c00b4f-a893-4365-89be-8c1b89e30ffb" />
      <!-- <proxy server="host:port"/> -->
    </server>
    <launch name="VSTest Demo Launch" debugMode="true" tags="t1,t2" />
  </reportPortal>
</configuration>
```
# Results publishing
To publish test results in real-tim to the ReportPortal specify `Logger` argument.

## For vstest.console.exe
```cmd
vstest.console.exe MyTests.dll /TestAdapterPath:. /Logger:ReportPortal
```
## For dotNet CLI
```cmd
dotnet test MyTests.dll -a:. -l:ReportPortal
```
