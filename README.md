# GAMA VR Provider

This package allows to adapt a GAMA simulation to a VR environment created with Unity. It provides the VR developer with a game and connection management system, including GameObjects, methods and events that can be hooked. A list of these elements and how to use them is provided in the [**Documentation**](#documentation) section.

## Installation

> [!WARNING]
> The package is being developped using **Unity Editor 2022.3.5f1**. Although it should work with newer versions, as is doesn't use any version-specific features (for now), it is strongly recommanded to use exactly the same Editor version.  

### Prerequisites

Once the project is opened in Unity, if you have any errors, you can check the following points: 
- Make sure that **Newtonsoft Json** is installed. Normaly, cloning this repo should ensure that it is installed. But if it's not the case, follow the tutorial on this [link](https://github.com/applejag/Newtonsoft.Json-for-Unity/wiki/Install-official-via-UPM).
- To work properly, we assume that you already have a compatible GAMA model and optionally that you have installed the [**Gama Server Middleware**](https://github.com/project-SIMPLE/GamaServerMiddleware) if you want to design a multi-player Game.

> [!TIP]
> **For Windows users**, make sure that the folder Assets/Plugins contains a .dll file called websocket-sharp. If not, download it from [this repo](https://github.com/sta/websocket-sharp). And place it in Assets/Plugins in your Unity project. 

### What is included 

The project contains three scenes:
 - Startup Menu: Main menu that allows to load two Scenes - IP Menu and Main Scene. It allows as well to define if the middleware will be used or not. Using the middleware requires to run another software (the middleware), but allows to connect several players et to follow the connection status of the players.
 - IP Menu: allows to change the IP used to connect to the computer running the middleware/GAMA
 - Demo/Main Scene: main scene with the required script and the following GameObjects:
	- Directional Light
	- FPS Player
	- Managers
		- Connection Manager: define the connection properties of Unity
		- Game Manager: define all the aspects of the game
  	- Telelport Area: used only for FPS player to move using teleportation
    	- Debug Overlay: display all the information written in the model (using Debug.Log("message")).  
  
### Quick Start

1. Download the silmple.template.project.
![qs1](./ReadmeRes/download.png)
1. Import it as a Unity project. **Make sure to use the right Editor version (Unity Editor 2022.3.5f1)**.
![qs1](./ReadmeRes/qs-1.png)
1. In the Menu "File" select "Build Settings..."
![qs1](./ReadmeRes/Build-setting_menu.png)
1. Select "Android" in "Platform", then click on "Switch Platform". You can after build and deploy the application on the headset by clicking on "Build and Run".
![qs1](./ReadmeRes/Build-setting.png)
1. To run the application in conjunction with GAMA, make sure you have installed [GAMA 1.9.3](https://github.com/gama-platform/gama/releases/tag/1.9.3) and the [Unity Plugin for GAMA](https://github.com/project-SIMPLE/gaml.extension.unity). Information on installing the plugin is available [here] (https://github.com/project-SIMPLE/gaml.extension.unity?tab=readme-ov-file#from-gama). The plugin provides a demo model (added in Plugin models/LinkToUnity/DemoModelVR.gaml) that works with the Unity project model, and in particular the main scene. To connect the VR headset to this model, run the vr_xp experiment before connecting the headset. 
   

## Documentation

This section focuses only on the C# scripts which are useful for a Unity developer. The scripts not mentioned here are at least commented.  
**Important note:** As all the scripts which name finishes by "Manager" are instantiated when Unity is launched in the "Managers" GameObject, they are all developed using the Singleton Pattern. Hence trying to instantiate in some external scripts could break the default mechanisms. To call a method from one of these classes, one should rather use the following code snippet :

```csharp
NameOfClassManager.Instance.SomeMethod();
```

### WebSocketConnector

Base abstract class to establish a web-socket connection with GAMA. All the methods of this class are private or protected. Hence they are only accessible through a child class (ConnectionManager here).  
Theorically, in most cases, **one mustn't try to access the methods of this class**, as they are alreay used/overriden by ConnectionManager.

**Abstract Methods:**  
- `HandleConnectionOpen` : triggered when a web-socket connection is established.
- `HandleReceivedMessage` : triggered when a message is received from the server.
- `HandleConnectionClosed` : triggered when the connection is closed, either by the server or by Unity itself.

### ConnectionManager

This class extends WebSocketConnector and implements the methods mentioned above. The corresponding script is already in a GameObject called "Connection Manager", which is already in the default scene.  
It is in charge of creating an ID for the player once the connection with GAMA is established. Moreover, it provides the Unity developer with a state machine implemented as an `enum` to handle each stage of the connection process. The specific role of each state is explined in the script source code. Some useful events allow the developer to to handle connection transitions and informations.

**Events:**  
- `OnConnectionStateChange<ConnectionState newState>` : Triggered when a transition from one connection state from another occurs.    
- `OnConnectiontStateReceived<JObject payload>` : Triggered when Unity receives a Json message from the server, which "type" field holds "json_state". For further informations about the payload detail, please refer to GamaServerMiddleware documentation
- `OnConnectionAttempted<boolean connectionSuccess>` : Triggered when a Json object with type "json_state" is received from the server, after Unity attempted to connect to it using `TryConnectionToServer` method. The boolean `connectionSuccess` contains true if the connection was successfully established, false otherwise.
- `OnServerMessageReceived` : Triggered when Unity receives a Json message from the server, which "type" field holds "json_simulation". For further informations about the payload detail, please refer to GamaServerMiddleware documentation.

**Methods:**
- `UpdateConnectionState(ConnectionState newState)` : Changes the current connection state to `newState`. Calling this method should be avoided whenever possible, as it could break the default connection process, leading to some undefined state.
- `TryConnectionToServer` : Attemps a connection to the middleware
- `IsConnectionState(ConnectionState currentState)` : Checks current state.
- `SendExecutableExpression(string expression)` : Allows to send an expression to GAMA through the middleware. **Beware** of the arguments expected by GAMA and special characters required by GAMA (such as `;`, `"`, ...) as the expression is executed as it is sent by Unity.
- `GetConnectionId` : Returns the ID created by Unity when the connection was established.

### MenuManager

As mentioned above this script is different from the other Managers as **it mustn't be instanciated at all** and it has no reason to be. It is only used to associate overlays with the GameStates during which they are displayed.  

How it works is decribed in the [Tutorials section](#tutorials).

### SimulationManager

This is the core script of this package. It converts raw incoming json data into a set of functions and events to which the developer can hook up, in order to trigger some actions during the simulation.

**Events**:

- `OnGameStateChanged<GameState newGameState>` : Triggered when a transition from one GameState to another occurs.
- `OnGameRestarted` : Triggered when the function `RestartGame` is called.
- `OnGeometriesInitialized<GAMAGeometry geometries>` : Triggered when the initial geometries sent by GAMA are converted into polygons in the Unity scene. By default, `OnGameStateChanged` is triggered just after this event, to switch from the LOADING_DATA state to the GAME state. Hooking to this event allows to seperate the logic between the game state transition and the loading of geometries.  
:warning: This event is called when incoming geometric data is successfully managed and NOT when it is received.
- `OnWorldDataReceived<WorldJSONInfo worldData>` : Triggered each time the data of the running simulation is received and deserialized into a WorldJSONInfo object. In other word, for a regular GAMA simulation, Unity receives data from it at each (GAMA) timestep. 

**Methods**:

- `void UpdateGameState(GameState newState)` : Changes the current game state to `newState`. This method must be used with caution, as it could break the default game logic, leading to errors in the execution of crucial steps such as initialization or connection steps.
- `GameState GetCurrentState` : Returns the current game state
- `bool IsGameState(GameState state)` : Compares the current game state with the one specified as a parameter.
- `void RestartGame` : Restarts the game. Concretely, it reloads the main scene. This implementation is quite basic and can be enhanced with additional features by using the `OnGameRestarted` event.
- `Timer GetTimer` : Give access to the Timer script provided by this package.
- `void DisplayInfoText(string text)` : Allows to display a text to the user during the simulation. For instance it can be used for warning or error messages related to the connection or the game logic itself. The text is displayed in the infoText component which is accessible from the Unity editor.
- `void RemoveInfoText` : Hides info text from the screen.


### Hooking to a built-in event

If you want to trigger an action when an event occurs (OnGameStateChanged for instance), proceed as follows:
1. Subscribe to the event (here OnGameStateChanged) in the script in which you want to trigger the action:
```csharp
// #### Built-in Unity functions ####
void OnEnable() {
	SimulationManager.Instance.OnGameStateChanged += HandleGameStateChanged;
}

// Unsubscribing is essential to avoid memory leaks
void OnDisable() {
	SimulationManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
}
```

2. Create the event handler function (i.e the function in which you define the actions to perform when the event is triggered):
```csharp
private void HandleGameStateChanged(GameState newState) {
	if (newState == GameManager.GAME) {
		// Do something
	} else {
		// Do something else
	}
}
```

> [!WARNING]
> For some reasons that are not yet explicable, if the actions defined within the event handler function are computationaly to heavy, then the handler function is not executed. Hence we propose a trick to overcome this issue :  


1. Create two local variables, one boolean that will act as a signal and one that will hold the new value of the parameter of the event handler when the event is fired:
```csharp
private GameState currentState;
private bool actionToPerformRequested;

// Built-in Unity function
void start() {
	currentState = SimulationManager.Instance.GetCurrentState();
	actionToPerformRequested = false;
}
```
2. Keep the same subscription mechanism as developed above.
3. Create the event handler and define the action in the Update loop:
```csharp
void Update() {
	if (actionToPerformRequested) {
		actionToPerformRequested = false;
		PerformAction();
	}
}

private void HandleGameStateChanged(GameState newState) {
	if (newState == GameManager.GAME) {
		actionToPerformRequested = true;
	}
}
```
