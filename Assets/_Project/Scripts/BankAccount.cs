using UnityEngine;

public class BankAccount : MonoBehaviour
{
    [SerializeField] int containedValue;
    public int BankValue => containedValue;
    public void AddMoneyToBank(int money)
    {
        containedValue += money;
    }
}
