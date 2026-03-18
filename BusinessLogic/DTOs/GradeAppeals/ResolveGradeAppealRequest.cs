namespace BusinessLogic.DTOs.GradeAppeals;

public sealed class ResolveGradeAppealRequest
{
    public long AppealId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string ResponseMessage { get; set; } = string.Empty;
    public int? GradeEntryId { get; set; }
    public decimal? NewScore { get; set; }
    public List<ResolveGradeAppealScoreChangeRequest> ScoreChanges { get; set; } = [];
}

public sealed class ResolveGradeAppealScoreChangeRequest
{
    public int GradeEntryId { get; set; }
    public decimal NewScore { get; set; }
}
