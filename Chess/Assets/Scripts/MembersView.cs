using System;
using System.Collections.Generic;
using UnityEngine;

public class MembersView : MonoBehaviour
{
    public GameObject membersContent;
    public MemberView memberViewPrefab;

    private readonly LinkedList<MemberView> _memberViews = new();

    public void UpdateMembersView(List<string> members)
    {
        while (_memberViews.Count > 0)
        {
            var removedClubView = _memberViews.Last;
            _memberViews.RemoveLast();
            Destroy(removedClubView.Value.gameObject);
        }

        foreach (var member in members)
        {
            var memberView = Instantiate(memberViewPrefab, membersContent.transform);
            memberView.UpdateView(member);
            _memberViews.AddLast(memberView);
        }
    }
}
