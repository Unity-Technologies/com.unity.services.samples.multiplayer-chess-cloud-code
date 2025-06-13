using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class UiActions : MonoBehaviour,IPointerClickHandler
{
    [FormerlySerializedAs("LobbyCopiedToClipboardText")] public TextMeshProUGUI lobbyCopiedToClipboardText;
    private TextMeshProUGUI _textToCopy;
    public float feedbackDuration = 5f;
    public string feedbackMessage = "Copied to clipboard!";

    private Coroutine _feedbackCoroutine; 
    
    private void Awake()
    {
        _textToCopy = GetComponent<TextMeshProUGUI>();

        if (_textToCopy != null) return;
        Debug.LogError("CopyToClipboardOnClick: No TextMeshProUGUI component found on this GameObject. Please attach one.", this);
        enabled = false;
        
        
        if (lobbyCopiedToClipboardText != null)
        {
            lobbyCopiedToClipboardText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("CopyToClipboardOnClick: FeedbackTextElement is not assigned. No feedback will be shown.", this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_textToCopy != null && !string.IsNullOrEmpty(_textToCopy.text))
        {
            GUIUtility.systemCopyBuffer = _textToCopy.text;
            ShowFeedback();
        }
        else if (_textToCopy != null && string.IsNullOrEmpty(_textToCopy.text))
        {
            Debug.LogWarning("CopyToClipboardOnClick: Text is empty, nothing to copy.", this);
        }
    }
    
    private void ShowFeedback()
    {
        if (lobbyCopiedToClipboardText == null) return;

        if (_feedbackCoroutine != null)
        {
            StopCoroutine(_feedbackCoroutine);
        }

        _feedbackCoroutine = StartCoroutine(FeedbackCoroutine());
    }

    private IEnumerator FeedbackCoroutine()
    {
        lobbyCopiedToClipboardText.text = feedbackMessage;
        lobbyCopiedToClipboardText.gameObject.SetActive(true);

        yield return new WaitForSeconds(feedbackDuration);

        lobbyCopiedToClipboardText.gameObject.SetActive(false);
        _feedbackCoroutine = null;
    }
}
