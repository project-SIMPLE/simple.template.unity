using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using UnityEngine.InputSystem;



public class SimulationManager : MonoBehaviour
{
    [SerializeField] private InputActionReference primaryRightHandButton;
    [SerializeField] private InputActionReference TryReconnectButton;

    [Header("Base GameObjects")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject Ground;

    // optional: define a scale between GAMA and Unity for the location given
    [Header("Coordinate conversion parameters")]
    [SerializeField] private float GamaCRSCoefX = 1.0f;
    [SerializeField] private float GamaCRSCoefY = 1.0f;
    [SerializeField] private float GamaCRSOffsetX = 0.0f;
    [SerializeField] private float GamaCRSOffsetY = 0.0f;

    // Z offset and scale
    [SerializeField] private float GamaCRSOffsetZ = 0.0f;

    private List<GameObject> toFollow;

    XRInteractionManager interactionManager;

    // ################################ EVENTS ################################
    // called when the current game state changes
    public static event Action<GameState> OnGameStateChanged;
    // called when the game is restarted
    public static event Action OnGameRestarted;
   
    // called when the world data is received
//    public static event Action<WorldJSONInfo> OnWorldDataReceived;
    // ########################################################################

    private Dictionary<string, List<object>> geometryMap;
    private Dictionary<string, PropertiesGAMA> propertyMap = null;

    private List<GameObject> SelectedObjects;


    // private bool geometriesInitialized;
    private bool handleGeometriesRequested;
//    private bool handlePlayerParametersRequested;
    private bool handleGroundParametersRequested;

    private CoordinateConverter converter;
    private PolygonGenerator polyGen;
    private ConnectionParameter parameters;
    private AllProperties propertiesGAMA;
    private WorldJSONInfo infoWorld;

    private GameState currentState;

    public static SimulationManager Instance = null;

    public float timeWithoutInteraction = 1.0f; //in second
    private float remainingTime = 0.0f;
  
    private bool isNight = false;

    private bool sendMessageToReactivatePositionSent = false;

    private float maxTimePing = 1.0f;
    private float currentTimePing = 0.0f;

    private List<GameObject> toDelete;


    // ############################################ UNITY FUNCTIONS ############################################
    void Awake() {
        Instance = this;
        SelectedObjects = new List<GameObject>();
       // toDelete = new List<GameObject>();

        toFollow = new List<GameObject>();
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
        geometryMap = new Dictionary<string, List<object>>();
        handleGeometriesRequested = false;
       // handlePlayerParametersRequested = false;
        handleGroundParametersRequested = false;
        interactionManager = player.GetComponentInChildren<XRInteractionManager>();
    }


    void FixedUpdate()
    {
        if (sendMessageToReactivatePositionSent)
        {

            Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") }};

            ConnectionManager.Instance.SendExecutableAsk("player_position_updated", args);
            sendMessageToReactivatePositionSent = false;

        }
        if (handleGroundParametersRequested)
        {
            InitGroundParameters();
            handleGroundParametersRequested = false;

        }
        if (handleGeometriesRequested && infoWorld != null && propertyMap != null)
        {
            sendMessageToReactivatePositionSent = true;
            GenerateGeometries(true);
            handleGeometriesRequested = false;
            UpdateGameState(GameState.GAME);

        }

        if (IsGameState(GameState.GAME))
        {
            UpdatePlayerPosition();
            UpdateGameToFollowPosition();
            if (infoWorld != null)
                UpdateAgentsList();
        }

    }



    private void Update()
    {
        //Debug.Log("num agents: " + geometryMap.Count);
        if (remainingTime > 0)
            remainingTime -= Time.deltaTime;
        if (currentTimePing > 0)
        {
            currentTimePing -= Time.deltaTime;
            if (currentTimePing <= 0)
            {
                Debug.Log("Try to reconnect to the server");
                ConnectionManager.Instance.Reconnect();
            }
        }
          

        if (primaryRightHandButton != null && primaryRightHandButton.action.triggered)
        {
            TriggerMainButton();
        }
        if (TryReconnectButton != null && TryReconnectButton.action.triggered)
        {
            Debug.Log("TryReconnectButton activated");
            TryReconnect();
        }

        /*int nb = toDelete.Count;
        for (int i = 0; i < nb; i++) {
            toDelete[i].transform.position = new Vector3(0, -100, 0);
            if (toFollow.Contains(toDelete[i]))
                toFollow.Remove(toDelete[i]);

            GameObject.Destroy(toDelete[i]);

        }
        toDelete.Clear();*/
    }

    void GenerateGeometries(bool initGame)
    {
       
        if (infoWorld.position != null && infoWorld.position.Count > 1 && (initGame || !sendMessageToReactivatePositionSent))
        {
            Vector3 pos = converter.fromGAMACRS(infoWorld.position[0], infoWorld.position[1], infoWorld.position[2]);
            player.transform.position = pos;

            sendMessageToReactivatePositionSent = true;
        }
        int cptPrefab = 0;
        int cptGeom = 0;
        for (int i = 0; i < infoWorld.names.Count; i++)
        {
            string name = infoWorld.names[i];
            string propId = infoWorld.propertyID[i];
         
            PropertiesGAMA prop = propertyMap[propId];
            GameObject obj = null;

            if (prop.hasPrefab)
            {
                if (initGame || !geometryMap.ContainsKey(name))
                {
                    obj = instantiatePrefab(name, prop, initGame);
                }
                else
                {
                    List<object> o = geometryMap[name];
                    PropertiesGAMA p = (PropertiesGAMA)o[1];
                    if (p == prop)
                    {
                        obj = (GameObject)o[0];
                    }
                    else
                    {
                        obj.transform.position = new Vector3(0, -100, 0);
                        if (toFollow.Contains(obj))
                            toFollow.Remove(obj);

                        GameObject.Destroy(obj);
                        obj = instantiatePrefab(name, prop, initGame);
                    }

                }
                List<int> pt = infoWorld.pointsLoc[cptPrefab].c;
                Vector3 pos = converter.fromGAMACRS(pt[0], pt[1], pt[2]);
                pos.y += pos.y + prop.yOffsetF;
                float rot = prop.rotationCoeffF * ((0.0f + pt[3]) / parameters.precision) + prop.rotationOffsetF ;
                obj.transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(rot, Vector3.up));
                obj.SetActive(true);
                cptPrefab++;

            }
            else
            {
                if (polyGen == null)
                { 
                    polyGen = PolygonGenerator.GetInstance(); 
                    polyGen.Init(converter);
                }
                List<int> pt = infoWorld.pointsGeom[cptGeom].c;
                obj = polyGen.GeneratePolygons(name, pt, prop, parameters.precision);

                if (prop.hasCollider)
                {

                    MeshCollider mc = obj.AddComponent<MeshCollider>();
                    mc.sharedMesh = polyGen.surroundMesh;
                   // mc.isTrigger = prop.isTrigger;
                }
                instantiateGO(obj, name, prop); 
               // polyGen.surroundMesh = null;
                 
                obj.SetActive(true);
                cptGeom++;

            }



        }
       if (initGame)
            AdditionalInitAfterGeomLoading();
        infoWorld = null;
    }

    void AdditionalInitAfterGeomLoading()
    {
        if (parameters.hotspots != null && parameters.hotspots.Count > 0) 
        {
            GameObject[] blocks = GameObject.FindGameObjectsWithTag("selectable");
           
            foreach (GameObject gameObj in blocks)
            {
                if (parameters.hotspots.Contains(gameObj.name))
                {
                    SelectedObjects.Add(gameObj);
                    ChangeColor(gameObj, Color.red);
                } 
            } 
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
   

    private void InitGroundParameters() {
        Debug.Log("GroundParameters : Beginnig ground initialization");
        if (Ground == null) {
            Debug.LogError("SimulationManager: Ground not set");
            return;
        }
        Vector3 ls = converter.fromGAMACRS(parameters.world[0], parameters.world[1], 0);
        if (ls.z < 0)
            ls.z = -ls.z;
        if (ls.x < 0)
            ls.x = -ls.x; 
        ls.y = Ground.transform.localScale.y;
        Ground.transform.localScale = ls;
        Vector3 ps = converter.fromGAMACRS(parameters.world[0] / 2, parameters.world[1] / 2, 0);

        Ground.transform.position = ps;
        Debug.Log("SimulationManager: Ground parameters initialized");
    }


    private void UpdateGameToFollowPosition()
    {
        if (toFollow.Count > 0)
        {


           String names = "";
           String points = "";
             string sep = ConnectionManager.Instance.MessageSeparator;
       
            foreach (GameObject obj in toFollow)
            {
                names += obj.name + sep;
                List<int> p = converter.toGAMACRS3D(obj.transform.position);

                points += p[0] + sep;

                points += p[1] + sep;
                points += p[2] + sep;

            }
            Dictionary<string, string> args = new Dictionary<string, string> {
            {"ids", names  },
            {"points", points},
            {"sep", sep}
            };

      ConnectionManager.Instance.SendExecutableAsk("move_geoms_followed", args);
        
    }
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

        List<int> p = converter.toGAMACRS3D(Camera.main.transform.position);
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") },
            {"x", "" +p[0]},
            {"y", "" +p[1]},
            {"z", "" +p[2]},
            {"angle", "" +angle}
        };

        ConnectionManager.Instance.SendExecutableAsk("move_player_external", args);
     }

   

    private void instantiateGO(GameObject obj,  String name, PropertiesGAMA prop)
    {
        obj.name = name;
        if (prop.toFollow)
        {
            toFollow.Add(obj);
        }
        if (prop.tag != null && !string.IsNullOrEmpty(prop.tag))
            obj.tag = prop.tag;
         
        if (prop.isInteractable){
        XRBaseInteractable interaction = null;
        if (prop.isGrabable)
        {
            interaction = obj.AddComponent<XRGrabInteractable>();
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (prop.constraints != null && prop.constraints.Count == 6)
            {
                    if (prop.constraints[0])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionX;
                    if (prop.constraints[1])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionY;
                    if (prop.constraints[2])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezePositionZ;
                    if (prop.constraints[3])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX;
                    if (prop.constraints[4])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationY;
                    if (prop.constraints[5])
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationZ;
                }

                
        }
        else {

            interaction = obj.AddComponent<XRSimpleInteractable>();
           
            
        }
       if(interaction.colliders.Count == 0)
        {
           Collider[] cs = obj.GetComponentsInChildren<Collider>();
           if (cs != null)
           {
               foreach (Collider c in cs)
               {
                        interaction.colliders.Add(c);
               }
           }
        }
        interaction.interactionManager = interactionManager;
        interaction.selectEntered.AddListener(SelectInteraction);
        interaction.firstHoverEntered.AddListener(HoverEnterInteraction);
        interaction.hoverExited.AddListener(HoverExitInteraction);
          
    }
}

   

    private GameObject instantiatePrefab(String name, PropertiesGAMA prop, bool initGame)
    {
        if (prop.prefabObj == null)
        {
            prop.loadPrefab(parameters.precision);
        }
        GameObject obj = Instantiate(prop.prefabObj);
        float scale = ((float)prop.size) / parameters.precision;
        obj.transform.localScale = new Vector3(scale, scale, scale);
        obj.SetActive(true);

        if (prop.hasCollider)
        {
            if (obj.TryGetComponent<LODGroup>(out var lod))
            {
                 foreach (LOD l in lod.GetLODs())
                {
                    GameObject b = l.renderers[0].gameObject;
                    BoxCollider bc = b.AddComponent<BoxCollider>();
                   // b.tag = obj.tag;
                   // b.name = obj.name;
                    //bc.isTrigger = prop.isTrigger;
                }
                    
            } else
            {
                BoxCollider bc = obj.AddComponent<BoxCollider>();
               // bc.isTrigger = prop.isTrigger;
            }
        }
        List<object> pL = new List<object>();
        pL.Add(obj); pL.Add(prop);
        if (!initGame) geometryMap.Add(name, pL);
        instantiateGO(obj, name, prop);
        return obj;
    }

   

    private void UpdateAgentsList() {
        
        foreach (List<object> obj in geometryMap.Values) {
             ((GameObject) obj[0]).SetActive(false);
        }
        GenerateGeometries(false);


        List<string> ids = new List<string>(geometryMap.Keys);
        foreach (string id in ids)
        {
            List<object> o = geometryMap[id];
            GameObject obj = (GameObject)o[0];
            if (!obj.activeSelf) {
                obj.transform.position = new Vector3(0, -100, 0);
                geometryMap.Remove(id);
                if (toFollow.Contains(obj))
                    toFollow.Remove(obj);
                GameObject.Destroy(obj);
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
        if (obj.tag.Equals("selectable") || obj.tag.Equals("car") || obj.tag.Equals("moto"))
            ChangeColor(obj, Color.blue);
    }

    public void HoverExitInteraction(HoverExitEventArgs ev)
    {
        GameObject obj = ev.interactableObject.transform.gameObject;
        if (obj.tag.Equals("selectable"))
        {
            bool isSelected = SelectedObjects.Contains(obj);

            ChangeColor(obj, isSelected ? Color.red : Color.gray);
        }
        else if (obj.tag.Equals("car") || obj.tag.Equals("moto"))
        {
            ChangeColor(obj, Color.white);
        }


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
            else if (grabbedObject.tag.Equals("car") || grabbedObject.tag.Equals("moto"))
            {
                Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", grabbedObject.name }
                    };
                ConnectionManager.Instance.SendExecutableAsk("remove_vehicle", args);
                grabbedObject.SetActive(false);
                //toDelete.Add(grabbedObject);


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
        if (content == null || content.Equals("{}")) return;
        if (firstKey == null)
        {
            if (content.Contains("pong"))
            {
                currentTimePing = 0;
                return;
            } 
            else if (content.Contains("pointsLoc"))
                firstKey = "pointsLoc"; 
            else if (content.Contains("precision"))
                firstKey = "precision";
            else if (content.Contains("properties"))
                firstKey = "properties";

        }

      //  Debug.Log("firstKey: " + firstKey);

        switch (firstKey) {
            // handle general informations about the simulation
            case "precision":

                parameters = ConnectionParameter.CreateFromJSON(content);
                converter = new CoordinateConverter(parameters.precision, GamaCRSCoefX, GamaCRSCoefY, GamaCRSCoefY, GamaCRSOffsetX, GamaCRSOffsetY, GamaCRSOffsetZ);
               
                Debug.Log("SimulationManager: Received simulation parameters");
                // Init ground and player
                // await Task.Run(() => InitGroundParameters());
                // await Task.Run(() => InitPlayerParameters()); 
               // handlePlayerParametersRequested = true;   
                handleGroundParametersRequested = true;
                handleGeometriesRequested = true;


            break;

            case "properties":
                propertiesGAMA = AllProperties.CreateFromJSON(content);
                propertyMap = new Dictionary<string, PropertiesGAMA>();
               foreach (PropertiesGAMA p in propertiesGAMA.properties)
                {
                    propertyMap.Add(p.id, p);
                }
                break;

            // handle agents while simulation is running
            case "pointsLoc":
                if (infoWorld == null) {
                    
                    infoWorld = WorldJSONInfo.CreateFromJSON(content);
                   
                    //      Debug.Log("HandleServerMessageReceived infoWorld: " + infoWorld);
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

    private void TryReconnect()
    {
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id",ConnectionManager.Instance.getUseMiddleware() ? ConnectionManager.Instance.GetConnectionId()  : ("\"" + ConnectionManager.Instance.GetConnectionId() +  "\"") }};

        ConnectionManager.Instance.SendExecutableAsk("ping_GAMA", args);

        currentTimePing = maxTimePing;
        Debug.Log("Sent Ping test");

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



public static class Extensions
{
    public static bool TryGetComponent<T>(this GameObject obj, T result) where T : Component
    {
        return (result = obj.GetComponent<T>()) != null;
    }
}