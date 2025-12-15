#:sdk Aspire.AppHost.Sdk@13.2.0-preview.1.25611.1
#:package Aspire.Hosting.JavaScript@13.2.0-preview.1.25611.1
#:package Aspire.Hosting.Azure.AppContainers@13.2.0-preview.1.25611.1

#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

// Conditionally adds parameters to app model if config values are specified.
var customDomainValue = builder.Configuration["Parameters:customDomain"];
var certificateNameValue = builder.Configuration["Parameters:certificateName"];

builder.AddAzureContainerAppEnvironment("env");

var website = builder.AddCSharpApp("website", "./src/Hex1b.Website")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var content = builder.AddViteApp("content", "./src/content")
    .WithReference(website)
    .WaitFor(website);

website.PublishWithContainerFiles(content, "./wwwroot");

builder.Build().Run();