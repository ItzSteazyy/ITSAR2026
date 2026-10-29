using UnityEngine;

public class VisibilityTest : MonoBehaviour
{
    [SerializeField] VariablesTest vt;

    private void Start()
    {
        if(vt != null)
        {
            Debug.Log($"{vt.ShopIndex}");
        }
    }
}
