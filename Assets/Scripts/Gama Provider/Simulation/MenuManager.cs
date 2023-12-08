using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuManager : MonoBehaviour {
    
    [SerializeField] private List<GameObject> allOverlays;
    [SerializeField] private List<GameState> associatedGameStates;

    void Start() {
        if (allOverlays.Count != associatedGameStates.Count) {
            Debug.LogError("MenuManager: All Overlays and Associated Game States must have the same length");
        }
    }
    
    void OnEnable() {
        SimulationManager.OnGameStateChanged += HandleMenuOnGameStateChange;
    }

    void OnDisable() {
        SimulationManager.OnGameStateChanged -= HandleMenuOnGameStateChange;
    }

    private void HandleMenuOnGameStateChange(GameState newState) {
        for (int i = 0; i < allOverlays.Count; i++) {
            if (associatedGameStates[i] == newState) {
                allOverlays[i].SetActive(true);
            } else {
                allOverlays[i].SetActive(false);
            }
        }
    }   
    
}
