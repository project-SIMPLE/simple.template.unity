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
    //private Dictionary<int, GameObject> geometriesMap3D;
   // private Dictionary<int, GameObject> geometriesMap2D;

    private static PolygonGenerator instance;

    public PolygonGenerator() {}

    public void Init(CoordinateConverter c, float Yoffset, SimulationManager simulationManager, XRInteractionManager interManager) {
        converter = c;
        offsetYBackgroundGeom = Yoffset;
        simuManager = simulationManager;
        interactionManager = interManager;
       // geometriesMap3D = new Dictionary<int, GameObject>();
       //  geometriesMap2D = new Dictionary<int, GameObject>();
    }

    public static PolygonGenerator GetInstance() {
        if (instance == null) {
            instance = new PolygonGenerator();
        }
        return instance;
    }

    public static void DestroyInstance() {
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
       // int id2D = 0;
       // int id3D = 0;
        for (int i = 0; i < geom.points.Count; i++)
        {
            GAMAPoint pt = geom.points[i];
            if (pt.c.Count < 2)
            {
                if (pts.Count > 2)
                {              
                    Vector2[] MeshDataPoints = pts.ToArray();
                    string name = geom.names.Count > 0 ?  geom.names[cpt] : "";
                    string tag = geom.tags.Count > 0 ?  geom.tags[cpt] : "";
                    float extrusionHeight = geom.heights[cpt];
                    bool isUsingCollider = geom.hasColliders[cpt];
                    bool is3D = geom.is3D[cpt];
                    bool isInteractable = geom.isInteractables[cpt];

                    // GameObject p = GeneratePolygon(pts.ToArray(), geom.names.Count > 0 ?  geom.names[cpt] : "", geom.tags.Count > 0 ?  geom.tags[cpt] : "", geom.heights[cpt], geom.hasColliders[cpt], geom.is3D[cpt]);
                    GameObject p = GeneratePolygon(MeshDataPoints, name, tag, extrusionHeight, isUsingCollider, is3D);
                    if (isInteractable == true)
                    {
                        XRSimpleInteractable interaction = p.AddComponent<XRSimpleInteractable>();

                        interaction.interactionManager = interactionManager;
                        if (simuManager != null)
                        {
                            interaction.selectEntered.AddListener(simuManager.SelectInteraction);
                           
                            interaction.firstHoverEntered.AddListener(simuManager.HoverEnterInteraction);
                            interaction.hoverExited.AddListener(simuManager.HoverExitInteraction);
                        } 
                            

                       // MeshCollider col = p.GetComponent<MeshCollider>();
                      //  col.convex = true;
                    }
                    if (geom.is3D[cpt]) {
                        p.transform.SetParent(generated3D.transform);
                   //     geometriesMap3D.Add(id3D, p);
                       // id3D++;
                    } else {
                        p.transform.SetParent(generated2D.transform);
                     //   geometriesMap2D.Add(id2D, p);
                      //  id2D++;
                    }
                }
                pts = new List<Vector2>();
                cpt++;

            } else {
                pts.Add(converter.fromGAMACRS2D(pt.c[0], pt.c[1]));
            }
        }
    }


    // Start is called before the first frame update
    GameObject GeneratePolygon(Vector2[] MeshDataPoints, string name, string tag, float extrusionHeight, bool isUsingCollider, bool is3D) {
        bool isUsingBottomMeshIn3D = false;
        bool isOutlineRendered = true;

        // create new GameObject (as a child)
        GameObject polyExtruderGO = new GameObject();
        if (name != "")
            polyExtruderGO.name = name;
        
        if (tag != null && !string.IsNullOrEmpty( tag )) {
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
        polyExtruder.createPrism(polyExtruderGO.name, extrusionHeight, MeshDataPoints, Color.grey, is3D, isUsingBottomMeshIn3D, isUsingCollider);
        if (isUsingCollider)
         {
             MeshCollider mc = polyExtruderGO.AddComponent<MeshCollider>();
             mc.sharedMesh = polyExtruder.surroundMesh;
         }
        return polyExtruderGO;
    }

   /* public Dictionary<int, GameObject> GetGeneratedGeometries3D()
    {
        return geometriesMap3D;
    }

    public Dictionary<int, GameObject> GetGeneratedGeometries2D()
    {
        return geometriesMap2D;
    }*/

    // public static void SetGeneratedBuildings(Dictionary<int, GameObject> buildings)
    // {
    //     buildingsMap = buildings;
    // }
}


