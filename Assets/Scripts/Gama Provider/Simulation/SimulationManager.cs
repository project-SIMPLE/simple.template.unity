using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using UnityEngine.InputSystem;
 
public class SimulationManager : MonoBehaviour
{ 
    [SerializeField] private InputActionReference primaryRightHandButton;

    [Header("Base GameObjects")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject Ground;
    [SerializeField] private List<GameObject> Agents;

    // optional: rotation, Y-translation and Size scale to apply to the prefabs correspoding to the different species of agents
    [Header("Transformations applied to agents prefabs")]
    [SerializeField] private List<float> rotations = new List<float> { 90.0f, 90.0f, 0.0f };
    [SerializeField] private List<float> rotationsCoeff = new List<float> { 1, 1, 0.0f };
    [SerializeField] private List<float> YValues = new List<float> { -0.9f, -0.9f, 0.15f };
    [SerializeField] private List<float> Sizefactor = new List<float> { 0.3f, 0.3f, 1.0f };  

    // optional: define a scale between GAMA and Unity for the location given
    [Header("Coordinate conversion parameters")]
    [SerializeField] private float GamaCRSCoefX = 1.0f;
    [SerializeField] private float GamaCRSCoefY = 1.0f;
    [SerializeField] private float GamaCRSOffsetX = 0.0f;
    [SerializeField] private float GamaCRSOffsetY = 0.0f;

    // Z offset and scale
    [SerializeField] private float GamaCRSOffsetZ = 180.0f;
    // [SerializeField] private float GamaCRSCoefZ = 1.0f;

    //Y-offset to apply to the background geometries
    [SerializeField] private float offsetYBackgroundGeom = 0.0f;

    // ################################ EVENTS ################################
    // called when the current game state changes
    public static event Action<GameState> OnGameStateChanged;
    // called when the game is restarted
    public static event Action OnGameRestarted;
    // called when the geometries are initialized
    public static event Action<GAMAGeometry> OnGeometriesInitialized;
    // called when the world data is received
    public static event Action<WorldJSONInfo> OnWorldDataReceived;
    // ########################################################################

    private List<Dictionary<int, GameObject>> agentMapList;
    private List<GameObject> SelectedObjects;

    // private bool geometriesInitialized;
    private bool handleGeometriesRequested;
    private bool handlePlayerParametersRequested;
    private bool handleGroundParametersRequested;

    private CoordinateConverter converter;
    private PolygonGenerator polyGen;
    private ConnectionParameter parameters;
    private WorldJSONInfo infoWorld;
    private GAMAGeometry gamaGeometry;

    private GameState currentState;

    public static SimulationManager Instance = null;

    public float timeWithoutInteraction = 1.0f; //in second
    private float remainingTime = 0.0f;

    private bool isNight = false;

    // ############################################ UNITY FUNCTIONS ############################################
    void Awake() {
        Instance = this;
        SelectedObjects = new List<GameObject>();
    }

    void OnEnable() {
        if (ConnectionManager.Instance != null) {
            ConnectionManager.Instance.OnServerMessageReceived += HandleServerMessageReceived;
            ConnectionManager.Instance.OnConnectionAttempted += HandleConnectionAttempted;
            ConnectionManager.Instance.OnConnectionStateChanged += HandleConnectionStateChanged;
        } else {
            Debug.Log("No connection manager");
        }
    }

    void OnDisable() {
        Debug.Log("SimulationManager: OnDisable");
        ConnectionManager.Instance.OnServerMessageReceived -= HandleServerMessageReceived;
        ConnectionManager.Instance.OnConnectionAttempted -= HandleConnectionAttempted;
        ConnectionManager.Instance.OnConnectionStateChanged -= HandleConnectionStateChanged;
    }

    void OnDestroy () {
        Debug.Log("SimulationManager: OnDestroy");
    }

    void Start() {
        InitAgentsList();
        handleGeometriesRequested = false;
        handlePlayerParametersRequested = false;
        handleGroundParametersRequested = false;
    }

    private void Update()
    {
        if (remainingTime > 0)
            remainingTime -= Time.deltaTime;
        if (primaryRightHandButton != null && primaryRightHandButton.action.triggered)
        {
            TriggerMainButton();
        }
    }

    void FixedUpdate() {
        if (handlePlayerParametersRequested)
        {
            InitPlayerParameters();
            handlePlayerParametersRequested = false;
        }

        if (handleGroundParametersRequested)
        {
            InitGroundParameters();
            handleGroundParametersRequested = false;
            UpdateGameState(GameState.GAME);
        }
        if (handleGeometriesRequested)
        {
            InitGeometries();
            handleGeometriesRequested = false;

        }
        if (IsGameState(GameState.GAME)) {
            UpdatePlayerPosition();

            if (infoWorld != null)
                UpdateAgentsList();
        }
    }


    public void TriggerMainButton()
    {
        isNight = !isNight;
        Light[] lights = FindObjectsOfType(typeof(Light)) as Light[];
        foreach (Light light in lights)
        {
            light.intensity = isNight ? 0 : 1.0f;
        }
    }

    // ############################################ GAMESTATE UPDATER ############################################
    public void UpdateGameState(GameState newState) {    
        
        switch(newState) {
            case GameState.MENU:
                Debug.Log("SimulationManager: UpdateGameState -> MENU");
                break;

            case GameState.WAITING:
                Debug.Log("SimulationManager: UpdateGameState -> WAITING");
                break;

            case GameState.LOADING_DATA:
                Debug.Log("SimulationManager: UpdateGameState -> LOADING_DATA");
                if (ConnectionManager.Instance.getUseMiddleware())
                {
                    Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", ConnectionManager.Instance.GetConnectionId() }
                    };
                    ConnectionManager.Instance.SendExecutableAsk("send_init_data", args);
                }
                break;

            case GameState.GAME:
                Debug.Log("SimulationManager: UpdateGameState -> GAME");
                break;

            case GameState.END:
                Debug.Log("SimulationManager: UpdateGameState -> END");
                break;

            case GameState.CRASH:
                Debug.Log("SimulationManager: UpdateGameState -> CRASH");
                break;

            default:
                Debug.Log("SimulationManager: UpdateGameState -> UNKNOWN");
                break;
        }
        
        currentState = newState;
        OnGameStateChanged?.Invoke(currentState); 
    }

    

    // ############################# INITIALIZERS ####################################
    private void InitPlayerParameters() {
        Vector3 pos = converter.fromGAMACRS(parameters.position[0], parameters.position[1]);
        pos.y = player.transform.position.y;
        player.transform.position = pos;
        Debug.Log("SimulationManager: Player parameters initialized");
    }


    private void InitGroundParameters() {
        Debug.Log("GroundParameters : Beginnig ground initialization");
        if (Ground == null) {
            Debug.LogError("SimulationManager: Ground not set");
            return;
        }
        Vector3 ls = converter.fromGAMACRS(parameters.world[0], parameters.world[1]);
        if (ls.z < 0)
            ls.z = -ls.z;
        if (ls.x < 0)
            ls.x = -ls.x;
        Ground.transform.localScale = ls;
        Vector3 ps = converter.fromGAMACRS(parameters.world[0] / 2, parameters.world[1] / 2);

        Ground.transform.position = ps;
        Debug.Log("SimulationManager: Ground parameters initialized");
    }

    private void InitGeometries() {
        if (polyGen == null) {
            polyGen = PolygonGenerator.GetInstance();
            //Debug.Log("player.GetComponentInChildren<XRInteractionManager>(): " + player.GetComponentInChildren<XRInteractionManager>());
            polyGen.Init(converter, offsetYBackgroundGeom, this, player.GetComponentInChildren<XRInteractionManager>());
        }
        polyGen.GeneratePolygons(gamaGeometry);
        OnGeometriesInitialized?.Invoke(gamaGeometry);
       // UpdateGameState(GameState.GAME);
        Debug.Log("SimulationManager: Geometries initialized");
    }

    private void InitAgentsList() {
        agentMapList = new List<Dictionary<int, GameObject>>();
        foreach (GameObject i in Agents) {
            agentMapList.Add(new Dictionary<int, GameObject>());
        }
        Debug.Log("SimulationManager: Agents list initialized. " + Agents.Count + " species found");
    }
     

    // ############################################ UPDATERS ############################################
    private void UpdatePlayerPosition() {
        Vector2 vF = new Vector2(Camera.main.transform.forward.x, Camera.main.transform.forward.z);
        Vector2 vR = new Vector2(transform.forward.x, transform.forward.z);
        vF.Normalize();
        vR.Normalize();
        float c = vF.x * vR.x + vF.y * vR.y;
        float s = vF.x * vR.y - vF.y * vR.x;

        int angle = (int) (((s > 0) ? -1.0 : 1.0) * (180 / Math.PI) * Math.Acos(c) * parameters.precision);

        List<int> p = converter.toGAMACRS(Camera.main.transform.position);
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") },
            {"x", "" +p[0]},
            {"y", "" +p[1]},
            {"angle", "" +angle}
        };

        ConnectionManager.Instance.SendExecutableAsk("move_player_external", args);
     }

    private void UpdateAgentsList() {
        if (infoWorld.position != null && infoWorld.position.Count > 1)
        {
            Vector3 pos = converter.fromGAMACRS(infoWorld.position[0], infoWorld.position[1]);
            player.transform.position = pos;
        }
        foreach (Dictionary<int, GameObject> agentMap in agentMapList) {
            foreach (GameObject obj in agentMap.Values) {
                obj.SetActive(false);
            }
        }

        foreach (AgentInfo pi in infoWorld.agents) {
            int speciesIndex = pi.v[0];
            GameObject Agent = Agents[speciesIndex];
            int id = pi.v[1];
            GameObject obj = null;
            Dictionary<int, GameObject> agentMap = agentMapList[speciesIndex];

            if (!agentMap.ContainsKey(id)) {
                obj = Instantiate(Agent);
                float scale = Sizefactor[speciesIndex];
                obj.transform.localScale = new Vector3(scale, scale, scale);
                obj.SetActive(true);
                agentMap.Add(id, obj);
            } else {
                obj = agentMap[id];
            }


            Vector3 pos = converter.fromGAMACRS(pi.v[2], pi.v[3]);
            pos.y = YValues[speciesIndex];
            float rot = rotationsCoeff[speciesIndex] * (pi.v[4] / parameters.precision) + rotations[speciesIndex];
            obj.transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(rot, Vector3.up));
            obj.SetActive(true);
        } 
        
        foreach (Dictionary<int, GameObject> agentMap in agentMapList) {
            List<int> ids = new List<int>(agentMap.Keys);
            foreach (int id in ids) {
                GameObject obj = agentMap[id];
                if (!obj.activeSelf) {
                    obj.transform.position = new Vector3(0, -100, 0);
                    agentMap.Remove(id);
                    GameObject.Destroy(obj);
                }
            }
        }
        infoWorld = null;
    }

    // ############################################# HANDLERS ########################################
    private void HandleConnectionStateChanged(ConnectionState state) {
        // player has been added to the simulation by the middleware
        if (state == ConnectionState.AUTHENTICATED) {
            Debug.Log("SimulationManager: Player added to simulation, waiting for initial parameters");
            UpdateGameState(GameState.LOADING_DATA);
        }
    }


  

    public void HoverEnterInteraction(HoverEnterEventArgs ev)
    {
         
        GameObject obj = ev.interactableObject.transform.gameObject;

        ChangeColor(obj, Color.blue);
    }

    public void HoverExitInteraction(HoverExitEventArgs ev)
    {
        GameObject obj = ev.interactableObject.transform.gameObject;
        bool isSelected = SelectedObjects.Contains(obj); 

        ChangeColor(obj, isSelected ? Color.red : Color.gray);
    }

    public void SelectInteraction(SelectEnterEventArgs ev)
    {

        if (remainingTime <= 0.0)
        {
            GameObject grabbedObject = ev.interactableObject.transform.gameObject;

            if (("selectable").Equals(grabbedObject.tag))
            {
                Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", grabbedObject.name }
                    };
                ConnectionManager.Instance.SendExecutableAsk("update_hotspot", args);
                bool newSelection = !SelectedObjects.Contains(grabbedObject);
                if (newSelection)
                    SelectedObjects.Add(grabbedObject);
                else
                    SelectedObjects.Remove(grabbedObject);
                ChangeColor(grabbedObject, newSelection ? Color.red : Color.gray);

                remainingTime = timeWithoutInteraction;
            }
        }
        
    }
     
    static public void ChangeColor(GameObject obj, Color color) 
    {
        Renderer[] renderers = obj.gameObject.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = color;// renderers[i].material.color == Color.red ? Color.gray : Color.red;
        }
    }
    private async void HandleServerMessageReceived(String firstKey, String content) {

        if (firstKey == null)
        {
            if (content.Contains("agents"))
                firstKey = "agents"; 
            else if (content.Contains("points"))
                firstKey = "points";
            else if (content.Contains("precision"))
                firstKey = "precision";
           
        }
        
        switch (firstKey) {
            // handle general informations about the simulation
            case "precision":

                parameters = ConnectionParameter.CreateFromJSON(content);
                converter = new CoordinateConverter(parameters.precision, GamaCRSCoefX, GamaCRSCoefY, GamaCRSCoefY, GamaCRSOffsetX, GamaCRSOffsetY, GamaCRSOffsetZ);
               
                Debug.Log("SimulationManager: Received simulation parameters");
                // Init ground and player
                // await Task.Run(() => InitGroundParameters());
                // await Task.Run(() => InitPlayerParameters()); 
                handlePlayerParametersRequested = true;   
                handleGroundParametersRequested = true;
                
            break; 

            // handle geometries sent by GAMA at the beginning of the simulation
            case "points":
                gamaGeometry = GAMAGeometry.CreateFromJSON(content);
                Debug.Log("SimulationManager: Received geometries data");
                handleGeometriesRequested = true;
            break;

            // handle agents while simulation is running
            case "agents":
                if (infoWorld == null) { 
                    infoWorld = WorldJSONInfo.CreateFromJSON(content);
                }
             break;

            default:
                Debug.LogError("SimulationManager: Received unknown message: " + content);
                break;
        }

    }

    private void HandleConnectionAttempted(bool success) {
        Debug.Log("SimulationManager: Connection attempt " + (success ? "successful" : "failed"));
        if (success) {
            if(IsGameState(GameState.MENU)) {
                Debug.Log("SimulationManager: Successfully connected to middleware");
                UpdateGameState(GameState.WAITING);
            }
        } else {
            // stay in MENU state
            Debug.Log("Unable to connect to middleware");
        }
    }

    // ############################################# UTILITY FUNCTIONS ########################################
    

    public void RestartGame() {
        OnGameRestarted?.Invoke();        
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsGameState(GameState state) {
        return currentState == state;
    }


    public GameState GetCurrentState() {
        return currentState;
    }
}


// ############################################################
public enum GameState {
    // not connected to middleware
    MENU,
    // connected to middleware, waiting for authentication
    WAITING,
    // connected to middleware, authenticated, waiting for initial data from middleware
    LOADING_DATA,
    // connected to middleware, authenticated, initial data received, simulation running
    GAME,
    END, 
    CRASH
}