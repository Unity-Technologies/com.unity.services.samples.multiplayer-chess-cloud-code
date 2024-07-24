using System;

public class Match
{
    public string Board { get; set; }
    public string WhitePlayerId { get; set; }
    public string BlackPlayerId { get; set; }
    public int TurnCounter { get; set; }
    public string MatchState { get; set; }


    public Match(string board, string whitePlayerId, string blackPlayerId, int turnCounter, string matchState)
    {
        Board = board;
        WhitePlayerId = whitePlayerId;
        BlackPlayerId = blackPlayerId;
        TurnCounter = turnCounter;
        MatchState = matchState;
    }

    public override string ToString()
    {
        return
            $"Board: {Board}, WhitePlayerId: {WhitePlayerId}, BlackPlayerId: {BlackPlayerId}, TurnCounter: {TurnCounter}, MatchState: {MatchState}";
    }
    
    public override bool Equals(object obj)  
    {  
        if (obj == null || GetType() != obj.GetType())  
        {  
            return false;  
        }  
  
        Match m = (Match)obj;  
        return (Board == m.Board) && (WhitePlayerId == m.WhitePlayerId) && (BlackPlayerId == m.BlackPlayerId) && (TurnCounter == m.TurnCounter) && (MatchState == m.MatchState);  
    }  
  
    public override int GetHashCode()  
    {  
        return Tuple.Create(Board, WhitePlayerId, BlackPlayerId, TurnCounter, MatchState).GetHashCode();  
    }  
}