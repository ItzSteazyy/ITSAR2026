using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class VariablesTest : MonoBehaviour
{
    [SerializeField] public int shopIndex = 0;
    public int ShopIndex => shopIndex;
    [SerializeField] public float shopRevenues = 50.5f;
    [SerializeField] public bool shopOpen = false;
    public enum ShopState { OPENING, SELLING, RESTOCKING, CLOSING }
    [SerializeField] ShopState shopStateBase = ShopState.OPENING;
    [SerializeField] ShopState shopStateAdvanced = ShopState.CLOSING;
    [SerializeField] public string shopName = "Shop";
    [SerializeField] public int[] productArray = new int[5];
    [SerializeField] List<int> productList = new List<int>();
    Dictionary<string, float> productDictionary = new Dictionary<string, float>();
    HashSet<Product> productHashSet = new HashSet<Product>();
    [SerializeField] List<Product> productsList = new List<Product>();

}
[Serializable]
public class Product
{
    public string Name;
    public float Price;
}
