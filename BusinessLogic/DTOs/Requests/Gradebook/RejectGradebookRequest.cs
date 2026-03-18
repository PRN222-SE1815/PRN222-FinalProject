namespace BusinessLogic.DTOs.Requests.Gradebook;

public sealed class RejectGradebookRequest
{
    public int ClassSectionId { get; set; }

    public string ResponseMessage { get; set; } = string.Empty;
}
