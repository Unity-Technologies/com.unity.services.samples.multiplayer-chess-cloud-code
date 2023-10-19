using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClubView : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI countryText;
    public TextMeshProUGUI memberCountText;
    public Guid ID;
    
    public Button clubButton;

    public void UpdateView(Guid id, string clubName, string country, int memberCount, bool inClub, Func<Guid, bool, Task> callback)
    {
        ID = id;
        nameText.text = clubName;
        countryText.text = country;
        memberCountText.text = $"{memberCount}";
        var clubButtonColor = clubButton.colors;
        
        if (inClub)
        {
            clubButtonColor.normalColor = new Color(165f/255, 229f/255, 165f/255);
            clubButtonColor.highlightedColor = new Color(133f/255, 229f/255, 133f/255);
            clubButtonColor.selectedColor = new Color(133f/255, 229f/255, 133f/255);
            clubButtonColor.pressedColor = new Color(83f/255, 229f/255, 83f/255);
            clubButton.colors = clubButtonColor;
        }
        else
        {
            clubButtonColor.normalColor = new Color(180f/255, 214f/255, 1);
            clubButtonColor.highlightedColor = new Color(141f/255, 193f/255, 1);
            clubButtonColor.selectedColor = new Color(141f/255, 193f/255, 1);
            clubButtonColor.pressedColor = new Color(117f/255, 180f/255, 1);
            clubButton.colors = clubButtonColor;
        }
        
        clubButton.onClick.AddListener(async () => await callback.Invoke(ID, inClub));
    }
}
