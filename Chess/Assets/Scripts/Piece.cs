using UnityEngine;

public class Piece : MonoBehaviour
{
    public Vector3 InitialPos;

    private void Start()
    {
        InitialPos = gameObject.transform.position;
    }
}
