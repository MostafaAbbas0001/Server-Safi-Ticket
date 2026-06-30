using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Safi_Ticket.DTO.Settings;

namespace Safi_Ticket.Services
{
    public class EmailReaderWorker : BackgroundService
    {
        private const int FallbackPollSeconds = 300;
        private const int ReconnectDelaySeconds = 30;
        private static readonly TimeSpan IdleRefreshInterval = TimeSpan.FromMinutes(9);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailReaderWorker> _logger;
        private readonly EmailSettings _settings;

        public EmailReaderWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailReaderWorker> logger,
            IOptions<EmailSettings> settings
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailReaderWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WatchMailboxAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while reading emails.");
                }

                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), stoppingToken);
            }
        }

        private async Task WatchMailboxAsync(CancellationToken cancellationToken)
        {
            ValidateSettings();

            using var client = new ImapClient();

            _logger.LogInformation(
                "Connecting to IMAP server {Host}:{Port} as {Username}.",
                _settings.Host,
                _settings.Port,
                _settings.Username
            );

            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.SslOnConnect,
                cancellationToken
            );

            client.AuthenticationMechanisms.Remove("XOAUTH2");
            client.AuthenticationMechanisms.Remove("OAUTHBEARER");
            client.AuthenticationMechanisms.Remove("NTLM");

            try
            {
                await client.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password,
                    cancellationToken
                );
            }
            catch (AuthenticationException exception)
            {
                _logger.LogError(
                    exception,
                    "Outlook rejected IMAP authentication for {Username}. Check the email address, app password, and whether IMAP/password access is enabled for this mailbox.",
                    _settings.Username
                );
                throw;
            }

            var folder = await client.GetFolderAsync(_settings.Mailbox, cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            await ProcessUnreadEmailsAsync(folder, cancellationToken);

            if (!client.Capabilities.HasFlag(ImapCapabilities.Idle))
            {
                _logger.LogWarning(
                    "IMAP server does not support IDLE. Falling back to checking unread emails every {PollSeconds} seconds.",
                    FallbackPollSeconds
                );

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(FallbackPollSeconds), cancellationToken);
                    await ProcessUnreadEmailsAsync(folder, cancellationToken);
                }

                return;
            }

            using var newMailSignal = new SemaphoreSlim(0);

            void OnCountChanged(object? sender, EventArgs args)
            {
                newMailSignal.Release();
            }

            folder.CountChanged += OnCountChanged;

            try
            {
                _logger.LogInformation("Watching mailbox with IMAP IDLE for new emails.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var idleDone = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken
                    );
                    var idleTask = client.IdleAsync(idleDone.Token, cancellationToken);

                    var signalTask = newMailSignal.WaitAsync(cancellationToken);
                    var timeoutTask = Task.Delay(IdleRefreshInterval, cancellationToken);
                    await Task.WhenAny(signalTask, timeoutTask);

                    await idleDone.CancelAsync();

                    try
                    {
                        await idleTask;
                    }
                    catch (OperationCanceledException)
                        when (!cancellationToken.IsCancellationRequested)
                    {
                        // Ending IDLE is how we return to normal IMAP commands.
                    }

                    await ProcessUnreadEmailsAsync(folder, cancellationToken);
                }
            }
            finally
            {
                folder.CountChanged -= OnCountChanged;
                await client.DisconnectAsync(true, cancellationToken);
            }
        }

        private async Task ProcessUnreadEmailsAsync(
            IMailFolder folder,
            CancellationToken cancellationToken
        )
        {
            _logger.LogInformation("Checking inbox for unread emails...");

            var unreadEmails = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken);

            _logger.LogInformation("Unread emails found: {Count}", unreadEmails.Count);

            foreach (var uid in unreadEmails)
            {
                _logger.LogInformation("Processing email UID: {Uid}", uid.Id);

                var message = await folder.GetMessageAsync(uid, cancellationToken);

                using var scope = _scopeFactory.CreateScope();
                var emailIngestionService =
                    scope.ServiceProvider.GetRequiredService<EmailIngestionService>();

                await emailIngestionService.IngestEmailAsync(
                    message,
                    $"{folder.FullName}-{uid.Id}"
                );

                await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
            }
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
            {
                throw new InvalidOperationException("EmailSettings:Host is required.");
            }

            if (_settings.Port == 0)
            {
                throw new InvalidOperationException("EmailSettings:Port is required.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Username))
            {
                throw new InvalidOperationException("EmailSettings:Username is required.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException("EmailSettings:Password is required.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Mailbox))
            {
                throw new InvalidOperationException("EmailSettings:Mailbox is required.");
            }
        }
    }
}
