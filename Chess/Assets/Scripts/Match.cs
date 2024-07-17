using System;

public class Match
{
    public string Board { get; set; }
    public string WhitePlayerId { get; set; }
    public string BlackPlayerId { get; set; }
    public int TurnCounter { get; set; }
    public string MatchState { get; set; }
    public long MatchCreatedAt { get; set; }

    public Match(string board, string whitePlayerId, string blackPlayerId, int turnCounter, string matchState,
        long matchCreatedAt)
    {
        Board = board;
        WhitePlayerId = whitePlayerId;
        BlackPlayerId = blackPlayerId;
        TurnCounter = turnCounter;
        MatchState = matchState;
        MatchCreatedAt = matchCreatedAt;
    }

    public override string ToString()
    {
        return
            $"Board: {Board}, WhitePlayerId: {WhitePlayerId}, BlackPlayerId: {BlackPlayerId}, TurnCounter: {TurnCounter}, MatchState: {MatchState}, MatchCreatedAt: {MatchCreatedAt}";
    }
    
    public override bool Equals(object obj)  
    {  
        if (obj == null || GetType() != obj.GetType())  
        {  
            return false;  
        }  
  
        Match m = (Match)obj;  
        return (Board == m.Board) && (WhitePlayerId == m.WhitePlayerId) && (BlackPlayerId == m.BlackPlayerId) && (TurnCounter == m.TurnCounter) && (MatchState == m.MatchState) && (MatchCreatedAt == m.MatchCreatedAt);  
    }  
  
    public override int GetHashCode()  
    {  
        return Tuple.Create(Board, WhitePlayerId, BlackPlayerId, TurnCounter, MatchState, MatchCreatedAt).GetHashCode();  
    }  
}