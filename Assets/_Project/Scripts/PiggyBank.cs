using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PiggyBank : MonoBehaviour
{
    [SerializeField] int maxCapacity = 10;
    [SerializeField] int currentValue;
    [SerializeField] BankAccount bankAccount;
    private void Awake()
    {
        currentValue = 0;
    }
    private void Start()
    {
        StartCoroutine(AddMoneyToPiggy());
    }
    IEnumerator AddMoneyToPiggy()
    {
        yield return new WaitForSeconds(1);
        currentValue++;
        StartCoroutine(AddMoneyToPiggy());
        if (currentValue == maxCapacity)
        {
            AddToAccount();
        }
    }
    void AddToAccount()
    {
        if(bankAccount)
        {
            bankAccount.AddMoneyToBank(currentValue);
            Debug.Log($"Money in the bank: {bankAccount.BankValue}");
            currentValue = 0;
        }
    }
}
