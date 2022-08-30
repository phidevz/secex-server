/*
  ---------------------------------------------------------------------
  secex - Secure File Exchange                                         
  Copyright (c) 2022  phidevz                                          
                                                                       
  This program is free software: you can redistribute it and/or modify 
  it under the terms of the GNU General Public License as published by 
  the Free Software Foundation, either version 3 of the License, or    
  (at your option) any later version.                                  
                                                                       
  This program is distributed in the hope that it will be useful,      
  but WITHOUT ANY WARRANTY; without even the implied warranty of       
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the        
  GNU General Public License for more details.                         
                                                                       
  You should have received a copy of the GNU General Public License    
  along with this program. If not, see <https://www.gnu.org/licenses/>.
  ---------------------------------------------------------------------
*/

using System.Reflection;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using Phimath.Secex.Server;

var browseFilesEnabled = false;
var browseFilesFromEnvironment = Environment.GetEnvironmentVariable("SECEX_ENABLE_BROWSE_FILES");
if (browseFilesFromEnvironment != null)
{
    if (!bool.TryParse(browseFilesFromEnvironment, out browseFilesEnabled))
    {
        await Console.Error.WriteLineAsync(
            $"Environment Variable 'SECEX_ENABLE_BROWSE_FILES' should be 'true' or 'false', but was {browseFilesFromEnvironment}");
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("SECEX_");
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Result.OkObjectResult", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddOptions<NotificationOptions>()
    .BindConfiguration("Notifications")
    .Validate(options => !options.Enabled || !string.IsNullOrEmpty(options.ServerAddress),
        "Mail Server address must not be empty")
    .Validate(options => !options.Enabled || options.ServerPort != 0,
        "Mail Server port must be set")
    .Validate(options => !options.Enabled || !string.IsNullOrEmpty(options.SenderEmail),
        "Sender mail must not be empty")
    .Validate(options => !options.Enabled || !string.IsNullOrEmpty(options.Recipients),
        "Mail recipients must not be empty")
    .Validate(options => !options.Enabled || (options.Username == null) == (options.Password == null),
        "Either both username and password must be NULL (no auth) or both must be set")
    .ValidateOnStart();
builder.Services.Configure<HostFilteringOptions>(o => o.AllowedHosts = new List<string> { "*" });
builder.Services.AddHostedService<NotificationTask>();
builder.Services.AddSingleton<INotificationSender>(services =>
{
    var configuration = services.GetRequiredService<IOptions<NotificationOptions>>().Value;
    if (!configuration.Enabled)
    {
        return new NullNotificationSender();
    }

    return new SmtpNotificationSender(
        configuration.ServerAddress!,
        configuration.ServerPort,
        configuration.UseSsl,
        configuration.Username,
        configuration.Password,
        configuration.SenderEmail!,
        configuration.Recipients!.Split(";"),
        services.GetRequiredService<ILogger<SmtpNotificationSender>>());
});
builder.Services.AddResponseCompression();
builder.Services.AddCors();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().WithExposedHeaders("Content-Disposition"));

app.MapGet("/keys", Endpoints.GetKeys);
app.MapMethods("/upload/{id}", new[] { "HEAD" }, Endpoints.TestUpload);
app.MapPost("/upload/{id}", Endpoints.Upload);
app.MapMethods("/d/{id}/{fileName}", new[] { "GET", "HEAD" }, Endpoints.DownloadFile);
app.MapMethods("/d/{id}", new[] { "HEAD", "OPTIONS" }, browseFilesEnabled
    ? Endpoints.Download
    : Endpoints.DownloadDummy);

app.UseResponseCompression();

app.Run();