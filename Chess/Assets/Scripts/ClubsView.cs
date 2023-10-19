using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ClubsView : MonoBehaviour
{
    public GameObject clubsContainer;
    public ClubView clubViewPrefab;

    private readonly LinkedList<ClubView> _clubViews = new();

    public void UpdateClubsView(List<Clubs.ChessClub> clubs, List<Guid> myClubs, Func<Guid, bool, Task> callback)
    {
        while (_clubViews.Count > 0)
        {
            var removedClubView = _clubViews.Last;
            _clubViews.RemoveLast();
            Destroy(removedClubView.Value.gameObject);
        }

        foreach (var club in clubs)
        {
            var inClub = myClubs.Contains(club.ID);
            var clubView = Instantiate(clubViewPrefab, clubsContainer.transform);
            clubView.UpdateView(club.ID, club.Name, club.Country, club.MemberCount, inClub, callback);
            _clubViews.AddLast(clubView);
        }
    }
}
