namespace DefaultNamespace
{
    public class GameState
    {
        private GamePhase _gamePhase;

        public GamePhase GamePhase => _gamePhase;

        public void SetGamePhase(GamePhase phase)
        {
            _gamePhase = phase;
            Events.current.phaseChangeEvent.Invoke(new PhaseChangeContext(phase));
        }
        
        public void SetGamePhase(GamePhase phase, string message)
        {
            _gamePhase = phase;
            Events.current.phaseChangeEvent.Invoke(new PhaseChangeContext(phase, message));
        }
    }
}