using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;



public class MoveHorizontal : InputData
{
    
    [SerializeField] private float speed = 2.0f;
    [SerializeField] private float speedRotation = 10.0f;
    [SerializeField] private bool Strafe = false;
    [SerializeField] private InputActionReference stick;

    // ############################################################
   
    private void FixedUpdate()
    {
        if (SimulationManager.Instance.IsGameState(GameState.GAME)) MoveHorizontally();
    }

    // ############################################################

    private void MoveHorizontally() {
        Vector2 val = stick.action.ReadValue<Vector2>();
        Vector3 vectF = Camera.main.transform.forward;
        vectF.y = 0;
        vectF = Vector3.Normalize(vectF);

        transform.position += (vectF * speed * Time.fixedDeltaTime * val.y);

        if (Strafe)
        {
            Vector3 vectR = Camera.main.transform.right;
            vectR.y = 0;
            vectR = Vector3.Normalize(vectR);

            transform.position += (vectR * speed * Time.fixedDeltaTime * val.x);
        } else
        {

           transform.Rotate(new Vector3(0,1,0), Time.fixedDeltaTime * speedRotation * val.x);
        }
    }
}
