using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GAMAGeometry 
{
  
    public List<GAMAPoint> points;
    public List<int> heights;
    public List<bool> hasColliders;
    public List<string> names;
    public List<string> tags;
    public List<bool> is3D;
    public List<bool> isInteractables;
    public List<bool> isGrabables; 
    public List<GAMAColor> colors;

    public static GAMAGeometry CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<GAMAGeometry>(jsonString);
    }
}

[System.Serializable]
public class GAMAColor
{
    public List<int> c;
}

[System.Serializable]
public class GAMAPoint
{
    public List<int> c;
}

