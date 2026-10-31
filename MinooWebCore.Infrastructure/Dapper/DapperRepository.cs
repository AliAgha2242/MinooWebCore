using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MinooWebCore.Infrastructure.Dapper;

/// <summary>
/// Generic Dapper repository. A new SQL connection is created and disposed per operation.
/// </summary>
public sealed class DapperRepository : IDapperRepository
{
    private readonly string _connectionString;

    public DapperRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not configured.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandType: commandType,
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<T>(command);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandType: commandType,
            cancellationToken: cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<T>(command);
    }

    public async Task<T> QuerySingleAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandType: commandType,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<T>(command);
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandType: commandType,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandType: commandType,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<T>(command);
    }
}
