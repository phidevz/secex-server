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

namespace Phimath.Secex.Server;

internal static class Endpoints
{
    public static async Task<IResult> GetKeys(HttpRequest request)
    {
        var sp = request.HttpContext.RequestServices;
        var environment = sp.GetRequiredService<IWebHostEnvironment>();

        var keysDirectory = Path.Join(environment.ContentRootPath, "keys");
        if (!Directory.Exists(keysDirectory))
        {
            return Results.Ok(Array.Empty<string>());
        }

        var keys = await Task.WhenAll(Directory
            .EnumerateFiles(keysDirectory, "*.public.asc")
            .Select(file => File.ReadAllTextAsync(file)));

        return Results.Ok(keys);
    }

    public static Task<IResult> DownloadFile(HttpRequest request, string id, string fileName)
    {
        var sp = request.HttpContext.RequestServices;
        var environment = sp.GetRequiredService<IWebHostEnvironment>();

        var targetFile = Path.Join(environment.ContentRootPath, "serve", id, fileName);

        if (!File.Exists(targetFile))
        {
            return Task.FromResult(Results.NotFound());
        }

        return Task.FromResult(Results.File(targetFile, "application/pgp-encrypted", fileName));
    }

    public static Task<IResult> Download(HttpRequest request, string id)
    {
        var sp = request.HttpContext.RequestServices;
        var environment = sp.GetRequiredService<IWebHostEnvironment>();

        var targetDirectory = Path.Join(environment.ContentRootPath, "serve", id);
        if (!Directory.Exists(targetDirectory))
        {
            Results.NotFound();
        }

        var files = Directory.EnumerateFileSystemEntries(targetDirectory, "*.gpg", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToArray();
        return Task.FromResult(Results.Ok(files));
    }

    public static async Task<IResult> Upload(HttpRequest request, string id)
    {
        var sp = request.HttpContext.RequestServices;
        var lf = sp.GetRequiredService<ILoggerFactory>();
        var environment = sp.GetRequiredService<IWebHostEnvironment>();
        var logger = lf.CreateLogger("Main");
        if (!request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var targetDirectory = Path.Join(environment.ContentRootPath, "drop", id);
        if (!Directory.Exists(targetDirectory))
        {
            return Results.BadRequest();
        }

        var form = await request.ReadFormAsync();
        var files = form.Files;
        logger.LogInformation("Number of files: {Length}", files.Count);

        foreach (var file in files.GetFiles("files"))
        {
            logger.LogInformation("Length of '{Name}': {Length}", file.FileName, file.Length);
            var targetFile = Path.Join(targetDirectory, file.FileName);
            if (File.Exists(targetFile))
            {
                var filenameWithoutExtensions = Path.GetFileNameWithoutExtension(file.FileName);
                var extension = Path.GetExtension(file.FileName);
                var newFileName = $"{filenameWithoutExtensions}-{Path.GetRandomFileName()}{extension}";
                targetFile = Path.Join(targetDirectory, newFileName);
            }

            await using var targetStream = File.Create(targetFile, 4 * 1024 * 1024);
            await using var read = file.OpenReadStream();
            await read.CopyToAsync(targetStream);
        }

        return Results.Ok("ok");
    }

    public static Task<IResult> DownloadDummy(HttpRequest request, string id)
    {
        return Task.FromResult(Results.StatusCode(403));
    }

    public static Task<IResult> TestUpload(HttpRequest request, string id)
    {
        var sp = request.HttpContext.RequestServices;
        var environment = sp.GetRequiredService<IWebHostEnvironment>();

        var targetDirectory = Path.Join(environment.ContentRootPath, "drop", id);
        return Task.FromResult(Directory.Exists(targetDirectory)
            ? Results.Ok()
            : Results.NotFound());
    }
}