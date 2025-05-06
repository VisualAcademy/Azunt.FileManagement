using Azunt.Repositories;

namespace Azunt.FileManagement;

/// <summary>
/// 기본 CRUD 작업을 위한 FileEntity 전용 저장소 인터페이스
/// </summary>
public interface IFileBaseRepository : IRepositoryBase<FileEntity, long>
{
}