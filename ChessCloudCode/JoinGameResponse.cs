namespace ChessCloudCode;

public class JoinGameResponse
{
    public string Session { get; set; }
    public string Board { get; set; }
    public string OpponentId { get; set; }
    public bool IsWhite { get; set; }
}