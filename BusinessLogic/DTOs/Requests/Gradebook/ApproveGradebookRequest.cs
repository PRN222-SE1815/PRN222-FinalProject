namespace BusinessLogic.DTOs.Requests.Gradebook;

public sealed class ApproveGradebookRequest
{
    public int ClassSectionId { get; set; }

    public string? ResponseMessage { get; set; }
}
