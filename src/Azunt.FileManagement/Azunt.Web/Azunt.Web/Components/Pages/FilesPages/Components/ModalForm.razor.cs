using Azunt.FileManagement;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;
using File = Azunt.FileManagement.File;

namespace Azunt.Web.Components.Pages.Files.Components;

public partial class ModalForm : ComponentBase
{
    #region Properties

    /// <summary>
    /// 모달 다이얼로그 표시 여부
    /// </summary>
    public bool IsShow { get; set; } = false;

    #endregion

    #region Public Methods

    public void Show() => IsShow = true;

    public void Hide()
    {
        IsShow = false;
        StateHasChanged();
    }

    #endregion

    #region Parameters

    [Parameter]
    public string UserName { get; set; } = "";

    [Parameter]
    public RenderFragment EditorFormTitle { get; set; } = null!;

    [Parameter]
    public File ModelSender { get; set; } = null!;

    public File ModelEdit { get; set; } = null!;

    [Parameter]
    public Action CreateCallback { get; set; } = null!;

    [Parameter]
    public EventCallback<bool> EditCallback { get; set; }

    [Parameter]
    public string ParentKey { get; set; } = "";

    #endregion

    #region Lifecycle

    protected override void OnParametersSet()
    {
        if (ModelSender != null)
        {
            ModelEdit = new File
            {
                Id = ModelSender.Id,
                Name = ModelSender.Name,
                Active = ModelSender.Active,
                Created = ModelSender.Created,
                CreatedBy = ModelSender.CreatedBy
            };
        }
        else
        {
            ModelEdit = new File();
        }
    }

    #endregion

    #region Injectors

    [Inject]
    public IFileRepository RepositoryReference { get; set; } = null!;

    #endregion

    #region Event Handlers

    protected async Task HandleValidSubmit()
    {
        ModelSender.Active = true;
        ModelSender.Name = ModelEdit.Name;
        ModelSender.CreatedBy = UserName ?? "Anonymous";

        if (ModelSender.Id == 0)
        {
            ModelSender.Created = DateTime.UtcNow;
            await RepositoryReference.AddAsync(ModelSender);
            CreateCallback?.Invoke();
        }
        else
        {
            await RepositoryReference.UpdateAsync(ModelSender);
            await EditCallback.InvokeAsync(true);
        }

        Hide();
    }

    #endregion
}