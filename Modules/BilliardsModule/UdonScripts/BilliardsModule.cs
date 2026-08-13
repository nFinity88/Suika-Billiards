#if UNITY_ANDROID
#define HT_QUEST
#endif

#if !HT_QUEST || true
#define HT8B_DEBUGGER
#endif

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;
using Metaphira.Modules.CameraOverride;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BilliardsModule : UdonSharpBehaviour
{
    [NonSerialized] public readonly string[] DEPENDENCIES = new string[] { nameof(CameraOverrideModule) };
    [NonSerialized] public readonly string VERSION = "6.0.0";

    // table model properties
    [NonSerialized] public float k_TABLE_WIDTH; // horizontal span of table
    [NonSerialized] public float k_TABLE_HEIGHT; // vertical span of table
    [NonSerialized] public float k_CUSHION_RADIUS; // The roundess of colliders
    [NonSerialized] public float k_POCKET_WIDTH_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_HEIGHT_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_RADIUS_SIDE; // Radius of side pockets
    [NonSerialized] public float k_POCKET_DEPTH_SIDE; // Depth of side pockets
    [NonSerialized] public float k_INNER_RADIUS_CORNER; // Pocket 'hitbox' cylinder
    [NonSerialized] public float k_INNER_RADIUS_SIDE; // Pocket 'hitbox' cylinder for corner pockets
    [NonSerialized] public float k_INNER_RADIUS_CORNER2; // Pocket 'hitbox' cylinder
    [NonSerialized] public float k_INNER_RADIUS_SIDE2; // Pocket 'hitbox' cylinder for corner pockets
    [NonSerialized] public float k_FACING_ANGLE_CORNER; // Angle of corner pocket inner walls
    [NonSerialized] public float k_FACING_ANGLE_SIDE; // Angle of side pocket inner walls
    [NonSerialized] public float K_BAULK_LINE; // Snooker baulk line distance from end of table
    [NonSerialized] public float K_BLACK_SPOT; // Snooker Black ball distance from end of table
    [NonSerialized] public float k_SEMICIRCLERADIUS; // Snooker, radius of D
    [NonSerialized] public float k_RAIL_HEIGHT_UPPER;
    [NonSerialized] public float k_RAIL_HEIGHT_LOWER;
    [NonSerialized] public float k_RAIL_DEPTH_WIDTH;
    [NonSerialized] public float k_RAIL_DEPTH_HEIGHT;
    // advanced physics  variables
    [NonSerialized] public float k_F_SLIDE; // bt_CoefSlide
    [NonSerialized] public float k_F_ROLL; // bt_CoefRoll
    [NonSerialized] public float k_F_SPIN; // bt_CoefSpin
    [NonSerialized] public float k_F_SPIN_RATE; // bt_CoefSpinRate
    [NonSerialized] public bool useRailLower; // useRailHeightLower
    [NonSerialized] public bool isDRate; // bt_isDRate
    [NonSerialized] public float K_BOUNCE_FACTOR; // BounceFactor
    [NonSerialized] public float k_POCKET_RESTITUTION; // Reduces bounce inside of pockets
    [Header("Cushion Model:")]
    [NonSerialized] public bool isHanModel; // bc_UseHan05
    [NonSerialized] public float k_E_C; // bc_CoefRestitution
    [NonSerialized] public bool isDynamicRestitution; // bc_DynRestitution
    [NonSerialized] public bool isCushionFrictionConstant; // bc_UseConstFriction
    [NonSerialized] public float k_Cushion_MU; // bc_ConstFriction
    [Header("Ball Set Configuration:")]
    [NonSerialized] public float k_BALL_E; // bs_CoefRestitution
    [NonSerialized] public float muFactor; // bs_Friction
    [NonSerialized] public float k_BALL_RADIUS; // Radius of balls
    [NonSerialized] public float k_BALL_MASS; // Mass of balls
    [NonSerialized] public float k_BALL_DIAMETRE; // Diameter of balls
    [NonSerialized] public Vector3 k_vE; // corner pocket data
    [NonSerialized] public Vector3 k_vF; // side pocket data
    [NonSerialized] public Vector3 k_vE2; // corner pocket data
    [NonSerialized] public Vector3 k_vF2; // side pocket data
    [NonSerialized] public Vector3 k_rack_position = new Vector3();
    private Vector3 k_rack_direction = new Vector3();
    private GameObject auto_rackPosition;
    [NonSerialized] public GameObject auto_pocketblockers;
    private GameObject auto_colliderBaseVFX;
    [NonSerialized] public MeshRenderer[] tableMRs;

    // cue guideline
    private readonly Color k_aimColour_aim = new Color(0.7f, 0.7f, 0.7f, 1.0f);
    private readonly Color k_aimColour_locked = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    // textures
    [SerializeField] public Texture[] textureSets;
    [NonSerialized] public ModelData[] tableModels;
    [SerializeField] public Texture2D[] tableSkins;
    [SerializeField] public Texture2D[] cueSkins;

    // hooks
    [SerializeField] public UdonBehaviour tableSkinHook;
    [SerializeField] public UdonBehaviour cueSkinHook;
    [SerializeField] public UdonBehaviour nameColorHook;

    // globals
    [NonSerialized] public AudioSource aud_main;
    [NonSerialized] public UdonBehaviour callbacks;
    private Vector3[][] initialPositions = new Vector3[5][];

    // constants
    private const float k_RANDOMIZE_F = 0.0001f;
    private float k_SPOT_POSITION_X = 0.5334f; // First X position of the racked balls
    private readonly int[] sixredsnooker_ballpoints = { 0, 7, 2, 5, 1, 6, 1, 3, 4, 1, 1, 1, 1 };
    private readonly int[] break_order_sixredsnooker = { 4, 6, 9, 10, 11, 12, 2, 7, 8, 3, 5, 1 };
    private readonly int[] break_order_8ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
    private readonly int[] break_order_suika12 = { 1, 9, 8, 7, 12, 6, 4, 10, 3, 5, 11, 2 };
    private readonly int[] break_rows_suika12 = { 1, 2, 3, 4, 1, 0, 1 };

    #region InspectorValues
    [Header("Managers")]
    [SerializeField] public NetworkingManager networkingManager;
    [SerializeField] public PracticeManager practiceManager;
    [SerializeField] public RepositionManager repositionManager;
    [SerializeField] public DesktopManager desktopManager;
    [SerializeField] public CameraManager cameraManager;
    [SerializeField] public GraphicsManager graphicsManager;
    [SerializeField] public MenuManager menuManager;
    [SerializeField] public UdonSharpBehaviour[] PhysicsManagers;

    [Header("Camera Module")]
    [SerializeField] public UdonSharpBehaviour cameraModule;

    [Space(10)]
    [Header("Sound Effects")]
    [SerializeField] AudioClip snd_Intro;
    [SerializeField] AudioClip snd_Sink;
    [SerializeField] AudioClip snd_OutOfBounds;
    [SerializeField] AudioClip snd_NewTurn;
    [SerializeField] AudioClip snd_PointMade;
    [SerializeField] public AudioClip snd_btn;
    [SerializeField] public AudioClip snd_spin;
    [SerializeField] public AudioClip snd_spinstop;
    [SerializeField] AudioClip snd_hitball;

    [Space(10)]
    [Header("Other")]
    public float LoDDistance = 10;
    [Tooltip("Shuffle positions of ball spawn points in 8ball and 9ball?")]
    public bool RandomizeBallPositions = true;

    [Space(10)]
    [Header("Table Light Colors")]
    // table colors
    [SerializeField] public Color k_colour_foul;        // v1.6: ( 1.2, 0.0, 0.0, 1.0 )
    [SerializeField] public Color k_colour_default;     // v1.6: ( 1.0, 1.0, 1.0, 1.0 )
    [SerializeField] public Color k_colour_off = new Color(0.01f, 0.01f, 0.01f, 1.0f);

    // 8/9 ball
    [SerializeField] public Color k_teamColour_spots;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_teamColour_stripes; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // Snooker
    [SerializeField] public Color k_snookerTeamColour_0;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_snookerTeamColour_1; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // 4 ball
    [SerializeField] public Color k_colour4Ball_team_0; // v1.6: ( )
    [SerializeField] public Color k_colour4Ball_team_1; // v1.6: ( 2.0, 1.0, 0.0, 1.0 )

    // fabrics
    [SerializeField][HideInInspector] public Color k_fabricColour_8ball; // v1.6: ( 0.3, 0.3, 0.3, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_9ball; // v1.6: ( 0.1, 0.6, 1.0, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_4ball; // v1.6: ( 0.15, 0.75, 0.3, 1.0 )

    [Space(10)]
    [Header("Internal (no touching!)")]
    // Other scripts
    [SerializeField] public CueController[] cueControllers;

    // GameObjects
    [SerializeField] public GameObject[] balls;
    [SerializeField] public Mesh[] ballMeshes;
    [SerializeField] public GameObject guideline;
    [SerializeField] public GameObject guideline2;
    [SerializeField] public GameObject devhit;
    [SerializeField] public GameObject markerObj;
    [SerializeField] public GameObject markerOnBall1;
    [SerializeField] public GameObject markerOnBall2;
    [NonSerialized] public Transform tableSurface;

    // Texts
    [SerializeField] Text ltext;
    [SerializeField] TextMeshProUGUI infReset;

    public ReflectionProbe reflection_main;
    #endregion

    // debugger
    [NonSerialized] public int PERF_MAIN = 0;
    [NonSerialized] public int PERF_PHYSICS_MAIN = 1;
    [NonSerialized] public int PERF_PHYSICS_VEL = 2;
    [NonSerialized] public int PERF_PHYSICS_BALL = 3;
    [NonSerialized] public int PERF_PHYSICS_CUSHION = 4;
    [NonSerialized] public int PERF_PHYSICS_POCKET = 5;

    [NonSerialized] public const int PERF_MAX = 6;
    private string[] perfNames = new string[] {
      "main",
      "physics",
      "physicsVel",
      "physicsBall",
      "physicsCushion",
      "physicsPocket"
   };
    private float[] perfCounters = new float[PERF_MAX];
    private float[] perfTimings = new float[PERF_MAX];
    private float[] perfStart = new float[PERF_MAX];
    private const int LOG_MAX = 32;
    private int LOG_LEN = 0;
    private int LOG_PTR = 0;
    private string[] LOG_LINES = new string[32];

    // cached copies of networked data, may be different from local game state
    [NonSerialized] public byte[] ballIdsCached = {};
    [NonSerialized] public int[] playerIDsCached = { -1, -1, -1, -1 };//the 4 is MAX_PLAYERS from NetworkingManager

    // local game state
    [NonSerialized] public bool lobbyOpen;
    [NonSerialized] public bool gameLive;
    [NonSerialized] public uint gameModeLocal;
    [NonSerialized] public uint timerLocal;
    [NonSerialized] public bool teamsLocal;
    [NonSerialized] public bool noGuidelineLocal;
    [NonSerialized] public bool noLockingLocal;
    [NonSerialized] public byte[] ballIdsLocal = {};
    [NonSerialized] public uint teamIdLocal;
    [NonSerialized] public uint fourBallCueBallLocal;
    [NonSerialized] public bool isTableOpenLocal;
    [NonSerialized] public uint teamColorLocal;
    [NonSerialized] public int numPlayersCurrent = 0;
    [NonSerialized] public int numPlayersCurrentOrange = 0;
    [NonSerialized] public int numPlayersCurrentBlue = 0;
    [NonSerialized] public int[] playerIDsLocal = { -1, -1, -1, -1 };
    [NonSerialized] public byte[] fbScoresLocal = new byte[2];
    [NonSerialized] public uint winningTeamLocal;
    // [NonSerialized] public byte activeCueSkin;
    [NonSerialized] public int tableSkinLocal;
    [NonSerialized] public byte gameStateLocal;
    private byte turnStateLocal;
    private int timerStartLocal;
    [NonSerialized] public uint foulStateLocal;
    [NonSerialized] public int tableModelLocal;
    [NonSerialized] public bool colorTurnLocal;

    // physics simulation data, must be reset before every simulation
    [NonSerialized] public bool isLocalSimulationRunning;
    [NonSerialized] public bool waitingForUpdate;
    [NonSerialized] public bool isLocalSimulationOurs = false;
    [NonSerialized] public int simulationOwnerID;
    private uint numBallsHitCushion = 0; // used to check if 9ball break was legal (4 balls must hit cushion)
    private bool[] ballhasHitCushion;
    private bool ballBounced;//tracks if any ball has touched the cushion after initial ball collision
    private byte[] ballIdsOrig;
    private int firstHit = 0;
    private int secondHit = 0;
    private int thirdHit = 0;
    private bool jumpShotFoul;
    private bool fallOffFoul;

    private bool fbMadePoint = false;
    private bool fbMadeFoul = false;

    // game state data
    [NonSerialized] public Vector3[] ballsP = new Vector3[16];
    [NonSerialized] public Vector3[] ballsV = new Vector3[16];
    [NonSerialized] public Vector3[] ballsW = new Vector3[16];

    [NonSerialized] public bool canPlayLocal;
    [NonSerialized] public bool isGuidelineValid;
    [NonSerialized] public bool canHitCueBall = false;
    [NonSerialized] public bool isReposition = false;
    [NonSerialized] public float repoMaxX;
    [NonSerialized] public bool timerRunning = false;

    [NonSerialized] public int localPlayerId = -1;
    [NonSerialized] public uint localTeamId = uint.MaxValue;

    [NonSerialized] public UdonSharpBehaviour currentPhysicsManager;
    [NonSerialized] public CueController activeCue;

    // some udon optimizations
    [NonSerialized] public bool isSuikaPool = false;
    [NonSerialized] public bool isSuika12 = false;
    [NonSerialized] public bool isPracticeMode = false;
    [NonSerialized] public bool isPlayer = false;
    [NonSerialized] public bool isOrangeTeamFull = false;
    [NonSerialized] public bool isBlueTeamFull = false;
    [NonSerialized] public bool localPlayerDistant = false;

    // use this to make sure max simulation is always visible
    [System.NonSerializedAttribute] public bool noLOD;
    // Add 1 to noLOD_ using SetProgramVariable() to prevent LoD check, subtract to undo
    // this allows more than one other script to disable LoD simultaniously
    [System.NonSerializedAttribute, FieldChangeCallback(nameof(noLOD__))] public int noLOD_ = 0;
    public int noLOD__
    {
        set
        {
            noLOD = value > 0;
            noLOD_ = value;
        }
        get => noLOD_;
    }
    bool checkingDistant;
    GameObject debugger;
    [NonSerialized] public CameraOverrideModule cameraOverrideModule;
    public string[] moderators = new string[0];
    [NonSerialized] public const float ballMeshDiameter = 0.06f;//the ball's size as modeled in the mesh file
    private void OnEnable()
    {
        _LogInfo("initializing billiards module");

        Transform tablesParent = transform.Find("intl.table");
        tableModels = GetComponentsInChildren<ModelData>(true);
        for (int i = 0; i < tableModels.Length; i++)
        {
            tableModels[i].gameObject.SetActive(false);
            tableModels[i]._Init();
        }

        cameraOverrideModule = (CameraOverrideModule)_GetModule(nameof(CameraOverrideModule));

        resetCachedData();

        currentPhysicsManager = PhysicsManagers[0];

        ballIdsLocal = new byte[balls.Length];
        ballIdsLocal[0] = 0x30;
        for (int i = 0; i < balls.Length; i++)
        {
            ballsP[i] = balls[i].transform.localPosition;
            balls[i].GetComponentInChildren<Repositioner>(true)._Init(this, i);

            Rigidbody ballRB = balls[i].GetComponent<Rigidbody>();
            ballRB.maxAngularVelocity = 999;
        }

        aud_main = this.GetComponent<AudioSource>();
        cueControllers[1].TeamBlue = true;
        for (int i = 0; i < cueControllers.Length; i++)
        { cueControllers[i]._Init(); }
        networkingManager._Init(this);
        practiceManager._Init(this);
        repositionManager._Init(this);
        desktopManager._Init(this);
        cameraManager._Init(this);
        graphicsManager._Init(this);
        cameraOverrideModule._Init();
        menuManager._Init(this);

        tableSurface = transform.Find("intl.balls");
        for (int i = 0; i < PhysicsManagers.Length; i++)
        {
            PhysicsManagers[i].SetProgramVariable("table_", this);
            PhysicsManagers[i].SendCustomEvent("_Init");
        }

        currentPhysicsManager.SendCustomEvent("_InitConstants");

        setTableModel(0);

        infReset.text = string.Empty;

        debugger = this.transform.Find("debugger").gameObject;
        debugger.SetActive(true);

        Transform gdisplay = guideline.transform.GetChild(0);
        if (gdisplay)
            gdisplay.GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);
        Transform gdisplay2 = guideline2.transform.GetChild(0);
        if (gdisplay2)
            gdisplay2.GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);

        if (LoDDistance > 0 && !checkingDistant)
        {
            checkingDistant = true;
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), UnityEngine.Random.Range(0, 1f));
        }
    }

    private void _UpdatePointers()
    {
        graphicsManager._UpdatePointers();
        for (int i = 0; i < PhysicsManagers.Length; i++)
        {
            PhysicsManagers[i].SendCustomEvent("_UpdatePointers");
        }
    }

    private void OnDisable()
    {
        checkingDistant = false;
    }

    private void FixedUpdate()
    {
        currentPhysicsManager.SendCustomEvent("_FixedTick");
    }

    private void Update()
    {
        if (localPlayerDistant) { return; }
        desktopManager._Tick();
        // menuManager._Tick();

        _BeginPerf(PERF_MAIN);
        practiceManager._Tick();
        repositionManager._Tick();
        cameraManager._Tick();
        graphicsManager._Tick();
        tickTimer();

        networkingManager._FlushBuffer();
        _EndPerf(PERF_MAIN);

        if (perfCounters[PERF_MAIN] % 500 == 0) _RedrawDebugger();
    }

    public UdonSharpBehaviour _GetModule(string type)
    {
        string[] parts = cameraModule.GetUdonTypeName().Split('.');
        if (parts[parts.Length - 1] == type)
        {
            return cameraModule;
        }
        return null;
    }

    #region Triggers
    public void _TriggerLobbyOpen()
    {
        if (lobbyOpen) return;
        menuManager._EnableLobbyMenu();
        networkingManager._OnLobbyOpened();
    }

    public void _TriggerTeamsChanged(bool teamsEnabled)
    {
        networkingManager._OnTeamsChanged(teamsEnabled);
    }

    public void _TriggerNoGuidelineChanged(bool noGuidelineEnabled)
    {
        networkingManager._OnNoGuidelineChanged(noGuidelineEnabled);
    }

    public void _TriggerNoLockingChanged(bool noLockingEnabled)
    {
        networkingManager._OnNoLockingChanged(noLockingEnabled);
    }

    public void _TriggerTimerChanged(byte timerSelected)
    {
        networkingManager._OnTimerChanged(timerSelected);
    }

    public void _TriggerTableModelChanged(uint TableModelSelected)
    {
        networkingManager._OnTableModelChanged(TableModelSelected);
    }

    public void _TriggerPhysicsChanged(uint PhysicsSelected)
    {
        networkingManager._OnPhysicsChanged(PhysicsSelected);
    }

    public void _TriggerGameModeChanged(uint newGameMode)
    {
        networkingManager._OnGameModeChanged(newGameMode);
    }

    public void _TriggerGlobalSettingsUpdated(int newPhysicsMode, int newTableModel)
    {
        networkingManager._OnGlobalSettingsChanged((byte)newPhysicsMode, (byte)newTableModel);
    }

    public void _TriggerCueBallHit()
    {
        if (!isMyTurn()) return;

        _LogWarn("trying to propagate cue ball hit, linear velocity is " + ballsV[0].ToString("F4") + " and angular velocity is " + ballsW[0].ToString("F4"));

        if (float.IsNaN(ballsV[0].x) || float.IsNaN(ballsV[0].y) || float.IsNaN(ballsV[0].z) || float.IsNaN(ballsW[0].x) || float.IsNaN(ballsW[0].y) || float.IsNaN(ballsW[0].z))
        {
            ballsV[0] = Vector3.zero;
            ballsW[0] = Vector3.zero;
            return;
        }

        _TriggerCueDeactivate();

        networkingManager._OnHitBall(ballsV[0], ballsW[0]);
    }

    public void _TriggerCueActivate()
    {
        if (!isMyTurn() || !activeCue) return;

        if (Vector3.Distance(activeCue._GetCuetip().transform.position, ballsP[0]) < k_BALL_RADIUS)
        {
            _TriggerCueDeactivate();
            return;
        }

        canHitCueBall = true;
        this._TriggerOnPlayerPrepareShoot();

#if !HT_QUEST
        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_locked);
#endif
    }

    public void _TriggerCueDeactivate()
    {
        canHitCueBall = false;

#if !HT_QUEST
        guideline.gameObject.transform.Find("guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_aim);
#endif
    }

    public void _OnPickupCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnPickupCue();
    }

    public void _OnDropCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnDropCue();
    }

    public void _TriggerOnPlayerPrepareShoot()
    {
        networkingManager._OnPlayerPrepareShoot();
    }

    public void _OnPlayerPrepareShoot()
    {
        cameraManager._OnPlayerPrepareShoot();
    }

    public void _TriggerPlaceBall(int idx)
    {
        if (!canPlayLocal) return; // in case player was forced to drop ball since someone else took the shot

        // practiceManager._Record();

        networkingManager._OnRepositionBalls(ballsP);
    }

    public void _TriggerGameStart()
    {

        if (playerIDsLocal[0] == -1)
        {
            _LogWarn("Cannot start without first player");
            return;
        }
        else
        {
            _LogYes("starting game");
        }

        if (gameModeLocal == 0) // Suika Pool
        {
            balls[1].SetActive(true);
            balls[1].GetComponent<MeshFilter>().mesh = ballMeshes[10];
            ballIdsLocal[1] = 0x3B;
            for (int i = 2; i < balls.Length; ++i)
            {
                balls[i].SetActive(false);
                ballIdsLocal[i] = 0;
            }
        }
        else if (gameModeLocal == 1) // Suika 12
        {
            balls[1].SetActive(true);
            balls[1].GetComponent<MeshFilter>().mesh = ballMeshes[0];
            ballIdsLocal[1] = 0x31;
            for (int i = 2; i < 13; ++i)
            {
                balls[i].SetActive(true);
                balls[i].GetComponent<MeshFilter>().mesh = ballMeshes[i - 2];
                ballIdsLocal[i] = (byte)(0x30 | (i - 1));
            }
            for (int i = 13; i < balls.Length; ++i)
            {
                balls[i].SetActive(false);
                ballIdsLocal[i] = 0;
            }
        }

        // 0 is Suika Pool, 1 is Suika 12
        Vector3[] randomPositions = new Vector3[16];
        Array.Copy(initialPositions[gameModeLocal], randomPositions, 16);
        if (RandomizeBallPositions)
        {
            switch (gameModeLocal)
            {
                case 0:
                    // Suika Pool - nothing to randomize
                    break;
                case 1:
                    // Suika 12
                    for (int i = 3; i < 12; i++)
                    {
                        // don't move either cherry (1, 2) or the suika (12)
                        int rand = UnityEngine.Random.Range(3, 11);
                        Vector3 temp = randomPositions[i];
                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    break;
            }
        }

        networkingManager._OnGameStart(randomPositions);
    }

    public void _TriggerJoinTeam(int teamId)
    {
        if (networkingManager.gameStateSynced == 0 || networkingManager.gameStateSynced == 3) return;

        _LogInfo("joining team " + teamId);

        int newslot = networkingManager._OnJoinTeam(teamId);
        if (newslot != -1)
        {
            //for responsive menu prediction. These values will be overwritten in deserialization
            isPlayer = true;
            VRCPlayerApi lp = Networking.LocalPlayer;
            int curSlot = _GetPlayerSlot(lp, playerIDsLocal);
            if (curSlot != -1)
            {
                playerIDsLocal[curSlot] = -1;
                if (curSlot % 2 == 0) { numPlayersCurrentOrange--; }
                else { numPlayersCurrentBlue--; }
            }
            int[] playerIDsLocal_new = new int[4];
            Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
            playerIDsLocal_new[newslot] = lp.playerId;
            onRemotePlayersChanged(playerIDsLocal_new);
        }
        else
        {
            _LogWarn("failed to join team " + teamId + ", did someone else beat you to it?");
        }
    }

    public void _TriggerLeaveLobby()
    {
        if (localPlayerId == -1) return;
        _LogInfo("leaving lobby");

        networkingManager._OnLeaveLobby(localPlayerId);

        //for responsive menu prediction, will be overwritten in deserialization
        isPlayer = false;
        int[] playerIDsLocal_new = new int[4];
        Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
        if (localPlayerId != -1) // true if lobby was closed
        {
            playerIDsLocal_new[localPlayerId] = -1;
        }
        onRemotePlayersChanged(playerIDsLocal_new);
    }
    private float lastActionTime;
    private float lastResetTime;
    public void _TriggerGameReset()
    {
        int self = Networking.LocalPlayer.playerId;

        int[] allowedPlayers = playerIDsLocal;

        bool allPlayersOffline = true;
        bool isAllowedPlayer = false;
        foreach (int allowedPlayer in allowedPlayers)
        {
            if (allPlayersOffline && Utilities.IsValid(VRCPlayerApi.GetPlayerById(allowedPlayer))) allPlayersOffline = false;

            if (allowedPlayer == self) isAllowedPlayer = true;
        }

        float nearestPlayer = float.MaxValue;
        for (int i = 0; i < allowedPlayers.Length; i++)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(allowedPlayers[i]);
            if (!Utilities.IsValid(player)) continue;
            float playerDist = Vector3.Distance(transform.position, player.GetPosition());
            if (playerDist < nearestPlayer)
                nearestPlayer = playerDist;
        }
        bool allPlayersAway = nearestPlayer < 20f ? false : true;

        if (Time.time - lastResetTime > 0.3f)
        {
            infReset.text = "Double Click To Reset"; ClearResetInfo();
        }
        else if (allPlayersOffline || isAllowedPlayer || _IsModerator(Networking.LocalPlayer) || (Time.time - lastActionTime > 300) || allPlayersAway)
        {
            _LogInfo("force resetting game");
            infReset.text = "Game Reset!"; ClearResetInfo();
            networkingManager._OnGameReset();
        }
        else
        {
            string playerStr = "";
            bool has = false;
            foreach (int allowedPlayer in allowedPlayers)
            {
                if (allowedPlayer == -1) continue;
                if (has) playerStr += "\n";
                has = true;

                playerStr += graphicsManager._FormatName(VRCPlayerApi.GetPlayerById(allowedPlayer));
            }

            infReset.text = "<size=60%>Only these players may reset:\n" + playerStr; ClearResetInfo();
        }
        lastResetTime = Time.time;
    }

    int resetInfoCount = 0;
    private void ClearResetInfo()
    {
        resetInfoCount++;
        SendCustomEventDelayedSeconds(nameof(_ClearResetInfo), 3f);
    }

    public void _ClearResetInfo()
    {
        resetInfoCount--;
        if (resetInfoCount != 0) return;
        infReset.text = string.Empty;
    }
    #endregion

    public bool _CanUseTableSkin(string owner, int skin)
    {
        if (tableSkinHook == null) return false;

        tableSkinHook.SetProgramVariable("inOwner", owner);
        tableSkinHook.SetProgramVariable("inSkin", skin);
        tableSkinHook.SendCustomEvent("_CanUseTableSkin");

        return (bool)tableSkinHook.GetProgramVariable("outCanUse");
    }

    public bool _CanUseCueSkin(int owner, int skin)
    {
        if (cueSkinHook == null) return false;

        cueSkinHook.SetProgramVariable("inOwner", owner);
        cueSkinHook.SetProgramVariable("inSkin", skin);
        cueSkinHook.SendCustomEvent("_CanUseCueSkin");

        return (bool)cueSkinHook.GetProgramVariable("outCanUse");
    }


    #region NetworkingClient
    private bool needPointerUpdate;
    // the order is important, unfortunately
    public void _OnRemoteDeserialization()
    {
        _LogInfo("processing latest remote state ("/*packet="  + networkingManager.packetIdSynced + " ,*/+ "state=" + networkingManager.stateIdSynced + ")");

        lastActionTime = Time.time;
        waitingForUpdate = false;
        needPointerUpdate = false;

        // propagate game settings first
        onRemoteGlobalSettingsUpdated(
            (byte)networkingManager.physicsSynced, (byte)networkingManager.tableModelSynced
        );
        onRemoteGameSettingsUpdated(
            networkingManager.gameModeSynced,
            networkingManager.timerSynced,
            networkingManager.teamsSynced,
            networkingManager.noGuidelineSynced,
            networkingManager.noLockingSynced
        );

        // propagate valid players second
        onRemotePlayersChanged(networkingManager.playerIDsSynced);
        // apply state transitions if needed
        onRemoteGameStateChanged(networkingManager.gameStateSynced);

        // now update game state
        onRemoteBallIdsChanged(networkingManager.ballIdsSynced);
        onRemoteBallPositionsChanged(networkingManager.ballsPSynced);
        onRemoteTeamIdChanged(networkingManager.teamIdSynced);
        //onRemoteFourBallCueBallChanged(networkingManager.fourBallCueBallSynced);
        onRemoteColorTurnChanged(networkingManager.colorTurnSynced);
        onRemoteFoulStateChanged(networkingManager.foulStateSynced);
        //onRemoteFourBallScoresUpdated(networkingManager.fourBallScoresSynced);
        onRemoteIsTableOpenChanged(networkingManager.isTableOpenSynced, networkingManager.teamColorSynced);
        onRemoteTurnStateChanged(networkingManager.turnStateSynced);

        if (needPointerUpdate) _UpdatePointers();

        // finally, take a snapshot
        practiceManager._Record();

        redrawDebugger();
    }

    private void onRemoteGlobalSettingsUpdated(byte physicsSynced, byte tableModelSynced)
    {
        // if (gameLive) return;

        _LogInfo($"onRemoteGlobalSettingsUpdated physicsMode={physicsSynced} tableModel={tableModelSynced}");

        if (currentPhysicsManager != PhysicsManagers[physicsSynced])
        {
            currentPhysicsManager = PhysicsManagers[physicsSynced];
            currentPhysicsManager.SendCustomEvent("_InitConstants");
            menuManager._RefreshPhysics();
            desktopManager._RefreshPhysics();
        }
        if (tableModelLocal != tableModelSynced)
        {
            setTableModel(tableModelSynced);
        }
    }

    private void onRemoteGameSettingsUpdated(uint gameModeSynced, uint timerSynced, bool teamsSynced, bool noGuidelineSynced, bool noLockingSynced)
    {
        if (
            gameModeLocal == gameModeSynced &&
            timerLocal == timerSynced &&
            teamsLocal == teamsSynced &&
            noGuidelineLocal == noGuidelineSynced &&
            noLockingLocal == noLockingSynced
        )
        {
            return;
        }

        _LogInfo($"onRemoteGameSettingsUpdated gameMode={gameModeSynced} timer={timerSynced} teams={teamsSynced} guideline={!noGuidelineSynced} locking={!noLockingSynced}");

        if (gameModeLocal != gameModeSynced)
        {
            gameModeLocal = gameModeSynced;

            isSuikaPool = gameModeLocal == 0u;
            isSuika12 = gameModeLocal == 1u;

            menuManager._RefreshGameMode();
        }

        if (timerLocal != timerSynced)
        {
            timerLocal = timerSynced;

            menuManager._RefreshTimer();
        }

        bool refreshToggles = false;
        if (teamsLocal != teamsSynced)
        {
            teamsLocal = teamsSynced;
            refreshToggles = true;
            isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
            isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
            menuManager._RefreshMenu();
        }

        if (noGuidelineLocal != noGuidelineSynced)
        {
            noGuidelineLocal = noGuidelineSynced;
            refreshToggles = true;
        }

        if (noLockingLocal != noLockingSynced)
        {
            noLockingLocal = noLockingSynced;
            refreshToggles = true;
        }

        if (refreshToggles)
        {
            menuManager._RefreshToggleSettings();
            menuManager._RefreshPlayerList();
        }
    }

    private void onRemotePlayersChanged(int[] playerIDsSynced)
    {
        // int myOldSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);

        if (intArrayEquals(playerIDsLocal, playerIDsSynced)) return;

        Array.Copy(playerIDsLocal, playerIDsCached, playerIDsLocal.Length);
        Array.Copy(playerIDsSynced, playerIDsLocal, playerIDsLocal.Length);

        if (networkingManager.gameStateSynced != 3) // don't set practice mode to true after the players are kicked when the game ends
            isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        string[] playerDetails = new string[4];
        for (int i = 0; i < 4; i++)
        {
            VRCPlayerApi plyr = VRCPlayerApi.GetPlayerById(playerIDsSynced[i]);
            playerDetails[i] = (playerIDsSynced[i] == -1 || plyr == null) ? "none" : plyr.displayName;
        }
        _LogInfo($"onRemotePlayersChanged newPlayers={string.Join(",", playerDetails)}");

        localPlayerId = Array.IndexOf(playerIDsLocal, Networking.LocalPlayer.playerId);
        if (localPlayerId != -1) localTeamId = (uint)(localPlayerId & 0x1u);
        else localTeamId = uint.MaxValue;
        cueControllers[0]._SetAuthorizedOwners(new int[] { playerIDsLocal[0], playerIDsLocal[2] });
        cueControllers[1]._SetAuthorizedOwners(new int[] { playerIDsLocal[1], playerIDsLocal[3] });
        cueControllers[1]._RefreshRenderer();// 2nd cue is invisible in practice mode
        if (playerIDsLocal[0] == -1 && playerIDsLocal[2] == -1)
        {
            cueControllers[0]._ResetCuePosition();
        }
        if (playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1)
        {
            cueControllers[1]._ResetCuePosition();
        }

        applyCueAccess();

        if (networkingManager.gameStateSynced != 3) { graphicsManager._SetScorecardPlayers(playerIDsLocal); } // don't remove player names when match is won

        int myNewSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);
        isPlayer = myNewSlot != -1;

        isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
        isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
        menuManager._RefreshLobby();

        // return gameLive && myOldSlot != myNewSlot;//if our slot changed, we left, or we joined, return true
    }

    private void onRemoteGameStateChanged(byte gameStateSynced)
    {
        if (gameStateLocal == gameStateSynced) return;

        gameStateLocal = gameStateSynced;
        _LogInfo($"onRemoteGameStateChanged newState={gameStateSynced}");

        if (gameStateLocal == 1)
        {
            onRemoteLobbyOpened();
        }
        else if (gameStateLocal == 0)
        {
            onRemoteLobbyClosed();
        }
        else if (gameStateLocal == 2)
        {
            onRemoteGameStarted();
        }
        else if (gameStateLocal == 3)
        {
            onRemoteGameEnded(networkingManager.winningTeamSynced);
        }
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
    }

    private void onRemoteLobbyOpened()
    {
        _LogInfo($"onRemoteLobbyOpened");

        lobbyOpen = true;
        graphicsManager._OnLobbyOpened();
        menuManager._RefreshLobby();
        cueControllers[0].resetScale();
        cueControllers[1].resetScale();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyOpened");
    }

    private void onRemoteLobbyClosed()
    {
        _LogInfo($"onRemoteLobbyClosed");

        lobbyOpen = false;
        localPlayerId = -1;
        graphicsManager._OnLobbyClosed();
        menuManager._RefreshLobby();

        if (networkingManager.winningTeamSynced == 2)
        {
            _LogWarn("game reset");
            graphicsManager._OnGameReset();
        }
        gameLive = false;

        disablePlayComponents();
        resetCachedData();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyClosed");
    }

    private void onRemoteGameStarted()
    {
        _LogInfo($"onRemoteGameStarted");

        lobbyOpen = false;
        gameLive = true;

        Array.Clear(perfCounters, 0, PERF_MAX);
        Array.Clear(perfStart, 0, PERF_MAX);
        Array.Clear(perfTimings, 0, PERF_MAX);

        isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        menuManager._RefreshLobby();
        graphicsManager._OnGameStarted();
        desktopManager._OnGameStarted();
        applyCueAccess();
        practiceManager._Clear();
        repositionManager._OnGameStarted();
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        Array.Clear(fbScoresLocal, 0, 2);
        auto_pocketblockers.SetActive(true);
        markerOnBall1.SetActive(isSuika12);
        markerOnBall2.SetActive(isSuika12);

        // Reflect game state
        // graphicsManager._UpdateScorecard();
        isReposition = false;
        markerObj.SetActive(false);

        // Effects
        graphicsManager._PlayIntroAnimation();
        aud_main.PlayOneShot(snd_Intro, 1.0f);

        timerRunning = false;

        activeCue = cueControllers[0];
    }

    private void onRemoteBallPositionsChanged(Vector3[] ballsPSynced)
    {
        if (vector3ArrayEquals(ballsP, ballsPSynced)) return;

        _LogInfo($"onRemoteBallPositionsChanged");

        if (ballsPSynced.Length > ballsP.Length) ballsP = new Vector3[ballsPSynced.Length];
        Array.Copy(ballsPSynced, ballsP, ballsPSynced.Length);

        _UpdateOnBallMarkers();
    }

    private void onRemoteGameEnded(uint winningTeamSynced)
    {
        _LogInfo($"onRemoteGameEnded winningTeam={winningTeamSynced}");

        isLocalSimulationRunning = false;

        winningTeamLocal = winningTeamSynced;

        if (winningTeamLocal < 2)
        {
            string p1str = "No one";
            string p2str = "No one";
            VRCPlayerApi winner1 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal]);
            if (Utilities.IsValid(winner1))
                p1str = winner1.displayName;
            VRCPlayerApi winner2 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal + 2]);
            if (Utilities.IsValid(winner2))
                p2str = winner2.displayName;
            // All players are kicked from the match when it's won, so use the previous turn's player names to show the winners (playerIDsCached)
            _LogWarn("game over, team " + winningTeamLocal + " won (" + p1str + " and " + p2str + ")");
            graphicsManager._SetWinners(/* isPracticeMode ? 0u :  */winningTeamLocal, playerIDsCached);
        }

        gameLive = false;
        isPracticeMode = false;

        Array.Copy(networkingManager.fourBallScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateTeamColor(winningTeamSynced);
        // graphicsManager._UpdateScorecard();
        // graphicsManager._RackBalls();

        disablePlayComponents();

        localPlayerId = -1;
        localTeamId = uint.MaxValue;
        applyCueAccess();

        lobbyOpen = false;

        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        infReset.text = string.Empty;

        resetCachedData();

        menuManager._RefreshLobby();
    }

    private void onRemoteBallIdsChanged(byte[] ballIdsSynced)
    {
        if (!gameLive) return;

        if (ballIdsLocal.Length < ballIdsSynced.Length)
        {
            ballIdsLocal = new byte[ballIdsSynced.Length];

            GameObject[] oldBalls = balls;
            balls = new GameObject[ballIdsSynced.Length];
            Array.Copy(oldBalls, balls, oldBalls.Length);

            needPointerUpdate = true;
        }
        Array.Copy(ballIdsSynced, ballIdsLocal, ballIdsSynced.Length);

        for (int i = 0; i < ballIdsSynced.Length; i++)
        {
            byte syncedId = ballIdsSynced[i];
            if (syncedId != ballIdsLocal[i])
            {
                if (syncedId == 0)
                {
                    // Ball was merged, remove it
                    _LogInfo($"onRemoteBallIdsChanged ball:{i}={ballIdsLocal[i]:X2} removed");
                    ballIdsLocal[i] = 0;
                    balls[i].SetActive(false);
                }
                else
                {
                    // New or upgraded ball
                    byte type = (byte)(syncedId & 0x0F);
                    if (balls[i] == null)
                    {
                        // We need to create a new ball
                        _LogInfo($"onRemoteBallIdsChanged ball:{i}={syncedId:X2} created");
                        GameObject newBall = GameObject.Instantiate(balls[i - 1]);
                        newBall.SetActive(true);
                        newBall.GetComponentInChildren<Repositioner>(true)._Init(this, i);
                        newBall.GetComponent<MeshFilter>().mesh = ballMeshes[type - 1];
                        balls[i] = newBall;
                    }
                    else
                    {
                        // Existing ball, but it changed
                        // Simply swap the ball mesh and enable if needed
                        _LogInfo($"onRemoteBallIdsChanged ball:{i}={ballIdsLocal[i]:X2}>{syncedId:X2} updated");
                        balls[i].SetActive(true);
                        balls[i].GetComponent<MeshFilter>().mesh = ballMeshes[type - 1];
                    }
                    ballIdsLocal[i] = ballIdsSynced[i];
                }
            }
        }

        // graphicsManager._UpdateScorecard();
        // graphicsManager._RackBalls();

        refreshBallPickups();
    }

    private void onRemoteTeamIdChanged(uint teamIdSynced)
    {
        if (!gameLive) return;

        if (teamIdLocal != teamIdSynced)
        {
            teamIdLocal = teamIdSynced;
            aud_main.PlayOneShot(snd_NewTurn, 1.0f);
            _LogInfo($"onRemoteTeamIdChanged newTeam={teamIdSynced}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);

        // always use first cue if practice mode
        activeCue = cueControllers[isPracticeMode ? 0 : (int)teamIdLocal];
    }

    private void onRemoteIsTableOpenChanged(bool isTableOpenSynced, uint teamColorSynced)
    {
        if (!gameLive) return;

        if ((teamColorLocal != teamColorSynced || isTableOpenLocal != isTableOpenSynced))
        {
            _LogInfo($"onRemoteIsTableOpenChanged isTableOpen={isTableOpenSynced} teamColor={teamColorSynced}");
        }
        isTableOpenLocal = isTableOpenSynced;
        teamColorLocal = teamColorSynced;

        if (!isTableOpenLocal)
        {
            string color = (teamIdLocal ^ teamColorLocal) == 0 ? "blues" : "oranges";
            _LogInfo($"table closed, team {teamIdLocal} is {color}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);
        // graphicsManager._UpdateScorecard();
    }
    private void onRemoteColorTurnChanged(bool ColorTurnSynced)
    {
        if (!gameLive) return;

        if (colorTurnLocal == ColorTurnSynced) return;

        _LogInfo($"onRemoteColorTurnChanged colorTurn={ColorTurnSynced}");
        colorTurnLocal = ColorTurnSynced;
    }

    private void onRemoteFoulStateChanged(uint foulStateSynced)
    {
        if (!gameLive) return;

        if (foulStateLocal != foulStateSynced)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={foulStateSynced}");
            // should not escape here because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        }

        foulStateLocal = foulStateSynced;
        bool myTurn = isMyTurn();

        if (!myTurn || foulStateLocal == 0)
        {
            isReposition = false;
            setFoulPickupEnabled(false);
            return;
        }

        if (foulStateLocal > 0 && foulStateLocal < 4)
        {
            isReposition = true;

            switch (foulStateLocal)
            {
                case 1://kitchen
                    repoMaxX = -k_TABLE_WIDTH / 2;
                    break;
                case 2://anywhere
                    repoMaxX = k_TABLE_WIDTH - k_BALL_RADIUS;
                    break;
                case 3://snooker D
                    repoMaxX = K_BAULK_LINE;
                    break;
            }
            setFoulPickupEnabled(true);
        }
    }

    private void onRemoteTurnBegin(int timerStartSynced)
    {
        _LogInfo("onRemoteTurnBegin");
        canPlayLocal = true;
        timerStartLocal = timerStartSynced;

        enablePlayComponents();
        Array.Clear(ballsV, 0, ballsV.Length);
        Array.Clear(ballsW, 0, ballsW.Length);
    }

    private void onRemoteTurnSimulate(Vector3 cueBallV, Vector3 cueBallW, bool fake = false)
    {
        VRCPlayerApi owner = Networking.GetOwner(networkingManager.gameObject);
        simulationOwnerID = Utilities.IsValid(owner) ? owner.playerId : -1;
        bool isOwner = owner == Networking.LocalPlayer || fake;
        _LogInfo($"onRemoteTurnSimulate cueBallV={cueBallV.ToString("F4")} cueBallW={cueBallW.ToString("F4")} owner={simulationOwnerID}");

        if (!fake)
            balls[0].GetComponent<AudioSource>().PlayOneShot(snd_hitball, 1.0f);

        canPlayLocal = false;
        disablePlayComponents();

        bool TableVisible = !localPlayerDistant;
        if (TableVisible)
        {
            for (int i = 0; i < tableMRs.Length; i++)
            {
                if (tableMRs[i].isVisible)
                {
                    TableVisible = true;
                    break;
                }
            }
        }
        if (!_IsPlayer(Networking.LocalPlayer) && !isOwner && (!TableVisible || localPlayerDistant))
        {
            // don't bother simulating if the table isn't even visible
            _LogWarn("skipping simulation");
            return;
        }

        isLocalSimulationRunning = true;
        firstHit = 0;
        secondHit = 0;
        thirdHit = 0;
        fbMadePoint = false;
        fbMadeFoul = false;
        ballBounced = false;
        numBallsHitCushion = 0;
        int numBalls = ballIdsLocal.Length;
        ballhasHitCushion = new bool[numBalls];
        ballIdsOrig = new byte[numBalls];
        Array.Copy(ballIdsLocal, ballIdsOrig, numBalls);
        jumpShotFoul = false;
        fallOffFoul = false;
        currentPhysicsManager.SendCustomEvent("_ResetSimulationVariables");
        numBallsPocketedThisTurn = 0;

        if (Networking.LocalPlayer.playerId == simulationOwnerID || fake)
        {
            isLocalSimulationOurs = true;
        }

        for (int i = 0; i < ballsV.Length; i++)
        {
            ballsV[i] = Vector3.zero;
            ballsW[i] = Vector3.zero;
        }
        ballsV[0] = cueBallV;
        ballsW[0] = cueBallW;

        auto_colliderBaseVFX.SetActive(true);
    }

    private void onRemoteTurnStateChanged(byte turnStateSynced)
    {
        if (!gameLive) return;

        // should not escape because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        bool stateChanged = false;
        if (turnStateSynced != turnStateLocal)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={turnStateSynced}");
            stateChanged = true;
        }
        turnStateLocal = turnStateSynced;

        if (turnStateLocal == 0 || turnStateLocal == 2)
        {
            /* if (turnStateLocal == 2) */
            turnStateLocal = 0; // synthetic state

            onRemoteTurnBegin(networkingManager.timerStartSynced);
            // practiceManager._Record();
            auto_colliderBaseVFX.SetActive(false);
        }
        else if (turnStateLocal == 1)
        {
            // prevent simulation from running twice if a serialization was sent during sim
            if (stateChanged || networkingManager.isUrgentSynced == 2)
                onRemoteTurnSimulate(networkingManager.cueBallVSynced, networkingManager.cueBallWSynced);
            // practiceManager._Record();
        }
        else
        {
            canPlayLocal = false;
            disablePlayComponents();
        }
    }
    #endregion

    #region PhysicsEngineCallbacks
    public void _TriggerBounceCushion(int ball)
    {
        if (!ballhasHitCushion[ball] && ball != 0)
        {
            numBallsHitCushion++;
            ballhasHitCushion[ball] = true;
        }
        if (firstHit != 0)
        { ballBounced = true; }
    }
    public void _TriggerCollision(int srcId, int dstId)
    {
        if (isSuika12 && firstHit == 0)
        {
            if (dstId < srcId)
            {
                int tmp = dstId;
                dstId = srcId;
                srcId = tmp;
            }
            if (srcId != 0) return;
            firstHit = dstId;
        }
    }

    private int numBallsPocketedThisTurn;
    public void _TriggerPocketBall(int id, bool outOfBounds)
    {
        // uint total = 0U;
        //
        // // Get total for X positioning
        // int count_extent = isSuika12 ? 13 : 16;
        // for (int i = 1; i < count_extent; i++)
        // {
        //     total += (ballsPocketedLocal >> i) & 0x1U;
        // }
        //
        // // place ball on the rack
        // ballsP[id] = k_rack_position + (float)total * k_BALL_DIAMETRE * k_rack_direction;
        //
        // ballsPocketedLocal ^= 1U << id;

        bool foulPocket = false;
        if (isSuika12)
        {
            int ballOn = findLowestUnpocketedBall(ballIdsOrig);
            foulPocket = (ballOn != firstHit && ballOn + 1 != firstHit) || id == 0;
        }
        foulPocket |= fallOffFoul;
        if (foulPocket)
        {
            graphicsManager._FlashTableError();
        }
        else
        {
            graphicsManager._FlashTableLight();
        }
        if (outOfBounds)
        { if (snd_OutOfBounds) aud_main.PlayOneShot(snd_OutOfBounds, 1.0f); }
        else
        { if (snd_Sink) aud_main.PlayOneShot(snd_Sink, 1.0f); }

        // VFX ( make ball move )
        // Rigidbody body = balls[id].GetComponent<Rigidbody>();
        // body.isKinematic = false;
        // body.velocity = transform.TransformDirection(ballsV[id]);
        // body.angularVelocity = transform.TransformDirection(ballsW[id].normalized) * -ballsW[id].magnitude;

        ballsV[id] = Vector3.zero;
        ballsW[id] = Vector3.zero;
    }

    public void _TriggerUpgradeBall(int id)
    {
        if (isSuikaPool)
        {
            int type = ballIdsLocal[id] & 0x0F;
            if (type == 11)
            {
                _TriggerPocketBall(id, false);
            }
            else
            {
                balls[id].GetComponent<MeshFilter>().mesh = ballMeshes[type + 1];
                ballIdsLocal[id]++;
            }
        }
        else // if (isSuika12)
        {
            if (id < 12) {
                balls[id].GetComponent<MeshFilter>().mesh = ballMeshes[id - 1];
            } else {
                _TriggerPocketBall(id, false);
            }
        }
    }

    public void _TriggerJumpShotFoul() { jumpShotFoul = true; }
    public void _TriggerBallFallOffFoul() { fallOffFoul = true; }

    public void _TriggerSimulationEnded(bool forceScratch, bool forceRun = false)
    {
        if (!isLocalSimulationRunning && !forceRun) return;
        isLocalSimulationRunning = false;
        waitingForUpdate = !isLocalSimulationOurs;

        if (!isLocalSimulationOurs && networkingManager.delayedDeserialization)
            networkingManager.OnDeserialization();

        cameraManager._OnLocalSimEnd();

        auto_colliderBaseVFX.SetActive(false);

        // Make sure we only run this from the client who initiated the move
        if (isLocalSimulationOurs || forceRun)
        {
            isLocalSimulationOurs = false;

            // Common informations
            bool isScratch = /*(ballsPocketedLocal & 0x1U) == 0x1U ||*/ forceScratch;
            bool nextTurnBlocked = false;

            //ballsPocketedLocal = ballsPocketedLocal & ~(0x1U);
            if (isScratch) ballsP[0] = Vector3.zero;
            //keep moving ball down the table until it's not touching any other balls
            moveBallInDirUntilNotTouching(0, Vector3.right * k_BALL_RADIUS * .051f);

            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
                isObjectiveSink,
                isOpponentSink,
                winCondition,
                foulCondition,
                deferLossCondition
            ;

            if (isSuikaPool)
            {
                isObjectiveSink = false;
                isOpponentSink = false;
                byte objectiveMask = (byte)(0x10 << (int)teamIdLocal);
                for (int i = 0; i < ballIdsLocal.Length; i++)
                {
                    byte origId = ballIdsOrig[i];
                    if (origId != 0 && ballIdsLocal[i] == 0)
                    {
                        // Check this first since golden Suika is marked for both teams
                        if ((origId & objectiveMask) != 0)
                        {
                            isObjectiveSink = true;
                        }
                        else
                        {
                            isOpponentSink = true;
                        }
                    }
                }

                // Calculate if objective was not hit first
                bool isWrongHit = (ballIdsLocal[firstHit] & objectiveMask) == 0;

                bool isSuikaSink = ballIdsLocal[1] == 0;

                winCondition = isSuikaSink;

                foulCondition = isScratch || isWrongHit || fallOffFoul || ((!isObjectiveSink && !isOpponentSink) && (!ballBounced || (colorTurnLocal && numBallsHitCushion < 4)));

                if (isScratch && colorTurnLocal)
                {
                    nextTurnBlocked = true; // re-using snooker variable for reposition to kitchen
                    ballsP[0].x = -k_TABLE_WIDTH / 2;
                }

                deferLossCondition = fallOffFoul;

                colorTurnLocal = false; // colorTurnLocal tracks if it's the break
            }
            else // if (isSuika12)
            {
                // Suika-12 inherits from 9 ball rules
                int target = findLowestUnpocketedBall(ballIdsOrig);
                bool isWrongHit = firstHit != target && firstHit != target + 1;

                isObjectiveSink = ballIdsLocal[target] == 0;

                isOpponentSink = false;
                deferLossCondition = fallOffFoul;

                foulCondition = isWrongHit || isScratch || fallOffFoul || (!isObjectiveSink && (!ballBounced || (colorTurnLocal && numBallsHitCushion < 4)));

                colorTurnLocal = false;// colorTurnLocal tracks if it's the break,

                // Win condition: Merge watermelons - one of them must be ball #12 ( and do not foul )
                bool isSuikaSink = ballIdsLocal[12] == 0;
                winCondition = isSuikaSink && !foulCondition;

                // To get here, you'd have to knock the watermelon off the table
                if (isSuikaSink && !winCondition)
                {
                    isSuikaSink = false;
                    ballIdsLocal[12] = 0x3B;
                    balls[12].SetActive(true);
                    ballsP[12] = initialPositions[1][12];
                    //keep moving ball down the table until it's not touching any other balls
                    moveBallInDirUntilNotTouching(12, Vector3.right * .051f);
                }
            }

            networkingManager._OnSimulationEnded(ballsP, ballIdsLocal, fbScoresLocal, colorTurnLocal);

            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    onLocalTeamWin(teamIdLocal ^ 0x1U);
                }
                else
                {
                    // Win
                    onLocalTeamWin(teamIdLocal);
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                onLocalTeamWin(teamIdLocal ^ 0x1U);
            }
            else if (foulCondition)
            {
                // Foul
                onLocalTurnFoul(isScratch, nextTurnBlocked);
            }
            else if (isObjectiveSink && (!isOpponentSink))
            {
                // Continue
                onLocalTurnContinue();
            }
            else
            {
                // Pass
                onLocalTurnPass();
            }
        }
    }
    private void moveBallToNearestFreePointBySpot(int Ball, Vector3 Spot)
    {
        //TODO: Make this function and use it instead of moveBallInDirUntilNotTouching() at the end of sixRedMoveBallUntilNotTouching()
        //TODO: check positions in all directions around spot instead of just moving in one direction 
    }
    private void moveBallInDirUntilNotTouching(int Ball, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall(Ball) > -1)
        {
            ballsP[Ball] += Dir;
        }
    }
    private int CheckIfBallTouchingBall(int Input)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
        for (int i = 0; i < balls.Length; i++)
        {
            if (i == Input) { continue; }
            if (ballIdsLocal[i] == 0) { continue; }
            if ((ballsP[Input] - ballsP[i]).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    private void moveBallInDirUntilNotTouching_Transform(int id, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall_Transform(id) > -1)
        {
            balls[id].transform.localPosition += Dir;
        }
    }
    private int CheckIfBallTouchingBall_Transform(int id)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
        for (int i = 0; i < balls.Length; i++)
        {
            if (i == id) { continue; }
            if (ballIdsLocal[i] == 0) { continue; }
            if ((balls[id].transform.position - balls[i].transform.position).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    #endregion

    #region GameLogic
    private void initializeRack()
    {
        float k_BALL_PL_X = k_BALL_RADIUS; // break placement X
        float k_BALL_PL_Y = Mathf.Sin(60 * Mathf.Deg2Rad) * k_BALL_DIAMETRE; // break placement Y
        float quarterTable = k_TABLE_WIDTH / 2;
        for (int i = 0; i < 5; i++)
        {
            initialPositions[i] = new Vector3[16];
            for (int j = 0; j < 16; j++)
            {
                initialPositions[i][j] = Vector3.zero;
            }

            // cue ball always starts here (unless four ball, but we override below)
            initialPositions[i][0] = new Vector3(-quarterTable, 0.0f, 0.0f);
        }

        {
            // 8 ball
            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[0][break_order_8ball[k++]] = new Vector3
                    (
                       quarterTable + i * k_BALL_PL_Y /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/,
                       0.0f,
                       (-i + j * 2) * k_BALL_PL_X /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/
                    );
                }
            }
        }

        {
            // Suika 12
            for (int i = 0, k = 0; i < 7; i++)
            {
                int rown = break_rows_suika12[i];
                for (int j = 0; j < rown; j++)
                {
                    initialPositions[1][break_order_suika12[k++]] = new Vector3
                    (
                       quarterTable - (k_BALL_PL_Y * 2) + i * k_BALL_PL_Y /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */,
                       0.0f,
                       (1 - rown + j * 2) * k_BALL_PL_X /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */
                    );
                }
            }
        }
    }

    private void resetCachedData()
    {
        for (int i = 0; i < 4; i++)
        {
            playerIDsLocal[i] = -1;
        }
        foulStateLocal = 0;
        gameModeLocal = int.MaxValue;
        turnStateLocal = byte.MaxValue;
    }

    public void setTransform(Transform src, Transform dest, bool doScale = false, float sf = 1f)
    {
        dest.position = src.position;
        dest.rotation = src.rotation;
        if (!doScale) return;
        dest.localScale = src.localScale * sf;
    }

    private void setTableModel(int newTableModel)
    {
        tableModels[tableModelLocal].gameObject.SetActive(false);
        tableModels[newTableModel].gameObject.SetActive(true);

        tableModelLocal = newTableModel;

        ModelData data = tableModels[tableModelLocal];
        k_TABLE_WIDTH = data.tableWidth * .5f;
        k_TABLE_HEIGHT = data.tableHeight * .5f;
        k_CUSHION_RADIUS = data.cushionRadius;
        k_POCKET_WIDTH_CORNER = data.pocketWidthCorner;
        k_POCKET_HEIGHT_CORNER = data.pocketHeightCorner;
        k_POCKET_RADIUS_SIDE = data.pocketRadiusSide;
        k_POCKET_DEPTH_SIDE = data.pocketDepthSide;
        k_INNER_RADIUS_CORNER = data.pocketInnerRadiusCorner;
        k_INNER_RADIUS_SIDE = data.pocketInnerRadiusSide;
        k_INNER_RADIUS_CORNER2 = data.pocketInnerRadiusCorner2;
        k_INNER_RADIUS_SIDE2 = data.pocketInnerRadiusSide2;
        k_FACING_ANGLE_CORNER = data.facingAngleCorner;
        k_FACING_ANGLE_SIDE = data.facingAngleSide;
        K_BAULK_LINE = -(k_TABLE_WIDTH - data.baulkLine);
        K_BLACK_SPOT = k_TABLE_WIDTH - data.blackSpot;
        k_SEMICIRCLERADIUS = data.semiCircleRadius;
        k_BALL_DIAMETRE = data.bs_BallDiameter / 1000f;
        k_BALL_RADIUS = k_BALL_DIAMETRE * .5f;
        k_BALL_MASS = data.bs_BallMass;
        k_RAIL_HEIGHT_UPPER = data.railHeightUpper;
        k_RAIL_HEIGHT_LOWER = data.railHeightLower;
        k_RAIL_DEPTH_WIDTH = data.railDepthWidth;
        k_RAIL_DEPTH_HEIGHT = data.railDepthHeight;
        k_SPOT_POSITION_X = k_TABLE_WIDTH - data.pinkSpot;
        k_POCKET_RESTITUTION = data.bt_PocketRestitutionFactor;
        k_vE = data.cornerPocket;
        k_vF = data.sidePocket;
        k_vE2 = data.cornerPocket2;
        k_vF2 = data.sidePocket2;

        //advanced physics
        useRailLower = data.useRailHeightLower;
        k_F_SLIDE = data.bt_CoefSlide;
        k_F_ROLL = data.bt_CoefRoll;
        k_F_SPIN = data.bt_CoefSpin;
        k_F_SPIN_RATE = data.bt_CoefSpinRate;
        isDRate = data.bt_ConstDecelRate;
        K_BOUNCE_FACTOR = data.bt_BounceFactor;
        isHanModel = data.bc_UseHan05;
        k_E_C = data.bc_CoefRestitution;
        isDynamicRestitution = data.bc_DynRestitution;
        isCushionFrictionConstant = data.bc_UseConstFriction;
        k_Cushion_MU = data.bc_ConstFriction;
        k_BALL_E = data.bs_CoefRestitution;
        muFactor = data.bs_Friction;

        tableMRs = tableModels[newTableModel].GetComponentsInChildren<MeshRenderer>();

        float newscale = k_BALL_DIAMETRE / ballMeshDiameter;
        Vector3 newBallSize = Vector3.one * newscale;
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localScale = newBallSize;
        }
        float table_base = _GetTableBase().transform.Find(".TABLE_SURFACE").localPosition.y;
        tableSurface.localPosition = new Vector3(0, table_base + k_BALL_RADIUS, 0);

        SetTableTransforms();

        k_rack_position = tableSurface.InverseTransformPoint(auto_rackPosition.transform.position);
        k_rack_direction = tableSurface.InverseTransformDirection(auto_rackPosition.transform.up);

        currentPhysicsManager.SendCustomEvent("_InitConstants");
        graphicsManager._InitializeTable();

        cueControllers[0]._RefreshTable();
        cueControllers[1]._RefreshTable();

        desktopManager._RefreshTable();

        //set height of guideline
        Transform guideDisplay = guideline.gameObject.transform.Find("guide_display");
        Vector3 newpos = guideDisplay.localPosition; newpos.y = 0;
        newpos += Vector3.down * (k_BALL_RADIUS - 0.003f) / guideline.transform.localScale.y;// divide to convert back to worldspace distance
        guideDisplay.localPosition = newpos;
        guideDisplay.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));
        Transform guideDisplay2 = guideline2.gameObject.transform.Find("guide_display");
        guideDisplay2.localPosition = newpos;
        guideDisplay2.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));
        guideDisplay2.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));

        //set height of on-ball markers
        newpos = markerOnBall1.transform.localPosition; newpos.y = 0;
        newpos += Vector3.down * -0.003f / markerOnBall1.transform.localScale.y;
        markerOnBall1.transform.localPosition = newpos;

        newpos = markerOnBall2.transform.localPosition; newpos.y = 0;
        newpos += Vector3.down * -0.003f / markerOnBall2.transform.localScale.y;
        markerOnBall2.transform.localPosition = newpos;

        initializeRack();
        ConfineBallTransformsToTable();

        menuManager._RefreshTable();
    }

    private void SetTableTransforms()
    {
        Transform table_base = _GetTableBase().transform;
        auto_pocketblockers = table_base.Find(".4BALL_FILL").gameObject;
        auto_rackPosition = table_base.Find(".RACK").gameObject;
        auto_colliderBaseVFX = table_base.Find("collision.vfx").gameObject;

        Transform NAME_0_SPOT = table_base.Find(".NAME_0");
        Transform MENU_SPOT = table_base.Find(".MENU");

        Transform score_info_root = this.transform.Find("intl.scorecardinfo");
        Transform player0name = score_info_root.Find("player0-name");
        if (NAME_0_SPOT && player0name)
            setTransform(NAME_0_SPOT, player0name);

        Transform NAME_1_SPOT = table_base.Find(".NAME_1");
        Transform player1name = score_info_root.Find("player1-name");
        if (NAME_1_SPOT && player1name)
            setTransform(NAME_1_SPOT, player1name);

        Transform SCORE_0_SPOT = table_base.Find(".SCORE_0");
        Transform player0score = score_info_root.Find("player0-score");
        if (SCORE_0_SPOT && player0score)
            setTransform(SCORE_0_SPOT, player0score);

        Transform SCORE_1_SPOT = table_base.Find(".SCORE_1");
        Transform player1score = score_info_root.Find("player1-score");
        if (SCORE_1_SPOT && player1score)
            setTransform(SCORE_1_SPOT, player1score);

        Transform SNOOKER_INSTRUCTIONS_SPOT = table_base.Find(".SNOOKER_INSTRUCTIONS");
        Transform SnookerInstructions = score_info_root.Find("SnookerInstructions");
        if (SNOOKER_INSTRUCTIONS_SPOT && SnookerInstructions)
            setTransform(SNOOKER_INSTRUCTIONS_SPOT, SnookerInstructions);

        Transform menu = this.transform.Find("intl.menu/MenuAnchor");
        if (MENU_SPOT && menu)
            setTransform(MENU_SPOT, menu);
    }

    private void ConfineBallTransformsToTable()
    {
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localPosition = ballsP[i];
            Vector3 thisBallPos = balls[i].transform.localPosition;

            float r_k_CUSHION_RADIUS = k_CUSHION_RADIUS + k_BALL_RADIUS;
            if (thisBallPos.x > k_TABLE_WIDTH - r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = k_TABLE_WIDTH - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.x < -k_TABLE_WIDTH + r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = -k_TABLE_WIDTH + r_k_CUSHION_RADIUS;
            }
            if (thisBallPos.z > k_TABLE_HEIGHT - r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = k_TABLE_HEIGHT - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.z < -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS;
            }
            balls[i].transform.localPosition = thisBallPos;
            Vector3 moveDir = -thisBallPos.normalized;
            if (moveDir == Vector3.zero) { moveDir = Vector3.right; }
            moveBallInDirUntilNotTouching_Transform(i, moveDir * k_BALL_RADIUS);
        }
    }

    public GameObject _GetTableBase()
    {
        return tableModels[tableModelLocal].transform.Find("table_artwork").gameObject;
    }

    private void onLocalTeamWin(uint winner)
    {
        _LogInfo($"onLocalTeamWin {(winner)}");

        networkingManager._OnGameWin(winner);
    }

    private void onLocalTurnPass()
    {
        _LogInfo($"onLocalTurnPass");

        networkingManager._OnTurnPass(teamIdLocal ^ 0x1u);
    }

    private void onLocalTurnTie()
    {
        _LogInfo($"onLocalTurnTie");

        networkingManager._OnTurnTie();
    }

    private void onLocalTurnFoul(bool Scratch, bool objBlocked)
    {
        _LogInfo($"onLocalTurnFoul");

        networkingManager._OnTurnFoul(teamIdLocal ^ 0x1u, Scratch, objBlocked);
    }

    private void onLocalTurnContinue()
    {
        _LogInfo($"onLocalTurnContinue");

        networkingManager._OnTurnContinue();
    }

    private void onLocalTimerEnd()
    {
        timerRunning = false;

        _LogWarn("out of time!");

        graphicsManager._HideTimers();

        canPlayLocal = false;

        if (Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject))
        {
            fakeFoulShot();
        }
    }

    private void applyCueAccess()
    {
        if (localPlayerId == -1 || !gameLive)
        {
            cueControllers[0]._Disable();
            cueControllers[1]._Disable();
            return;
        }

        if (localTeamId == 0)
        {
            cueControllers[0]._Enable();
            cueControllers[1]._Disable();
        }
        else
        {
            cueControllers[1]._Enable();
            cueControllers[0]._Disable();
        }
    }

    private void enablePlayComponents()
    {
        bool isOurTurnVar = isMyTurn();

        if (isSuika12)
        {
            markerOnBall1.SetActive(true);
            markerOnBall2.SetActive(true);
            _UpdateOnBallMarkers();
        }

        refreshBallPickups();

        if (isOurTurnVar)
        {
            // Update for desktop
            desktopManager._AllowShoot();
            menuManager._EnableSkipTurnMenu();
        }
        else
        {
            desktopManager._DenyShoot();
            menuManager._DisableSkipTurnMenu();
        }

        if (timerLocal > 0)
        {
            timerRunning = true;
            graphicsManager._ShowTimers();
        }
    }

    public void _SkipTurn()
    {
        if (!isMyTurn()) { return; }
        fakeFoulShot();
    }

    public void fakeFoulShot()
    {
        onRemoteTurnSimulate(Vector3.zero, Vector3.zero, true);
        _TriggerSimulationEnded(false, true);
    }

    public void _UpdateOnBallMarkers()
    {
        if (markerOnBall1.activeSelf)
        {
            int target = findLowestUnpocketedBall(ballIdsLocal);
            // move without changing y
            Vector3 oldpos = markerOnBall1.transform.localPosition;
            Vector3 newpos = ballsP[target];
            markerOnBall1.transform.localPosition = new Vector3(newpos.x, oldpos.y, newpos.z);

            if (markerOnBall2.activeSelf)
            {
                newpos = ballsP[target + 1];
                markerOnBall2.transform.localPosition = new Vector3(newpos.x, oldpos.y, newpos.z);
            }
        }
    }

    // turn off any game elements that are enabled when someone is taking a shot
    private void disablePlayComponents()
    {
        markerOnBall1.SetActive(false);
        markerOnBall2.SetActive(false);
        setFoulPickupEnabled(false);
        refreshBallPickups();
        devhit.SetActive(false);
        guideline.SetActive(false);
        guideline2.SetActive(false);
        isGuidelineValid = false;
        isReposition = false;
        auto_colliderBaseVFX.SetActive(false);

        desktopManager._DenyShoot();
        graphicsManager._HideTimers();
    }

    public int findLowestUnpocketedBall(byte[] ballIds)
    {
        for (int i = 1; i < ballIds.Length; i++)
        {
            if (ballIds[i] != 0) return i;
        }

        // ??
        return 0;
    }

    private void setBallPickupActive(int ballId, bool active)
    {
        Transform pickup = balls[ballId].transform.GetChild(0);

        pickup.gameObject.SetActive(active);
        pickup.GetComponent<SphereCollider>().enabled = active;
        ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).pickupable = active;
        if (!active) ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).Drop();
    }

    private void refreshBallPickups()
    {
        bool canUsePickup = isMyTurn() && isPracticeMode && gameLive;

        for (int i = 0; i < balls.Length; i++)
        {
            if ((canUsePickup || (i == 0 && isReposition)) && gameLive && canPlayLocal && ballIdsLocal[i] != 0x0u)
            {
                setBallPickupActive(i, true);
            }
            else
            {
                setBallPickupActive(i, false);
            }
        }
    }

    private void setFoulPickupEnabled(bool enabled)
    {
        markerObj.SetActive(enabled);
        if (enabled)
        {
            setBallPickupActive(0, true);
        }
        else if (!isPracticeMode)
        {
            setBallPickupActive(0, false);
        }
    }

    private void tickTimer()
    {
        if (gameLive && timerRunning && canPlayLocal)
        {
            float timeRemaining = timerLocal - (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
            float timePercentage = timeRemaining >= 0.0f ? 1.0f - (timeRemaining / timerLocal) : 0.0f;

            if (!localPlayerDistant)
            {
                graphicsManager._SetTimerPercentage(timePercentage);
            }

            if (timeRemaining < 0.0f)
            {
                onLocalTimerEnd();
            }
        }
    }

    public bool isMyTurn()
    {
        return localPlayerId >= 0 && (localTeamId == teamIdLocal || (isPracticeMode && isPlayer));
    }

    public bool _AllPlayersOffline()
    {
        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == -1) continue;

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerIDsLocal[i]);
            if (Utilities.IsValid(player))
            {
                return false;
            }
        }

        return true;
    }

    public VRCPlayerApi _GetPlayerByName(string name)
    {
        VRCPlayerApi[] onlinePlayers = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
        for (int playerId = 0; playerId < onlinePlayers.Length; playerId++)
        {
            if (onlinePlayers[playerId].displayName == name)
            {
                return onlinePlayers[playerId];
            }
        }
        return null;
    }

    public void _IndicateError()
    {
        graphicsManager._FlashTableError();
    }

    public void _IndicateSuccess()
    {
        graphicsManager._FlashTableLight();
    }

    public string _SerializeGameState()
    {
        return networkingManager._EncodeGameState();
    }

    public void _LoadSerializedGameState(string gameState)
    {
        // no loading on top of other people's games
        if (!_IsPlayer(Networking.LocalPlayer)) return;

        networkingManager._OnLoadGameState(gameState);
        // practiceManager._Record();
    }

    public object[] _SerializeInMemoryState()
    {
        Vector3[] positionClone = new Vector3[ballsP.Length];
        Array.Copy(ballsP, positionClone, ballsP.Length);
        byte[] scoresClone = new byte[fbScoresLocal.Length];
        Array.Copy(fbScoresLocal, scoresClone, fbScoresLocal.Length);
        return new object[13]
        {
            positionClone, ballIdsLocal, scoresClone, gameModeLocal, teamIdLocal, foulStateLocal, isTableOpenLocal, teamColorLocal, fourBallCueBallLocal,
            turnStateLocal, networkingManager.cueBallVSynced, networkingManager.cueBallWSynced, colorTurnLocal
        };
    }

    public void _LoadInMemoryState(object[] state, int stateIdLocal)
    {
        networkingManager._ForceLoadFromState(
            stateIdLocal,
            (Vector3[])state[0], (byte[])state[1], (byte[])state[2], (uint)state[3], (uint)state[4], (uint)state[5], (bool)state[6], (uint)state[7], (uint)state[8],
            (byte)state[9], (Vector3)state[10], (Vector3)state[11], (bool)state[12]
        );
    }

    public bool _AreInMemoryStatesEqual(object[] a, object[] b)
    {
        Vector3[] posA = (Vector3[])a[0];
        Vector3[] posB = (Vector3[])b[0];
        for (int i = 0; i < ballsP.Length; i++) if (posA[i] != posB[i]) return false;

        byte[] scoresA = (byte[])a[2];
        byte[] scoresB = (byte[])b[2];
        for (byte i = 0; i < fbScoresLocal.Length; i++) if (scoresA[i] != scoresB[i]) return false;

        for (byte i = 0; i < a.Length; i++) if (i != 0 && i != 2 && !a[i].Equals(b[i])) return false;

        return true;
    }

    public bool _IsModerator(VRCPlayerApi player)
    {
        return Array.IndexOf(moderators, player.displayName) != -1;
    }

    public int _GetPlayerSlot(VRCPlayerApi who, int[] playerlist)
    {
        if (who == null) return -1;

        for (int i = 0; i < 4; i++)
        {
            if (playerlist[i] == who.playerId)
            {
                return i;
            }
        }

        return -1;
    }

    public bool _IsPlayer(VRCPlayerApi who)
    {
        if (who == null) return false;
        if (who.isLocal && localPlayerId >= 0) return true;

        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == who.playerId)
            {
                return true;
            }
        }

        return false;
    }

    private bool stringArrayEquals(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool intArrayEquals(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool vector3ArrayEquals(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
    #endregion

    public void checkDistanceLoop()
    {
        if (checkingDistant)
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), 1f);
        else
            return;

        checkDistanceLoD();
    }

    public void checkDistanceLoD()
    {
        bool nowDistant = (Vector3.Distance(Networking.LocalPlayer.GetPosition(), transform.position) > LoDDistance) && !noLOD
        && !(networkingManager.gameStateSynced == 2 && Networking.IsOwner(networkingManager.gameObject));
        if (nowDistant == localPlayerDistant) { return; }
        if (isPlayer)
        {
            localPlayerDistant = false;
            return;
        }
        else
        {
            localPlayerDistant = nowDistant;
        }
        if (networkingManager.delayedDeserialization)
        {
            networkingManager.OnDeserialization();
        }
        setLOD();
    }

    private void setLOD()
    {
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
        balls[0].transform.parent.gameObject.SetActive(!localPlayerDistant);
        debugger.SetActive(!localPlayerDistant);
        menuManager._RefreshLobby();
        graphicsManager._UpdateLOD();
        auto_pocketblockers.SetActive(true);
    }

    #region Debugger
    const string LOG_LOW = "<color=\"#ADADAD\">";
    const string LOG_ERR = "<color=\"#B84139\">";
    const string LOG_WARN = "<color=\"#DEC521\">";
    const string LOG_YES = "<color=\"#69D128\">";
    const string LOG_END = "</color>";
#if HT8B_DEBUGGER
    public void _Log(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogYes(string msg)
    {
        _log(LOG_YES + msg + LOG_END);
    }
    public void _LogWarn(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogError(string msg)
    {
        _log(LOG_ERR + msg + LOG_END);
    }
    public void _LogInfo(string msg)
    {
        _log(LOG_LOW + msg + LOG_END);
    }
    public void _RedrawDebugger()
    {
        redrawDebugger();
    }
#else
public void _Log(string msg) { }
public void _LogYes(string msg) { }
public void _LogInfo(string msg) { }
public void _LogWarn(string msg) { }
public void _LogError(string msg) { }
public void _RedrawDebugger() { }
#endif

    public void _BeginPerf(int id)
    {
        perfStart[id] = Time.realtimeSinceStartup;
    }

    public void _EndPerf(int id)
    {
        perfTimings[id] += Time.realtimeSinceStartup - perfStart[id];
        perfCounters[id]++;
    }

    private void _log(string ln)
    {
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>] " + ln);

        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule</color>] " + ln + "\n";
        LOG_LEN++;

        if (LOG_PTR >= LOG_MAX)
        {
            LOG_PTR = 0;
        }

        if (LOG_LEN > LOG_MAX)
        {
            LOG_LEN = LOG_MAX;
        }

        redrawDebugger();
    }

    private void redrawDebugger()
    {
        string output = "BilliardsModule ";

        // Add information about game state:
        output += Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject) ?
           "<color=\"#95a2b8\">net(</color> <color=\"#4287F5\">OWNER</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">net(</color> <color=\"#678AC2\">RECVR</color> <color=\"#95a2b8\">)</color> ";

        output += isLocalSimulationRunning ?
           "<color=\"#95a2b8\">sim(</color> <color=\"#4287F5\">ACTIVE</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">sim(</color> <color=\"#678AC2\">PAUSED</color> <color=\"#95a2b8\">)</color> ";

        VRCPlayerApi currentOwner = Networking.GetOwner(networkingManager.gameObject);
        output += "<color=\"#95a2b8\">owner(</color> <color=\"#4287F5\">" + (Utilities.IsValid(currentOwner) ? currentOwner.displayName + ":" + currentOwner.playerId : "[null]") + "/" + teamIdLocal + "</color> <color=\"#95a2b8\">)</color> ";

        if (currentPhysicsManager)
        {
            output += "Physics: " + (string)currentPhysicsManager.GetProgramVariable("PHYSICSNAME");
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        for (int i = 0; i < PERF_MAX; i++)
        {
            output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (perfCounters[i] > 0 ? perfTimings[i] * 1e6 / perfCounters[i] : 0).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
            // to not average them (see values from this frame)
            // requires changing _EndPerf() to be = instead of +=
            // output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (/*perfCounters[i] > 0 ? */ perfTimings[i] * 1e6 /* / perfCounters[i] : 0 */).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        // Update display 
        for (int i = 0; i < LOG_LEN; i++)
        {
            output += LOG_LINES[(LOG_MAX + LOG_PTR - LOG_LEN + i) % LOG_MAX];
        }

        ltext.text = output;
    }
    #endregion
}
