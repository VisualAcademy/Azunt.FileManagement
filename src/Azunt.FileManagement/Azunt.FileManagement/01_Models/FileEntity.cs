using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Azunt.FileManagement
{
    /// <summary>
    /// Files 테이블과 매핑되는 파일업로드(FileEntity) 엔터티 클래스입니다.
    /// </summary>
    [Table("Files")]
    public class FileEntity
    {
        /// <summary>
        /// 파일업로드 고유 아이디 (자동 증가)
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// 활성 상태 (기본값: true)
        /// </summary>
        public bool? Active { get; set; }

        /// <summary>
        /// 소프트 삭제 플래그 (기본값: false)
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 생성 일시
        /// </summary>
        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// 생성자 이름
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// 파일업로드 이름
        /// </summary>
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters.")]
        public string? Name { get; set; }

        /// <summary>
        /// 정렬 순서
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// 실제 파일 이름
        /// </summary>
        [StringLength(255)]
        public string? FileName { get; set; }

        /// <summary>
        /// 파일 크기 (바이트)
        /// </summary>
        public int? FileSize { get; set; }

        /// <summary>
        /// 다운로드 횟수
        /// </summary>
        public int? DownCount { get; set; }

        /// <summary>
        /// 숫자 형식의 외래키? - AppId 형태로 ParentId와 ParentKey 속성은 보조로 만들어 놓은 속성
        /// </summary>
        public long? ParentId { get; set; } = default;  // long? 형식으로 변경 가능    

        /// <summary>
        /// 숫자 형식의 외래키? - AppId 형태로 ParentId와 ParentKey 속성은 보조로 만들어 놓은 속성
        /// </summary>
        public string? ParentKey { get; set; } = string.Empty;

        /// <summary>
        /// Category used to group or classify the file (optional)
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// [3] Title of the file (required)
        /// </summary>
        [MaxLength(255)]
        //[Required(ErrorMessage = "Please enter a title.")]
        [Display(Name = "Title")]
        [Column(TypeName = "NVarChar(255)")]
        public string? Title { get; set; } = string.Empty;
    }
}
