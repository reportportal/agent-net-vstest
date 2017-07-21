#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#addin nuget:?package=SharpZipLib
#addin nuget:?package=Cake.Compression
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./src/ReportPortal.VSTest/bin") + Directory(configuration);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./src/ReportPortal.VSTest.sln");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild("./src/ReportPortal.VSTest.sln", settings =>
        settings.SetConfiguration(configuration));
    }
    else
    {
      // Use XBuild
      XBuild("./src/ReportPortal.VSTest.sln", settings =>
        settings.SetConfiguration(configuration));
    }
});

Task("Zip")
	.IsDependentOn("Build")
	.Does(() =>
{
  var files=new [] {
    new FilePath("./src/ReportPortal.VSTest/bin/Release/ReportPortal.VSTest.dll"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/ReportPortal.VSTest.dll.config"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/RestSharp.dll"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/RestSharp.xml"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/Newtonsoft.Json.dll"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/ReportPortal.Client.dll"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/ReportPortal.Shared.dll"),
    new FilePath("./src/ReportPortal.VSTest/bin/Release/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll"),
   };
  ZipCompress("./src/ReportPortal.VSTest/bin/Release/", "ReportPortal.VSTest.zip", files);
}
);

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Zip");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
