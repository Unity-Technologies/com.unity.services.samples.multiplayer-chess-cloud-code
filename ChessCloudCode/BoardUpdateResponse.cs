namespace ChessCloudCode;

public class BoardUpdateResponse
{
    public string Board { get; set; }
    public bool GameOver { get; set; }
    public string EndgameType { get; set; }
}