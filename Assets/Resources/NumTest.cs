using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NumTest : MonoBehaviour
{
    ExpantaNum expantaNum = 1;
    void Update()
    {
        expantaNum = 2;
        expantaNum = expantaNum.Pow(3);
        Debug.Log(expantaNum.ToGameString());
    }
}
