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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Phimath.Secex.Server;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                            DynamicallyAccessedMemberTypes.PublicProperties)]
public class NotificationOptions
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(3);

    public bool Enabled { get; init; } = false;

    public string? ServerAddress { get; init; }
    public int ServerPort { get; init; }

    public string? SenderEmail { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseSsl { get; init; }

    public string? Recipients { get; init; }
}

public interface INotificationSender
{
    void Register(string uploadId, string fileName);

    Task<bool> Send();
}

public sealed class NotificationTask : BackgroundService
{
    private readonly PeriodicTimer _timer;
    private readonly INotificationSender _sender;

    public NotificationTask(IOptions<NotificationOptions> notificationOptions, INotificationSender sender)
    {
        _sender = sender;
        _timer = new PeriodicTimer(notificationOptions.Value.Interval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _timer.WaitForNextTickAsync(stoppingToken);
            await _sender.Send();
        }
    }
}

public sealed class SmtpNotificationSender : INotificationSender
{
    private const string Subject = "New File(s) Uploaded";
    private static readonly string HtmlStart;
    private static readonly string HtmlEnd;

    static SmtpNotificationSender()
    {
        HtmlStart = new StringBuilder()
            .Append(
                "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"https://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">")
            .Append(
                "<html xmlns=\"https://www.w3.org/1999/xhtml\" lang=\"en\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" style=\"min-height: 100%;background: #ffffff\">")
            .Append("<head>")
            .Append("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width\">")
            .Append("<meta name=\"eventId\" content=\"secex-files-uploaded\">")
            .Append("<title>").Append(Subject).Append("</title>")
            .Append("<style>")
            .Append("@media only screen{html{min-height:100%;background:#ffffff;}}")
            .Append(
                "@media (min-resolution: 1dpi){body[data-outlook-cycle] .main-container > table{width:640px;}body[data-outlook-cycle] .container-wide .main-container > table{width:888px;}}")
            .Append(
                "body{font-family:Segoe UI,SegoeUI,Roboto,'Helvetica Neue',Arial,sans-serif;display:flex;flex-direction:column;max-width:640px;margin-left:auto;margin-right:auto}")
            .Append("ul{padding:0 0.5em;margin-top:0}")
            .Append(
                "li{margin-bottom:15px;mso-margin-bottom-alt:15px;margin-left:17px;mso-margin-left-alt:17px;padding:0;mso-padding-alt:0;-webkit-padding-start:0}")
            .Append("p+p{margin-top:0}")
            .Append("p+ul{margin-top:0}")
            .Append(".content{padding:0.5em}")
            .Append("header{background:#14213D}")
            .Append("header h1{text-align:center;color:rgba(255,255,255,0.85);margin:0.4em 0;font-weight:500}")
            .Append("</style>")
            .Append("</head>")
            .Append("<body>")
            .Append("<header><h1>Secure File Exchange</h1></header>")
            .Append("<div class=\"content\">")
            .Append("<p>New Files have been uploaded to your SecEx instance.</p>")
            .ToString();

        HtmlEnd = "</div></body></html>";
    }

    private readonly string _server;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string _sender;
    private readonly IReadOnlyCollection<string> _recipients;
    private readonly ILogger<SmtpNotificationSender> _logger;

    private readonly ConcurrentBag<(string uploadId, string fileName)> _fileNames = new();

    public SmtpNotificationSender(
        string server,
        int port,
        bool useSsl,
        string? username,
        string? password,
        string sender,
        IReadOnlyCollection<string> recipients,
        ILogger<SmtpNotificationSender> logger)
    {
        _server = server;
        _port = port;
        _useSsl = useSsl;
        _username = username;
        _password = password;
        _sender = sender;
        _recipients = recipients;
        _logger = logger;
    }

    public void Register(string uploadId, string fileName)
    {
        _fileNames.Add((uploadId, fileName));
    }

    public async Task<bool> Send()
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Secure File Exchange", _sender));
            foreach (var recipient in _recipients)
            {
                message.To.Add(new MailboxAddress(recipient, recipient));
            }

            message.Subject = Subject;

            var bodyBuilder = new StringBuilder(HtmlStart);

            var fileNamesByUploadId = new Dictionary<string, List<string>>();
            while (_fileNames.TryTake(out var tuple))
            {
                if (!fileNamesByUploadId.TryGetValue(tuple.uploadId, out var fileNames))
                {
                    fileNames = new List<string>();
                }

                fileNames.Add(tuple.fileName);
                fileNamesByUploadId[tuple.uploadId] = fileNames;
            }

            if (fileNamesByUploadId.Count == 0)
            {
                return false;
            }

            foreach (var (uploadId, fileNames) in fileNamesByUploadId)
            {
                bodyBuilder
                    .Append("<p>The new files in folder '").Append(uploadId).Append("' are:</p>")
                    .Append("<ul>");

                foreach (var fileName in fileNames)
                {
                    bodyBuilder.Append("<li>").Append(fileName).Append("</li>");
                }

                bodyBuilder.Append("</ul>");
            }

            bodyBuilder.Append(HtmlEnd);

            message.Body = new TextPart("html")
            {
                Text = bodyBuilder.ToString(),
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_server, _port, _useSsl);

            if (_username != null && _password != null)
            {
                await client.AuthenticateAsync(_username, _password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification via email");
            return false;
        }
    }
}

public sealed class NullNotificationSender : INotificationSender
{
    private static readonly Task<bool> ReturnValue = Task.FromResult(false);

    public void Register(string uploadId, string fileName)
    {
    }

    public Task<bool> Send()
    {
        return ReturnValue;
    }
}