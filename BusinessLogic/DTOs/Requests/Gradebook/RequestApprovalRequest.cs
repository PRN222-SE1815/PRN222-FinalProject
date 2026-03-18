namespace BusinessLogic.DTOs.Requests.Gradebook;

public sealed class RequestApprovalRequest
{
    public int ClassSectionId { get; set; }

    public string? RequestMessage { get; set; }
}
