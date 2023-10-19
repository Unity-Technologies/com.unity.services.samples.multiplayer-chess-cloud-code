using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;

public class MemberManagementView : MonoBehaviour
{
    public TextMeshProUGUI memberNameText;
    public Button actionButton;
    public Button denyButton;

    private Guid _clubId;
    private string _memberId;

    public void UpdateView(Guid clubId, string memberId, string memberName, Func<Guid, string, Task> callback, Func<Guid, string, Task> denyCallback)
    {
        _memberId = memberId;
        _clubId = clubId;
        memberNameText.text = memberName;
        if (memberId == AuthenticationService.Instance.PlayerId)
        {
            memberNameText.text += " (you)";
            actionButton.gameObject.SetActive(false);
            return;
        }

        actionButton.onClick.AddListener(async () => await callback(_clubId, _memberId));
        if (denyButton != null)
        {
            denyButton.onClick.AddListener(async() => await denyCallback(_clubId, _memberId));
        }
    }
}
