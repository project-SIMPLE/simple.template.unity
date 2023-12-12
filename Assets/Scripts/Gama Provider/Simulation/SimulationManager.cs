using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

public class SimulationManager : MonoBehaviour
{
    [Header("Base GameObjects")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject Ground;
    [SerializeField] private List<GameObject> Agents;
    [SerializeField] private TMPro.TextMeshProUGUI infoText;

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

    //Y scale for the ground
    [SerializeField] private float groundY = 1.0f;

    //Y-offset to apply to the background geometries
    [SerializeField] private float offsetYBackgroundGeom = 0.1f;

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

    // ############################################ UNITY FUNCTIONS ############################################
    void Awake() {
        Instance = this;
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
        UpdateGameState(GameState.MENU);
        InitAgentsList();
        handleGeometriesRequested = false;
        handlePlayerParametersRequested = false;
        handleGroundParametersRequested = false;
        // ConnectionManager.Instance.TryConnectionToServer();
    }

    void FixedUpdate() {
        if(IsGameState(GameState.GAME)) {
            UpdatePlayerPosition();
            UpdateAgentsList();
        }
    }

    void LateUpdate() {

        if (handleGeometriesRequested) {
            InitGeometries();
            handleGeometriesRequested = false;
        }

        if (handlePlayerParametersRequested) {
            InitPlayerParameters();
            handlePlayerParametersRequested = false;
        }

        if (handleGroundParametersRequested) {
            InitGroundParameters();
            handleGroundParametersRequested = false;
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
                ConnectionManager.Instance.SendExecutableExpression("do init_player(\"" + ConnectionManager.Instance.GetConnectionId() + "\");");
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
        player.transform.position = pos;

        if (parameters.physics) {
            if (!player.TryGetComponent(out Rigidbody rigidBody)) {
                player.AddComponent<Rigidbody>();
            }
        } else {
            if (player.TryGetComponent(out Rigidbody rigidBody)) {
                Destroy(rigidBody);
            }
        }
        Debug.Log("SimulationManager: Player parameters initialized");
    }


    private void InitGroundParameters() {
        Debug.Log("GroundParameters : Beginnig ground initialization");
        if (Ground == null) {
            Debug.LogError("SimulationManager: Ground not set");
            return;
        }
        Debug.Log("GroundParameters : after first if statement");
        Vector3 ls = converter.fromGAMACRS(parameters.world[0], parameters.world[1]);
        Debug.Log("GroundParameters : intialized ls vector");
        if (ls.z < 0)
            ls.z = -ls.z;
        if (ls.x < 0)
            ls.x = -ls.x;
        ls.y = groundY;
        Debug.Log("GroundParameters : after next if statements");
        Ground.transform.localScale = ls;
        Debug.Log("GroundParameters : after local scale transform");
        Vector3 ps = converter.fromGAMACRS(parameters.world[0] / 2, parameters.world[1] / 2);
        ps.y = -groundY;

        Ground.transform.position = ps;
        Debug.Log("SimulationManager: Ground parameters initialized");
    }

    private void InitGeometries() {
        if (polyGen == null) {
            polyGen = PolygonGenerator.GetInstance();
            polyGen.Init(converter, offsetYBackgroundGeom);
        }
        polyGen.GeneratePolygons(gamaGeometry);
        OnGeometriesInitialized?.Invoke(gamaGeometry);
        UpdateGameState(GameState.GAME);
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
        ConnectionManager.Instance.SendExecutableExpression("do move_player_external($id," + p[0] + "," + p[1] + "," + angle + ");");
    }

    private void UpdateAgentsList() {

        foreach (Dictionary<int, GameObject> agentMap in agentMapList) {
            foreach (GameObject obj in agentMap.Values) {
                obj.SetActive(false);
            }
        }

        foreach (AgentInfo pi in infoWorld.agents) {
            int speciesIndex = pi.v[0];
            // Debug.Log("Species index: " + speciesIndex);
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
    }

    // ############################################# HANDLERS ########################################
    private void HandleConnectionStateChanged(ConnectionState state) {
        // player has been added to the simulation by the middleware
        if (state == ConnectionState.AUTHENTICATED) {
            Debug.Log("SimulationManager: Player added to simulation, waiting for initial parameters");
            UpdateGameState(GameState.LOADING_DATA);
        }
    }

    private async void HandleServerMessageReceived(JObject jsonObj) {
        string firstKey = jsonObj.Properties().Select(p => p.Name).FirstOrDefault();
        switch (firstKey) {
            // handle general informations about the simulation
            case "precision":

                parameters = ConnectionParameter.CreateFromJSON(jsonObj.ToString());
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
                gamaGeometry = GAMAGeometry.CreateFromJSON(jsonObj.ToString());
                Debug.Log("SimulationManager: Received geometries data");
                handleGeometriesRequested = true;
            break;

            // handle agents while simulation is running
            case "agents":
                infoWorld = WorldJSONInfo.CreateFromJSON(jsonObj.ToString());                
                OnWorldDataReceived?.Invoke(infoWorld);
            break;

            default:
                Debug.LogError("SimulationManager: Received unknown message from middleware");
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
            DisplayInfoText("Unable to connect to middleware", Color.red);
        }
    }

    // ############################################# UTILITY FUNCTIONS ########################################
    public void DisplayInfoText(string text, Color color) {
        infoText.text = text;
        infoText.color = color;
    }

    public void RemoveInfoText() {
        DisplayInfoText("", new Color(0,0,0,0));
    }    

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