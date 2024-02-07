using System;
using UnityEngine;
using WebSocketSharp;


public abstract class WebSocketConnector : MonoBehaviour
{

     protected string host ;
     protected string port;

    protected bool UseMiddleware; 

    private WebSocket socket;

    public bool UseHeartbeat = true; //only for middleware mode

    public bool DesktopMode = false;
    public bool UseMiddlewareDM = true; //only for Desktop mode

    public int numErrorsBeforeDeconnection = 10;
    protected int numErrors = 0;

    void OnEnable() {
       
        Debug.Log("WebSocketConnector OnEnable host: " + PlayerPrefs.GetString("IP") + " PORT: " + PlayerPrefs.GetString("PORT") + " MIDDLEWARE:" + PlayerPrefs.GetString("MIDDLEWARE"));
        port = PlayerPrefs.GetString("PORT"); 
        host = PlayerPrefs.GetString("IP");

        if (DesktopMode)
        {
            UseMiddleware = UseMiddlewareDM;
            host = "10.0.128.39";// "localhost";

            if (UseMiddleware)
            {
                port = "8080";
            }
            else
            {
                port = "1000";
            }
            
        }  
      
        socket = new WebSocket("ws://" + host + ":" + port + "/");
        socket.OnOpen += HandleConnectionOpen;
        socket.OnMessage += HandleReceivedMessage;
        socket.OnClose += HandleConnectionClosed;
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
        try
        {
            socket.SendAsync(message, successCallback);
        } catch (Exception e)
        {

        }
        
    }

    protected WebSocket GetSocket() {
        return socket;
    }

    private bool ValidIp(string ip) {
        if (ip == null || ip.Length == 0) return false;
        string[] ipb = ip.Split(".");
        return (ipb.Length != 4);
    }
}
