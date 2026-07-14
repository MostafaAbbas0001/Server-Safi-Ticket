namespace Safi_Ticket.Configuration
{
    public static class EnvironmentConfiguration
    {
        public static void Load()
        {
            LoadEnvFile();
            ApplyEnvironmentConfigurationAliases();
        }

        private static void LoadEnvFile()
        {
            foreach (var envPath in GetEnvFileCandidates())
            {
                if (!File.Exists(envPath))
                {
                    continue;
                }

                foreach (var rawLine in File.ReadAllLines(envPath))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();

                    if (
                        value.Length >= 2
                        && (
                            (value[0] == '"' && value[^1] == '"')
                            || (value[0] == '\'' && value[^1] == '\'')
                        )
                    )
                    {
                        value = value[1..^1];
                    }

                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }

                return;
            }
        }

        private static string[] GetEnvFileCandidates()
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            return new[]
            {
                Path.Combine(currentDirectory, ".env"),
                Path.Combine(currentDirectory, "Server", ".env"),
                Path.Combine(AppContext.BaseDirectory, ".env"),
            };
        }

        private static void ApplyEnvironmentConfigurationAliases()
        {
            SetFromEnvironmentIfMissing("Jwt__Key", "JWT_KEY");
            SetFromEnvironmentIfMissing("Jwt__Issuer", "JWT_ISSUER");
            SetFromEnvironmentIfMissing("Jwt__Audience", "JWT_AUDIENCE");
            SetFromEnvironmentIfMissing("Jwt__ExpireMinutes", "JWT_EXPIRE_MINUTES");

            SetFromEnvironmentIfMissing("EmailSettings__Host", "EMAIL_HOST");
            SetFromEnvironmentIfMissing("EmailSettings__Port", "EMAIL_PORT");
            SetFromEnvironmentIfMissing("EmailSettings__Username", "EMAIL_USERNAME");
            SetFromEnvironmentIfMissing("EmailSettings__Password", "EMAIL_PASSWORD");
            SetFromEnvironmentIfMissing("EmailSettings__Mailbox", "EMAIL_MAILBOX");
            SetFromEnvironmentIfMissing("EmailSettings__SmtpHost", "EMAIL_SMTP_HOST");
            SetFromEnvironmentIfMissing("EmailSettings__SmtpPort", "EMAIL_SMTP_PORT");
            SetFromEnvironmentIfMissing("EmailSettings__FromName", "EMAIL_FROM_NAME");

            SetFromEnvironmentIfMissing("Frontend__BaseUrl", "FRONTEND_BASE_URL");
            SetFromEnvironmentIfMissing("Frontend__AllowedOrigins", "FRONTEND_ALLOWED_ORIGINS");
            SetFromEnvironmentIfMissing("AllowedHosts", "ALLOWED_HOSTS");
            SetPostgresConnectionStringIfMissing();
        }

        private static void SetPostgresConnectionStringIfMissing()
        {
            if (
                !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                )
            )
            {
                return;
            }

            var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            if (string.IsNullOrWhiteSpace(postgresPassword))
            {
                return;
            }

            var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "SafiTicket";
            var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "safi";

            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                $"Host={postgresHost};Port={postgresPort};Database={postgresDb};Username={postgresUser};Password={postgresPassword}"
            );
        }

        private static void SetFromEnvironmentIfMissing(
            string configurationKey,
            string environmentKey
        )
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(configurationKey)))
            {
                return;
            }

            var value = Environment.GetEnvironmentVariable(environmentKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                Environment.SetEnvironmentVariable(configurationKey, value);
            }
        }
    }
}
