using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;

public class PhaseChangeContext
{
    public GamePhase Phase { get; }
    public string Message { get; }

    public PhaseChangeContext(GamePhase gamePhase, string message)
    {
        Phase = gamePhase;
        Message = message;
    }
    
    public PhaseChangeContext(GamePhase gamePhase)
    {
        Phase = gamePhase;
        Message = "";
    }
}

[System.Serializable]  
public class PhaseChangedEvent : UnityEvent<PhaseChangeContext> {}

[System.Serializable]  
public class ErrorAcknowledgedEvent : UnityEvent {}  

public class Events : MonoBehaviour
{
    public static Events current;  
  
    private void Awake()   
    {  
        current = this;  
    }  
  
    // Event fired when an error occurs  
    public UnityEvent<PhaseChangeContext> phaseChangeEvent = new UnityEvent<PhaseChangeContext>();  
  
    // Event fired when an error is acknowledged  
    public UnityEvent errorAcknowledgedEvent = new UnityEvent();  
}
