using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;


public abstract class WebSocketConnector : MonoBehaviour
{

    [SerializeField] private string host = "localhost";
    [SerializeField] private int port = 8000;
    
    private WebSocket socket;

    void OnEnable() {
        host = ValidIp(PlayerPrefs.GetString("IP")) ? PlayerPrefs.GetString("IP") : host;
        socket = new WebSocket("ws://" + host + ":" + port + "/");
        socket.OnOpen += HandleConnectionOpen;
        socket.OnMessage += HandleReceivedMessage;
        socket.OnClose += HandleConnectionClosed;
    }
    
    void Update() {
        if (socket == null) {
            Debug.Log("Socket is null");
            return;
        }
    }

    void OnDestroy() {
        socket.Close();
    }

    // ############################## HANDLERS ##############################

    protected abstract void HandleConnectionOpen(object sender, System.EventArgs e);

    protected abstract void HandleReceivedMessage(object sender, MessageEventArgs e);

    protected abstract void HandleConnectionClosed(object sender, CloseEventArgs e);

    // #######################################################################

    protected void SendMessageToServer(string message, Action<bool> successCallback) {
        socket.SendAsync(message, successCallback);
    }

    protected WebSocket GetSocket() {
        return socket;
    }

    private bool ValidIp(string ip) {
        if (ip == null || ip.Length == 0) return false;
        string[] ipb = ip.Split(".");
        return (ipb.Length != 4);
    }

    // (sender, e) =>
        // {
        //     if (e.IsText)
        //     {
        //         JObject jsonObj = JObject.Parse(e.Data);

        //         if (jsonObj["id"] != null)
        //         {
                    
        //             PlayerData tempPlayerData = JsonUtility.FromJson<PlayerData>(e.Data);
        //             playerData = tempPlayerData;
        //             Debug.Log("player ID is " + playerData.id);
        //             return;
        //         } 
        //     }

        // };

        // if (player != null && playerData.id != "")
        // {
        //     playerData.xPos = player.transform.position.x;
        //     playerData.yPos = player.transform.position.y;

        //     System.DateTime epochStart =  new System.DateTime(1970, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
        //     double timestamp = (System.DateTime.UtcNow - epochStart).TotalSeconds;
        //     //Debug.Log(timestamp);
        //     playerData.timestamp = timestamp;

        //     string playerDataJSON = JsonUtility.ToJson(playerData);
        //     socket.Send(playerDataJSON);
        // }
}
