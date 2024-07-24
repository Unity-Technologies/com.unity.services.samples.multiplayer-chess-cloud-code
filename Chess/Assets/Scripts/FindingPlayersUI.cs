using UnityEngine;
using UnityEngine.UIElements;

public class FindingPlayersUI : MonoBehaviour
{
    private VisualElement root;
    private Label findingText;
    [SerializeField]
    private GamePhase _phase;

    private void Update()
    {
        if (_phase == GamePhase.Finding)
        {
            int seconds = Mathf.FloorToInt(Time.time) % 3;
            switch (seconds)
            {
                case 0:
                    findingText.text = "Finding Players.";
                    break;
                case 1:
                    findingText.text = "Finding Players..";
                    break;
                case 2:
                    findingText.text = "Finding Players...";
                    break;
            }
        }
    }

    private void Awake()
    {
        // Load the UI document  
        var uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        // Bind the UI elements  
        findingText = root.Q<Label>("FindingPlayersLabel");

        Events.current.phaseChangeEvent.AddListener(OnPhaseChange);
        HideElement();
    }

    public void OnPhaseChange(PhaseChangeContext phaseChangeContext)
    {
        if (phaseChangeContext.Phase == GamePhase.Finding && _phase != phaseChangeContext.Phase)
        {
            ShowElement();
        }
        else
        {
            HideElement();
        }
        _phase = phaseChangeContext.Phase;
    }

    public void ShowElement()
    {
        Debug.Log("Showing Finding Player UI");
        root.Q<VisualElement>("RootElement").style.display = DisplayStyle.Flex;
    }

    public void HideElement()
    {
        Debug.Log("Hiding Finding Player UI");
        root.Q<VisualElement>("RootElement").style.display = DisplayStyle.None;
    }
}