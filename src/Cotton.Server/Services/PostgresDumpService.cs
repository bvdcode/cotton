using System.Diagnostics;
using System.Globalization;
using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    public class PostgresDumpService(
        IConfiguration configuration,
        ILogger<PostgresDumpService> logger) : IPostgresDumpService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<PostgresDumpService> _logger = logger;

        public async Task DumpToFileAsync(string outputFilePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);

            string? outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Output directory is not specified in output file path.");
            }
            Directory.CreateDirectory(outputDirectory);

            DbSettings settings = ReadDbSettings();
            var processStartInfo = CreateProcessStartInfo(settings, outputFilePath);

            using var process = new Process { StartInfo = processStartInfo };
            _logger.LogInformation("Creating PostgreSQL dump to {OutputFilePath}.", outputFilePath);

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start pg_dump process.");
            }

            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }

            string stderr = await stderrTask;
            string stdout = await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"pg_dump failed with exit code {process.ExitCode}. stderr: {stderr}. stdout: {stdout}");
            }

            _logger.LogInformation("PostgreSQL dump created at {OutputFilePath}.", outputFilePath);
        }

        public async Task RestoreFromFileAsync(string inputFilePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);

            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException("PostgreSQL dump file not found.", inputFilePath);
            }

            DbSettings settings = ReadDbSettings();
            var processStartInfo = CreateRestoreProcessStartInfo(settings, inputFilePath);

            using var process = new Process { StartInfo = processStartInfo };
            _logger.LogInformation("Restoring PostgreSQL database from {InputFilePath}.", inputFilePath);

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start pg_restore process.");
            }

            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }

            string stderr = await stderrTask;
            string stdout = await stdoutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"pg_restore failed with exit code {process.ExitCode}. stderr: {stderr}. stdout: {stdout}");
            }

            _logger.LogInformation("PostgreSQL database restored successfully from {InputFilePath}.", inputFilePath);
        }

        private static ProcessStartInfo CreateProcessStartInfo(DbSettings settings, string outputFilePath)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "pg_dump",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            processStartInfo.ArgumentList.Add("--format=custom");
            processStartInfo.ArgumentList.Add("--no-password");
            processStartInfo.ArgumentList.Add("--host");
            processStartInfo.ArgumentList.Add(settings.Host);
            processStartInfo.ArgumentList.Add("--port");
            processStartInfo.ArgumentList.Add(settings.Port.ToString(CultureInfo.InvariantCulture));
            processStartInfo.ArgumentList.Add("--username");
            processStartInfo.ArgumentList.Add(settings.Username);
            processStartInfo.ArgumentList.Add("--dbname");
            processStartInfo.ArgumentList.Add(settings.Database);
            processStartInfo.ArgumentList.Add("--file");
            processStartInfo.ArgumentList.Add(outputFilePath);
            processStartInfo.Environment["PGPASSWORD"] = settings.Password;

            return processStartInfo;
        }

        private static ProcessStartInfo CreateRestoreProcessStartInfo(DbSettings settings, string inputFilePath)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "pg_restore",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            processStartInfo.ArgumentList.Add("--clean");
            processStartInfo.ArgumentList.Add("--if-exists");
            processStartInfo.ArgumentList.Add("--no-owner");
            processStartInfo.ArgumentList.Add("--no-privileges");
            processStartInfo.ArgumentList.Add("--no-password");
            processStartInfo.ArgumentList.Add("--host");
            processStartInfo.ArgumentList.Add(settings.Host);
            processStartInfo.ArgumentList.Add("--port");
            processStartInfo.ArgumentList.Add(settings.Port.ToString(CultureInfo.InvariantCulture));
            processStartInfo.ArgumentList.Add("--username");
            processStartInfo.ArgumentList.Add(settings.Username);
            processStartInfo.ArgumentList.Add("--dbname");
            processStartInfo.ArgumentList.Add(settings.Database);
            processStartInfo.ArgumentList.Add(inputFilePath);
            processStartInfo.Environment["PGPASSWORD"] = settings.Password;

            return processStartInfo;
        }

        private DbSettings ReadDbSettings()
        {
            string host = GetRequiredConfig("DatabaseSettings:Host");
            string portStr = GetRequiredConfig("DatabaseSettings:Port");
            string database = GetRequiredConfig("DatabaseSettings:Database");
            string username = GetRequiredConfig("DatabaseSettings:Username");
            string password = GetRequiredConfig("DatabaseSettings:Password");

            if (!ushort.TryParse(portStr, out ushort port))
            {
                throw new InvalidOperationException("DatabaseSettings:Port must be a valid unsigned 16-bit integer.");
            }

            return new DbSettings(host, port, database, username, password);
        }

        private string GetRequiredConfig(string key)
        {
            string? value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Required configuration key is missing: {key}");
            }
            return value;
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        private sealed record DbSettings(string Host, ushort Port, string Database, string Username, string Password);
    }
}
