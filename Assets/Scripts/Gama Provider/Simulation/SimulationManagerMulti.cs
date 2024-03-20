using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using UnityEngine.InputSystem;



public class SimulationManagerMulti : SimulationManager
{
    private int score = 0 ;
    private int ranking = 1;


    protected override void TriggerMainButton()
    {
 
    }

    protected override void HoverEnterInteraction(HoverEnterEventArgs ev)
    {

        GameObject obj = ev.interactableObject.transform.gameObject;
        ChangeColor(obj, Color.blue);
        Debug.Log("HoverEnterInteraction : " + obj);
    }

    protected override void HoverExitInteraction(HoverExitEventArgs ev)
    {
        GameObject obj = ev.interactableObject.transform.gameObject;
        ChangeColor(obj, Color.white);
        Debug.Log("HoverExitInteraction : " + obj);

    }

    protected override void SelectInteraction(SelectEnterEventArgs ev)
    {

        if (remainingTime <= 0.0)
        {
            GameObject grabbedObject = ev.interactableObject.transform.gameObject;

            Debug.Log("SelectInteraction : " + grabbedObject);
            Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", grabbedObject.name }
                    };
                ConnectionManager.Instance.SendExecutableAsk("remove_token", args);
                grabbedObject.SetActive(false);
                score = score + 1;

               // toDelete.Add(grabbedObject);
        }
        
    }
}