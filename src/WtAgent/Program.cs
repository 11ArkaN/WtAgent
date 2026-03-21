using System.Text.Json;
using WtAgent;

var serializerOptions = JsonDefaults.Create();
var app = new CliApplication(serializerOptions);
var exitCode = await app.RunAsync(args);
Environment.ExitCode = exitCode;
