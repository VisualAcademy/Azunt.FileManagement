using Dul.Articles;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Azunt.FileManagement;

public class FileRepositoryAdoNet : IFileRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FileRepositoryAdoNet> _logger;

    public FileRepositoryAdoNet(string connectionString, ILoggerFactory loggerFactory)
    {
        _connectionString = connectionString;
        _logger = loggerFactory.CreateLogger<FileRepositoryAdoNet>();
    }

    private SqlConnection GetConnection() => new(_connectionString);

    public async Task<FileEntity> AddAsync(FileEntity model)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Files (Active, Created, CreatedBy, Name, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@Active, @Created, @CreatedBy, @Name, 0)";
        cmd.Parameters.AddWithValue("@Active", model.Active ?? true);
        cmd.Parameters.AddWithValue("@Created", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("@CreatedBy", model.CreatedBy ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Name", model.Name ?? (object)DBNull.Value);

        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        if (result == null)
        {
            throw new InvalidOperationException("Failed to insert FileEntity. No ID was returned.");
        }
        model.Id = (long)result;
        return model;
    }

    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        var result = new List<FileEntity>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Active, Created, CreatedBy, Name FROM Files WHERE IsDeleted = 0 ORDER BY Id DESC";

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new FileEntity
            {
                Id = reader.GetInt64(0),
                Active = reader.IsDBNull(1) ? (bool?)null : reader.GetBoolean(1),
                Created = reader.GetDateTimeOffset(2),
                CreatedBy = reader.IsDBNull(3) ? null : reader.GetString(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return result;
    }

    public async Task<FileEntity> GetByIdAsync(long id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Active, Created, CreatedBy, Name FROM Files WHERE Id = @Id AND IsDeleted = 0";
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new FileEntity
            {
                Id = reader.GetInt64(0),
                Active = reader.IsDBNull(1) ? (bool?)null : reader.GetBoolean(1),
                Created = reader.GetDateTimeOffset(2),
                CreatedBy = reader.IsDBNull(3) ? null : reader.GetString(3),
                Name = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return new FileEntity();
    }

    public async Task<bool> UpdateAsync(FileEntity model)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Files SET
                Active = @Active,
                Name = @Name
            WHERE Id = @Id AND IsDeleted = 0";
        cmd.Parameters.AddWithValue("@Active", model.Active ?? true);
        cmd.Parameters.AddWithValue("@Name", model.Name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Id", model.Id);

        await conn.OpenAsync();
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Files SET IsDeleted = 1 WHERE Id = @Id AND IsDeleted = 0";
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<Azunt.Models.Common.ArticleSet<FileEntity, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex, int pageSize, string searchField, string searchQuery, string sortOrder, TParentIdentifier parentIdentifier)
    {
        var all = await GetAllAsync();
        var filtered = string.IsNullOrWhiteSpace(searchQuery)
            ? all
            : all.Where(m => m.Name != null && m.Name.Contains(searchQuery)).ToList();

        var paged = filtered
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        return new Azunt.Models.Common.ArticleSet<FileEntity, int>(paged, filtered.Count());
    }

    public async Task<Azunt.Models.Common.ArticleSet<FileEntity, long>> GetAllAsync<TParentIdentifier>(FilterOptions<TParentIdentifier> options)
    {
        var all = await GetAllAsync();
        var filtered = all
            .Where(m => string.IsNullOrWhiteSpace(options.SearchQuery)
                     || (m.Name != null && m.Name.Contains(options.SearchQuery)))
            .ToList();

        var paged = filtered
            .Skip(options.PageIndex * options.PageSize)
            .Take(options.PageSize)
            .ToList();

        return new Azunt.Models.Common.ArticleSet<FileEntity, long>(paged, filtered.Count);
    }

    public async Task<bool> MoveUpAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "SELECT Id, DisplayOrder FROM Files WHERE Id = @Id AND IsDeleted = 0";
        cmd1.Parameters.AddWithValue("@Id", id);

        using var reader1 = await cmd1.ExecuteReaderAsync();
        if (!await reader1.ReadAsync()) return false;

        long currentId = reader1.GetInt64(0);
        int currentOrder = reader1.GetInt32(1);
        await reader1.CloseAsync();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = @"
        SELECT TOP 1 Id, DisplayOrder 
        FROM Files 
        WHERE DisplayOrder < @CurrentOrder AND IsDeleted = 0 
        ORDER BY DisplayOrder DESC";
        cmd2.Parameters.AddWithValue("@CurrentOrder", currentOrder);

        using var reader2 = await cmd2.ExecuteReaderAsync();
        if (!await reader2.ReadAsync()) return false;

        long upperId = reader2.GetInt64(0);
        int upperOrder = reader2.GetInt32(1);
        await reader2.CloseAsync();

        using var tx = conn.BeginTransaction();
        try
        {
            using var cmdUpdate1 = conn.CreateCommand();
            cmdUpdate1.Transaction = tx;
            cmdUpdate1.CommandText = "UPDATE Files SET DisplayOrder = @NewOrder WHERE Id = @Id";
            cmdUpdate1.Parameters.AddWithValue("@NewOrder", upperOrder);
            cmdUpdate1.Parameters.AddWithValue("@Id", currentId);
            await cmdUpdate1.ExecuteNonQueryAsync();

            using var cmdUpdate2 = conn.CreateCommand();
            cmdUpdate2.Transaction = tx;
            cmdUpdate2.CommandText = "UPDATE Files SET DisplayOrder = @NewOrder WHERE Id = @Id";
            cmdUpdate2.Parameters.AddWithValue("@NewOrder", currentOrder);
            cmdUpdate2.Parameters.AddWithValue("@Id", upperId);
            await cmdUpdate2.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> MoveDownAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "SELECT Id, DisplayOrder FROM Files WHERE Id = @Id AND IsDeleted = 0";
        cmd1.Parameters.AddWithValue("@Id", id);

        using var reader1 = await cmd1.ExecuteReaderAsync();
        if (!await reader1.ReadAsync()) return false;

        long currentId = reader1.GetInt64(0);
        int currentOrder = reader1.GetInt32(1);
        await reader1.CloseAsync();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = @"
        SELECT TOP 1 Id, DisplayOrder 
        FROM Files 
        WHERE DisplayOrder > @CurrentOrder AND IsDeleted = 0 
        ORDER BY DisplayOrder ASC";
        cmd2.Parameters.AddWithValue("@CurrentOrder", currentOrder);

        using var reader2 = await cmd2.ExecuteReaderAsync();
        if (!await reader2.ReadAsync()) return false;

        long lowerId = reader2.GetInt64(0);
        int lowerOrder = reader2.GetInt32(1);
        await reader2.CloseAsync();

        using var tx = conn.BeginTransaction();
        try
        {
            using var cmdUpdate1 = conn.CreateCommand();
            cmdUpdate1.Transaction = tx;
            cmdUpdate1.CommandText = "UPDATE Files SET DisplayOrder = @NewOrder WHERE Id = @Id";
            cmdUpdate1.Parameters.AddWithValue("@NewOrder", lowerOrder);
            cmdUpdate1.Parameters.AddWithValue("@Id", currentId);
            await cmdUpdate1.ExecuteNonQueryAsync();

            using var cmdUpdate2 = conn.CreateCommand();
            cmdUpdate2.Transaction = tx;
            cmdUpdate2.CommandText = "UPDATE Files SET DisplayOrder = @NewOrder WHERE Id = @Id";
            cmdUpdate2.Parameters.AddWithValue("@NewOrder", currentOrder);
            cmdUpdate2.Parameters.AddWithValue("@Id", lowerId);
            await cmdUpdate2.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }
}