-- [0][0] 파일업로드: Files 
CREATE TABLE [dbo].[Files]
(
    [Id]           BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,               -- 고유 ID
    [Active]       BIT NOT NULL DEFAULT(1),                                 -- 활성 상태
    [IsDeleted]    BIT NOT NULL DEFAULT(0),                                 -- 소프트 삭제
    [Created]      DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(), -- 생성 일시
    [CreatedBy]    NVARCHAR(255) NULL,                                      -- 생성자
    [Name]         NVARCHAR(100) NULL,                                      -- 파일업로드 이름 (100자로 제한)
    [DisplayOrder] INT NOT NULL DEFAULT(0),                                 -- 정렬 순서
    [FileName]     NVARCHAR(255) NULL,                                      -- 실제 저장된 파일명
    [FileSize]     INT NULL,                                                -- 파일 크기 (바이트)
    [DownCount]    INT NULL,                                                -- 다운로드 횟수
    [ParentId]     BIGINT NULL,                                             -- 연관 부모 ID (예: AppId)
    [ParentKey]    NVARCHAR(255) NULL                                       -- 연관 부모 키
);
