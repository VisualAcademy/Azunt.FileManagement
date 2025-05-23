﻿using Azunt.FileManagement;
using Azunt.Repositories;
using Dul.Articles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azunt.FileManagement;

/// <summary>
/// FileEntity 테이블에 대한 Entity Framework Core 기반 리포지토리 구현체입니다.
/// Blazor Server 회로 유지 이슈를 피하고, 멀티테넌트 연결 문자열 지원을 위해 팩터리 사용.
/// </summary>
public class FileRepository : IFileRepository
{
    private readonly FileAppDbContextFactory _factory;
    private readonly ILogger<FileRepository> _logger;
    private readonly string? _connectionString;

    public FileRepository(
        FileAppDbContextFactory factory,
        ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _logger = loggerFactory.CreateLogger<FileRepository>();
    }

    public FileRepository(
        FileAppDbContextFactory factory,
        ILoggerFactory loggerFactory,
        string connectionString)
    {
        _factory = factory;
        _logger = loggerFactory.CreateLogger<FileRepository>();
        _connectionString = connectionString;
    }

    private FileAppDbContext CreateContext() =>
        string.IsNullOrWhiteSpace(_connectionString)
            ? _factory.CreateDbContext()
            : _factory.CreateDbContext(_connectionString);

    public async Task<FileEntity> AddAsyncDefault(FileEntity model)
    {
        await using var context = CreateContext();
        model.Created = DateTime.UtcNow;
        model.IsDeleted = false;
        context.Files.Add(model);
        await context.SaveChangesAsync();
        return model;
    }

    public async Task<FileEntity> AddAsync(FileEntity model)
    {
        await using var context = CreateContext();
        model.Created = DateTime.UtcNow;
        model.IsDeleted = false;

        // 현재 가장 높은 DisplayOrder 값 조회
        var maxDisplayOrder = await context.Files
            .Where(m => !m.IsDeleted)
            .MaxAsync(m => (int?)m.DisplayOrder) ?? 0;
        model.DisplayOrder = maxDisplayOrder + 1;

        context.Files.Add(model);
        await context.SaveChangesAsync();
        return model;
    }

    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        await using var context = CreateContext();
        return await context.Files
            .Where(m => !m.IsDeleted)
            //.OrderByDescending(m => m.Id)
            .OrderBy(m => m.DisplayOrder) // 정렬 순서 변경
            .ToListAsync();
    }

    public async Task<FileEntity> GetByIdAsync(long id)
    {
        await using var context = CreateContext();
        return await context.Files
            .Where(m => m.Id == id && !m.IsDeleted)
            .SingleOrDefaultAsync()
            ?? new FileEntity();
    }

    public async Task<bool> UpdateAsync(FileEntity model)
    {
        await using var context = CreateContext();
        context.Attach(model);
        context.Entry(model).State = EntityState.Modified;
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        await using var context = CreateContext();
        var entity = await context.Files.FindAsync(id);
        if (entity == null || entity.IsDeleted) return false;

        entity.IsDeleted = true;
        context.Files.Update(entity);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<Azunt.Models.Common.ArticleSet<FileEntity, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex,
        int pageSize,
        string searchField,
        string searchQuery,
        string sortOrder,
        TParentIdentifier parentIdentifier,
        string category = "")
    {
        await using var context = CreateContext();
        var query = context.Files
            .Where(m => !m.IsDeleted)
            .AsQueryable();

        #region ParentBy: 특정 부모 키 값(int, string)에 해당하는 리스트인지 확인
        if (parentIdentifier is int parentId && parentId != 0)
        {
            query = query.Where(m => m.ParentId != null && m.ParentId == parentId);
        }
        else if (parentIdentifier is string parentKey && !string.IsNullOrEmpty(parentKey))
        {
            query = query.Where(m => m.ParentKey != null && m.ParentKey == parentKey);
        }
        #endregion

        // 검색어 조건
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(m => m.Name != null && m.Name.Contains(searchQuery));
        }

        // 카테고리 조건 추가
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        // 정렬
        query = sortOrder switch
        {
            "Name" => query.OrderBy(m => m.Name),
            "NameDesc" => query.OrderByDescending(m => m.Name),
            "Created" => query.OrderBy(m => m.Created),
            "CreatedDesc" => query.OrderByDescending(m => m.Created),
            "Title" => query.OrderBy(m => m.Title),
            "TitleDesc" => query.OrderByDescending(m => m.Title),
            "Category" => query.OrderBy(m => m.Category),
            "CategoryDesc" => query.OrderByDescending(m => m.Category),
            "DisplayOrder" => query.OrderBy(m => m.DisplayOrder),
            _ => query.OrderBy(m => m.DisplayOrder)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new Azunt.Models.Common.ArticleSet<FileEntity, int>(items, totalCount);
    }

    public async Task<bool> MoveUpAsync(long id)
    {
        await using var context = CreateContext();
        var current = await context.Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (current == null) return false;

        var upper = await context.Files
            .Where(x => x.DisplayOrder < current.DisplayOrder && !x.IsDeleted)
            .OrderByDescending(x => x.DisplayOrder)
            .FirstOrDefaultAsync();

        if (upper == null) return false;

        // Swap
        int temp = current.DisplayOrder;
        current.DisplayOrder = upper.DisplayOrder;
        upper.DisplayOrder = temp;

        // 명시적 변경 추적
        context.Files.Update(current);
        context.Files.Update(upper);

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveDownAsync(long id)
    {
        await using var context = CreateContext();
        var current = await context.Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (current == null) return false;

        var lower = await context.Files
            .Where(x => x.DisplayOrder > current.DisplayOrder && !x.IsDeleted)
            .OrderBy(x => x.DisplayOrder)
            .FirstOrDefaultAsync();

        if (lower == null) return false;

        // Swap
        int temp = current.DisplayOrder;
        current.DisplayOrder = lower.DisplayOrder;
        lower.DisplayOrder = temp;

        // 명시적 변경 추적
        context.Files.Update(current);
        context.Files.Update(lower);

        await context.SaveChangesAsync();
        return true;
    }
}