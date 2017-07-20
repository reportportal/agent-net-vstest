# agent-net-vstest
Logger extension for vstest.console.exe

# Installation
Download **agent-net-vstest**

# Configuration
The plugin has *ReportPortal.VSTest.dll.config* file with configuration of the integration.

Example of config file:
```xml
<configuration>
  <configSections>
    <section name="reportPortal" type="ReportPortal.VSTest.ReportPortalSection, ReportPortal.VSTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"/>
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
# Plugin connection
To use the "agent-net-vstest” plugin you need to perform the following steps:
- run the script build.cmd which will create “ReportPortal.VSTest.zip” archive file;
- copy all files from “ReportPortal.VSTest.zip” to a folder “Extensions” which is in the same directory as VSTest.console.exe e.g. (c:/Program Files (x86)/ Microsoft Visual Studio/2017/Professional/Common7/IDE/CommonExtensions/Microsoft/TestWindow/);
- run VSTest.console.exe with the following command-line option - "/logger:ReportPortalVSTest".

# Tags
If you use Microsoft.VisualStudio.TestTools.UnitTesting and you want add tags for you test, please add "TestPropertyAttribute" with name "Category". For example:

	[TestProperty("Category", "My tag")]
        public void TestMethod()
        {}

If you use NUnit.Framework and you want add tags for you test, please add "TestCategoryAttribute". For example:

	[TestCategory("My tag")]
        public void TestMethod()
        {}