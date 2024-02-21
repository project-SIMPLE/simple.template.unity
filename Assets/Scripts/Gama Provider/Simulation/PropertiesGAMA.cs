﻿using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class AllProperties
{
   public List<PropertiesGAMA> properties;

    public static AllProperties CreateFromJSON(string jsonString)
    {
       return JsonUtility.FromJson<AllProperties>(jsonString);
    }
}

[System.Serializable]
public class PropertiesGAMA
{
    public string id;
    public bool hasCollider;
    public string tag;
  
    public bool isInteractable;
    public bool isGrabable;

    public bool hasPrefab;
    public string prefab;
    public int size;
    public int yOffset;
    public int rotationCoeff;
    public int rotationOffset;


    public float yOffsetF;
    public float rotationCoeffF;
    public float rotationOffsetF;

    public int height;
    public bool is3D;
    // public List<int> color;

    public int red;
    public int green;
    public int blue;
    public int alpha;

    public GameObject prefabObj = null;


    public void loadPrefab(int precision)
    {
        if (prefab != null && !prefab.Equals(""))
        {
            prefabObj = Resources.Load(prefab) as GameObject;
            yOffsetF = (0.0f + yOffset)/precision  ;
            rotationCoeffF = (0.0f + rotationCoeff) / precision;
            rotationOffsetF = (0.0f + rotationOffset) / precision;

        }
    }

    

}
