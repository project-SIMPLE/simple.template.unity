
using UnityEngine;
using WebSocketSharp;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

public class GAMAGeometryLoader: ConnectionWithGama
{
   
    // optional: define a scale between GAMA and Unity for the location given
    public float offsetYBackgroundGeom = 0.0f;
   
    private PolygonGenerator polyGen;

    private GAMAGeometry geoms;

    private bool continueProcess = true;
    public float GamaCRSCoefX = 1.0f;
    public float GamaCRSCoefY = 1.0f;
    public float GamaCRSOffsetX = 0.0f;
    public float GamaCRSOffsetY = 0.0f;


    protected ConnectionParameter parameters = null;
    protected CoordinateConverter converter;


    public void GenerateGeometries(string ip_, string port_, float x, float y, float ox, float oy, float YOffset)
    {
        ip = ip_;
        port = port_;
        GamaCRSCoefX = x;
        GamaCRSCoefY = y;
        GamaCRSOffsetX = ox;
        GamaCRSOffsetY = oy;
        offsetYBackgroundGeom = YOffset;
        socket = new WebSocket("ws://" + ip + ":" + port + "/");

        socket.Connect();
        continueProcess = true;

        socket.OnMessage += HandleReceivedMessage;

        int i = 0;
        Dictionary<string, string> args = new Dictionary<string, string> {
                    {"id", "geomloader"}
                  };
        SendExecutableAsk("create_init_player", args);


        while (continueProcess) {
            //  Debug.Log("continueProcess:" + continueProcess);
            i++;
            generateGeom();
            if (i > 50000) break;
        }


    }


    private void generateGeom()
    {

        if (parameters != null && converter != null && geoms != null)
        {
       
            polyGen = new PolygonGenerator();
            polyGen.Init(converter, offsetYBackgroundGeom, null, null);
            polyGen.GeneratePolygons(geoms);
            continueProcess = false;
        }
    }

    private void HandleServerMessageReceived(String content)
    {

        string firstKey = "";
        if (content.Contains("points"))
            firstKey = "points";
        else if (content.Contains("precision"))
            firstKey = "precision";

        switch (firstKey)
        {
            // handle general informations about the simulation
            case "precision":

                parameters = ConnectionParameter.CreateFromJSON(content);
                converter = new CoordinateConverter(parameters.precision, GamaCRSCoefX, GamaCRSCoefY, GamaCRSOffsetX, GamaCRSOffsetY);
                Debug.Log("Received parameter data");
                break;

            // handle geometries sent by GAMA at the beginning of the simulation
            case "points":
                geoms = GAMAGeometry.CreateFromJSON(content);
                Debug.Log("Received geometry data");
                break;
        }

    }
    protected void HandleReceivedMessage(object sender, MessageEventArgs e)
    {

        if (e.IsText)
        {
            JObject jsonObj = JObject.Parse(e.Data);
            string type = (string)jsonObj["type"];


            if (type.Equals("SimulationOutput"))
            {
                JValue content = (JValue)jsonObj["content"];
                foreach (String mes in content.ToString().Split(MessageSeparator))
                {
                    if (!mes.IsNullOrEmpty())
                        HandleServerMessageReceived(mes);
                }
            }
        }
    }

}