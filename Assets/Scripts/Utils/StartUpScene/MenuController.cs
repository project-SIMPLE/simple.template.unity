using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuController : MonoBehaviour
{
    public void Start()
    {
        TextMeshProUGUI textMP = GameObject.FindGameObjectWithTag("textIP").GetComponent<TextMeshProUGUI>();
        string ip = PlayerPrefs.GetString("IP");
        if (ip.Length == 0)
        {
            ip = "127.0.0.1";
            PlayerPrefs.SetString("IP", ip);
        }
        textMP.text = "Current IP: " + ip;  

    }
    public void StartBtn()
    {
        SceneManager.LoadScene("Main Scene");
    }

    public void IPBtn()
    {
        SceneManager.LoadScene("IP Menu");
    }
}
