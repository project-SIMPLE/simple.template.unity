using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


public class PolygonGenerator
{
    CoordinateConverter converter;
    SimulationManager simuManager;
    XRInteractionManager interactionManager;

    float offsetYBackgroundGeom;

    private static PolygonGenerator instance;

    public PolygonGenerator() { }

    public void Init(CoordinateConverter c, float Yoffset, SimulationManager simulationManager, XRInteractionManager interManager)
    {
        converter = c;
        offsetYBackgroundGeom = Yoffset;
        simuManager = simulationManager;
        interactionManager = interManager;
    }

    public static PolygonGenerator GetInstance()
    {
        if (instance == null)
        {
            instance = new PolygonGenerator();
        }
        return instance;
    }

    public static void DestroyInstance()
    {
        instance = null;
    }



    public void GeneratePolygons(GAMAGeometry geom)
    {
        GameObject generated2D = new GameObject();
        generated2D.name = "Generated 2D geometries";
        GameObject generated3D = new GameObject();
        generated3D.name = "Generated 3D geometries";


        List<Vector2> pts = new List<Vector2>();
        int cpt = 0;
        for (int i = 0; i < geom.points.Count; i++)
        {
            GAMAPoint pt = geom.points[i];
            if (pt.c.Count < 2)
            {
                if (pts.Count > 2)
                {
                    Vector2[] MeshDataPoints = pts.ToArray();
                    string name = geom.names.Count > 0 ? geom.names[cpt] : "";
                    string tag = geom.tags.Count > 0 ? geom.tags[cpt] : "";
                    float extrusionHeight = geom.heights[cpt];
                    bool isUsingCollider = geom.hasColliders[cpt];
                    bool is3D = geom.is3D[cpt];
                    bool isInteractable = geom.isInteractables[cpt];
                    bool isGrabable = geom.isGrabables[cpt];
                    List<int> color = geom.colors[cpt].c;

                    Color32 col = new Color32(BitConverter.GetBytes(color[0])[0], BitConverter.GetBytes(color[1])[0],
                       BitConverter.GetBytes(color[2])[0], BitConverter.GetBytes(color[3])[0]);

                    // GameObject p = GeneratePolygon(pts.ToArray(), geom.names.Count > 0 ?  geom.names[cpt] : "", geom.tags.Count > 0 ?  geom.tags[cpt] : "", geom.heights[cpt], geom.hasColliders[cpt], geom.is3D[cpt]);
                    GameObject p = GeneratePolygon(MeshDataPoints, name, tag, extrusionHeight, isUsingCollider, is3D, col);
                    if (isInteractable == true)
                    {
                        XRBaseInteractable interaction = null;
                        if (isGrabable)
                        {
                           interaction = p.AddComponent<XRGrabInteractable>();
                        }
                        else 
                        {
                            interaction = p.AddComponent<XRSimpleInteractable>();
                        }
                        interaction.interactionManager = interactionManager;
                        if (simuManager != null)
                        {
                            interaction.selectEntered.AddListener(simuManager.SelectInteraction);

                            interaction.firstHoverEntered.AddListener(simuManager.HoverEnterInteraction);
                            interaction.hoverExited.AddListener(simuManager.HoverExitInteraction);
                        }

                    }
                    if (geom.is3D[cpt])
                    {
                        p.transform.SetParent(generated3D.transform);
                    }
                    else
                    {
                        p.transform.SetParent(generated2D.transform);
                    }
                }
                pts = new List<Vector2>();
                cpt++;

            }
            else
            {
                pts.Add(converter.fromGAMACRS2D(pt.c[0], pt.c[1]));
            }
        }
    }


    // Start is called before the first frame update
    GameObject GeneratePolygon(Vector2[] MeshDataPoints, string name, string tag, float extrusionHeight, bool isUsingCollider, bool is3D, Color32 color)
    {
        bool isUsingBottomMeshIn3D = false;
        bool isOutlineRendered = true;

        // create new GameObject (as a child)
        GameObject polyExtruderGO = new GameObject();
        if (name != "")
            polyExtruderGO.name = name;

        if (tag != null && !string.IsNullOrEmpty(tag))
        {
            polyExtruderGO.tag = tag;
            //polyExtruderGO.layer = Layer Mask.NameToLayer(tag);
        }

        // reference to setup example poly extruder 
        PolyExtruder polyExtruder;

        // add PolyExtruder script to newly created GameObject and keep track of its reference
        polyExtruder = polyExtruderGO.AddComponent<PolyExtruder>();

        // global PolyExtruder configurations
        polyExtruder.isOutlineRendered = isOutlineRendered;
        Vector3 pos = polyExtruderGO.transform.position;
        pos.y += offsetYBackgroundGeom;
        polyExtruderGO.transform.position = pos;
        polyExtruder.createPrism(polyExtruderGO.name, extrusionHeight, MeshDataPoints, color, is3D, isUsingBottomMeshIn3D, isUsingCollider);
        if (isUsingCollider)
        {
            MeshCollider mc = polyExtruderGO.AddComponent<MeshCollider>();
            mc.sharedMesh = polyExtruder.surroundMesh;
        }
        return polyExtruderGO;
    }

  
}


