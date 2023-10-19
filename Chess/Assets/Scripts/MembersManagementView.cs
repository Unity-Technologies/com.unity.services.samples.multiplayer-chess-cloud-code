using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

public class MembersManagementView : MonoBehaviour
{
    public GameObject membersManagementContent;
    public MemberManagementView memberManagementViewPrefab;

    private readonly LinkedList<MemberManagementView> _memberManagementViews = new();

    public void UpdateMembersView(Guid clubId, Dictionary<string, string> members, Func<Guid, string, Task> callback, Func<Guid, string, Task> denyCallback)
    {
        while (_memberManagementViews.Count > 0)
        {
            var removedClubView = _memberManagementViews.Last;
            _memberManagementViews.RemoveLast();
            Destroy(removedClubView.Value.gameObject);
        }

        foreach (var (memberId, memberName) in members)
        {
            var memberView = Instantiate(memberManagementViewPrefab, membersManagementContent.transform);
            memberView.UpdateView(clubId, memberId, memberName, callback, denyCallback);
            _memberManagementViews.AddLast(memberView);
        }
    }
}
