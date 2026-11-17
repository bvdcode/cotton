// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using System.Diagnostics;
using System.Globalization;

namespace Cotton.Server.Services
{
    public class PostgresDumpService(
        IConfiguration configuration,
        ILogger<PostgresDumpService> logger) : IPostgresDumpService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<PostgresDumpService> _logger = logger;

        public async Task<TResult> ReadDumpAsync<TResult>(
            Func<Stream, CancellationToken, Task<TResult>> readDump,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(readDump);
            DbSettings settings = ReadDbSettings();
            _logger.LogInformation("Streaming PostgreSQL dump.");
            TResult result = await ExecuteAsync(CreateProcessStartInfo(settings), readDump, cancellationToken);
            _logger.LogInformation("PostgreSQL dump stream completed.");
            return result;
        }

        public async Task<PostgresRestoreResult> RestoreFromFileAsync(string inputFilePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);

            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException("PostgreSQL dump file not found.", inputFilePath);
            }

            DbSettings settings = ReadDbSettings();
            ProcessStartInfo listStartInfo = new()
            {
                FileName = "pg_restore",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            listStartInfo.ArgumentList.Add("--list");
            listStartInfo.ArgumentList.Add(inputFilePath);
            PostgresRestoreList restoreList = PostgresRestoreList.Parse(await ExecuteAsync(listStartInfo, cancellationToken));
            string listPath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(listPath, restoreList.Content, cancellationToken);
                ProcessStartInfo processStartInfo = CreateRestoreProcessStartInfo(settings, inputFilePath);
                processStartInfo.ArgumentList.Add("--use-list");
                processStartInfo.ArgumentList.Add(listPath);
                _logger.LogInformation("Restoring PostgreSQL database from {InputFilePath}.", inputFilePath);
                await ExecuteAsync(processStartInfo, cancellationToken);
                _logger.LogInformation("PostgreSQL database restored successfully from {InputFilePath}.", inputFilePath);
                return restoreList.Result;
            }
            finally
            {
                try
                {
                    File.Delete(listPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to delete the temporary database restore list at {ListPath}.", listPath);
                }
            }
        }

        private Task<string> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken) =>
            ExecuteAsync(processStartInfo, async (stream, token) =>
            {
                using StreamReader reader = new(stream, leaveOpen: true);
                return await reader.ReadToEndAsync(token);
            }, cancellationToken);

        private async Task<TResult> ExecuteAsync<TResult>(
            ProcessStartInfo processStartInfo,
            Func<Stream, CancellationToken, Task<TResult>> readOutput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Process process = new Process { StartInfo = processStartInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start {processStartInfo.FileName} process.");
            }

            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            try
            {
                TResult result = await readOutput(process.StandardOutput.BaseStream, cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"{processStartInfo.FileName} failed with exit code {process.ExitCode}. stderr: {stderr}");
                }
                return result;
            }
            finally
            {
                if (!process.HasExited)
                {
                    TryKillProcess(process);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                await stderrTask;
            }
        }

        private static ProcessStartInfo CreateProcessStartInfo(DbSettings settings)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "pg_dump",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            processStartInfo.ArgumentList.Add("--format=custom");
            processStartInfo.ArgumentList.Add("--compress=none");
            processStartInfo.ArgumentList.Add(
                $"--exclude-table-data={VectorIndexDefinition.Expected.SchemaName}.{VectorIndexDefinition.Expected.TableName}");
            processStartInfo.ArgumentList.Add("--no-password");
            processStartInfo.ArgumentList.Add("--host");
            processStartInfo.ArgumentList.Add(settings.Host);
            processStartInfo.ArgumentList.Add("--port");
            processStartInfo.ArgumentList.Add(settings.Port.ToString(CultureInfo.InvariantCulture));
            processStartInfo.ArgumentList.Add("--username");
            processStartInfo.ArgumentList.Add(settings.Username);
            processStartInfo.ArgumentList.Add("--dbname");
            processStartInfo.ArgumentList.Add(settings.Database);
            processStartInfo.Environment["PGPASSWORD"] = settings.Password;

            return processStartInfo;
        }

        private static ProcessStartInfo CreateRestoreProcessStartInfo(DbSettings settings, string inputFilePath)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "pg_restore",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            processStartInfo.ArgumentList.Add("--clean");
            processStartInfo.ArgumentList.Add("--single-transaction");
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

        private void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to kill database process; it may have already exited.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill database process.");
            }
        }

        private record DbSettings(string Host, ushort Port, string Database, string Username, string Password);
    }
}
