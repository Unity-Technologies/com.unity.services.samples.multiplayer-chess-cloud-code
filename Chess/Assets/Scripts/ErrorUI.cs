using UnityEngine;
using UnityEngine.UIElements;

public class ErrorUI : MonoBehaviour
{
    private VisualElement root;
    private Label errorText;
    private Button okButton;

    private void Awake()
    {
        // Load the UI document  
        var uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        // Bind the UI elements  
        errorText = root.Q<Label>("ErrorText");
        okButton = root.Q<Button>("OkButton");

        // Add click event listener for the OK button  
        okButton.clicked += OkButtonClicked;
        
        Events.current.phaseChangeEvent.AddListener(OnPhaseChange);
        HideError();
    }

    public void OnPhaseChange(PhaseChangeContext phaseChangeContext)
    {
        if (phaseChangeContext.Phase == GamePhase.Error)
        {
            ShowError(phaseChangeContext.Message);
        }
        else
        {
            HideError();
        }
    }
    
    // Use this to show the error message  
    public void ShowError(string message)
    {
        Debug.Log($"Show Error: {message}");
        errorText.text = message;
        root.Q<VisualElement>("ErrorPanel").style.display = DisplayStyle.Flex;
    }

    // Use this to hide the error message  
    public void HideError()
    {
        Debug.Log("Hide Error");
        root.Q<VisualElement>("ErrorPanel").style.display = DisplayStyle.None;
    }

    // Define what happens when the OK button is clicked  
    private void OkButtonClicked()
    {
        Events.current.errorAcknowledgedEvent.Invoke();
    }
}