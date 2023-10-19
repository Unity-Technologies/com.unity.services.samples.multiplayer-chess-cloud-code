using TMPro;
using UnityEngine;

public class MemberView : MonoBehaviour
{
    public TextMeshProUGUI memberNameText;

    public void UpdateView(string memberName)
    {
        memberNameText.text = memberName;
    }
}
