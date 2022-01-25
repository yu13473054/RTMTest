#if UNITY_CHANGE1 || UNITY_CHANGE2 || UNITY_CHANGE3
#warning UNITY_CHANGE has been set manually
#elif UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
#define UNITY_CHANGE1
#elif UNITY_5_0 || UNITY_5_1 || UNITY_5_2
#define UNITY_CHANGE2
#else
#define UNITY_CHANGE3
#endif
//use UNITY_CHANGE1 for unity older than "unity 5"
//use UNITY_CHANGE2 for unity 5.0 -> 5.3 
//use UNITY_CHANGE3 for unity 5.3 (fix for new SceneManger system  )


using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
#if UNITY_CHANGE3
using UnityEngine.SceneManagement;
#endif


[System.Serializable]
public class Images
{

	public Texture2D logImage;
	public Texture2D warningImage;
	public Texture2D errorImage;

	public Texture2D barImage;
	public Texture2D button_activeImage;
	public Texture2D even_logImage;
	public Texture2D odd_logImage;
	public Texture2D selectedImage;

	public GUISkin reporterScrollerSkin;
}

//To use Reporter just create reporter from menu (Reporter->Create) at first scene your game start.
//then set the ” Scrip execution order ” in (Edit -> Project Settings ) of Reporter.cs to be the highest.

//Now to view logs all what you have to do is to make a circle gesture using your mouse (click and drag) 
//or your finger (touch and drag) on the screen to show all these logs
//no coding is required 

public class Reporter : MonoBehaviour
{
    public string UserData = "";
    //frame rate per second
    public float fps;
    public string fpsText;
    public Images images;
    public Vector2 size = new Vector2(32, 32);
    public float maxSize = 20;
    public int numOfCircleToShow = 1;
    //show hide In Game Logs
    private bool _show;
    public bool show 
    {
        get{ return _show; }
        set
        { 
            _show = value;
        }
    }

    private void InitGMTxt()
    {
        gmList = new List<GMInfo>();
        gmList.Add(new GMInfo("  增加钻石：/gm adddiamond 数量", "/gm adddiamond "));
        gmList.Add(new GMInfo("  增加经验：/gm addexp 数量", "/gm addexp "));
        gmList.Add(new GMInfo("  增加勇者：/gm addhero 勇者sn", "/gm addhero "));
        gmList.Add(new GMInfo("  解锁勇者装备：/gm addheroequip 勇者sn 装备sn", "/gm addheroequip "));
        gmList.Add(new GMInfo("  增加道具：/gm additem 道具sn 数量", "/gm additem "));
        gmList.Add(new GMInfo("  增加金钱：/gm addmoney 数量", "/gm addmoney "));
        gmList.Add(new GMInfo("  增加队伍：/gm addteam", "/gm addteam"));
        gmList.Add(new GMInfo("  增加卡牌：/gm addwife 卡牌sn", "/gm addwife "));
        gmList.Add(new GMInfo("  启动冒险：/gm adv 任务sn 1队 2队", "/gm adv "));
        gmList.Add(new GMInfo("  跳过：/gm jump 全部引导或解锁ID", "/gm jump "));
        gmList.Add(new GMInfo("  完成任务：/gm quest 任务sn", "/gm quest "));
        gmList.Add(new GMInfo("  勇者加经验：/gm addheroexp 职业 经验数量", "/gm addheroexp "));
        gmList.Add(new GMInfo("  卡加经验：/gm addwifeexp 卡sn 经验数量", "/gm addwifeexp "));
        gmList.Add(new GMInfo("  发送邮件：/gm sendmail 邮件数量 邮件标题 邮件内容 附件1sn 附件1数量...", "/gm sendmail "));
        gmList.Add(new GMInfo("  使用SDK：/gm sdk 值", "/gm sdk "));
        gmList.Add(new GMInfo("  完成七日任务：/gm sevenstory 完成条件", "/gm sevenstory "));
        gmList.Add(new GMInfo("  重置七日任务：/gm sevenstoryreset", "/gm sevenstoryreset"));
        gmList.Add(new GMInfo("  设置月塔层数：/gm tower 层数sn", "/gm tower "));
        gmList.Add(new GMInfo("  添加boss挑战次数：/gm resetboss", "/gm resetboss"));
        gmList.Add(new GMInfo("  后宅：/gm house 指令", "/gm house "));
    }

    public enum _LogType
	{
		Assert    = LogType.Assert,
		Error     = LogType.Error,
		Exception = LogType.Exception,
		Log       = LogType.Log,
		Warning   = LogType.Warning,
	}

	public class Sample
	{
		public float time;
		public float memory;
		public float fps;
		public string fpsText;
		public static float MemSize()
		{
			float s = sizeof(float) + sizeof(byte) + sizeof(float) + sizeof(float);
			return s;
		}

		public string GetSceneName()
		{
			return "";
		}
	}

	List<Sample> samples = new List<Sample>(60 * 60 * 60);

	public class Log
	{
		public int count = 1;
		public _LogType logType;
		public string condition;
		public string stacktrace;
		public int sampleId;
		//public string   objectName="" ;//object who send error
		//public string   rootName =""; //root of object send error

		public Log CreateCopy()
		{
			return (Log)this.MemberwiseClone();
		}
		public float GetMemoryUsage()
		{
			return (float)(sizeof(int) +
					sizeof(_LogType) +
					condition.Length * sizeof(char) +
					stacktrace.Length * sizeof(char) +
					sizeof(int));
		}
	}
	//contains all uncollapsed log
	List<Log> logs = new List<Log>();
	//contains all collapsed logs
	List<Log> collapsedLogs = new List<Log>();
	//contain logs which should only appear to user , for example if you switch off show logs + switch off show warnings
	//and your mode is collapse,then this list will contains only collapsed errors
	List<Log> currentLog = new List<Log>();

	//used to check if the new coming logs is already exist or new one
	MultiKeyDictionary<string, string, Log> logsDic = new MultiKeyDictionary<string, string, Log>();
	//to save memory
	Dictionary<string, string> cachedString = new Dictionary<string, string>();

	//collapse logs
	bool collapse;
	//to decide if you want to clean logs for new loaded scene
	bool clearOnNewSceneLoaded;

	bool showTime;

	bool showScene;

	bool showMemory;

	bool showFps;

	//show or hide logs
	bool showLog = true;
	//show or hide warnings
	bool showWarning = true;
	//show or hide errors
	bool showError = true;

	//total number of logs
	int numOfLogs = 0;
	//total number of warnings
	int numOfLogsWarning = 0;
	//total number of errors
	int numOfLogsError = 0;
	//total number of collapsed logs
	int numOfCollapsedLogs = 0;
	//total number of collapsed warnings
	int numOfCollapsedLogsWarning = 0;
	//total number of collapsed errors
	int numOfCollapsedLogsError = 0;

	//maximum number of allowed logs to view
	//public int maxAllowedLog = 1000 ;

	bool showClearOnNewSceneLoadedButton = true;
	bool showTimeButton = true;
	bool showSceneButton = true;
	bool showMemButton = true;
	bool showFpsButton = true;
	bool showSearchText = true;

	string logDate;
	float logsMemUsage;
	float graphMemUsage;
	public float TotalMemUsage
	{
		get
		{
			return logsMemUsage + graphMemUsage;
		}
	}
	float gcTotalMemory;

    //List<Texture2D> snapshots = new List<Texture2D>() ;

    enum ReportView
	{
		None,
		Logs,
		Info,
		Snapshot,
        GM,
	}
	ReportView currentView = ReportView.Logs;
	enum DetailView
	{
		None,
		StackTrace,
		Graph,
	}

	//used to check if you have In Game Logs multiple time in different scene
	//only one should work and other should be deleted
	static bool created = false;
	//public delegate void OnLogHandler( string condition, string stack-trace, LogType type );
	//public event OnLogHandler OnLog ;

	// gui
	GUIContent clearContent;
	GUIContent collapseContent;
	GUIContent showTimeContent;
	GUIContent showSceneContent;
	GUIContent userContent;
	GUIContent showMemoryContent;
	GUIContent softwareContent;
	GUIContent dateContent;
	GUIContent showFpsContent;
	//GUIContent graphContent;
	GUIContent infoContent;
	GUIContent searchContent;
	GUIContent closeContent;

	GUIContent systemInfoContent;
	GUIContent graphicsInfoContent;
	GUIContent backContent;
	GUIContent gmContent;
	GUIContent msgContent;

	//GUIContent cameraContent;

	GUIContent logContent;
	GUIContent warningContent;
	GUIContent errorContent;
	GUIStyle barStyle;
	GUIStyle feildStyle;
	GUIStyle textAreaStyle;
	GUIStyle buttonActiveStyle;

	GUIStyle nonStyle, txtStyle;
	GUIStyle lowerLeftFontStyle;
	GUIStyle backStyle;
	GUIStyle evenLogStyle;
	GUIStyle oddLogStyle;
	GUIStyle logButtonStyle;
	GUIStyle selectedLogStyle;
	GUIStyle selectedLogFontStyle;
	GUIStyle stackLabelStyle;
	GUIStyle scrollerStyle;
	GUIStyle searchStyle;
	GUIStyle sliderBackStyle;
	GUIStyle sliderThumbStyle;
	GUISkin toolbarScrollerSkin;
	GUISkin logScrollerSkin;
	GUISkin graphScrollerSkin;

	string currentScene;
	string filterText = "";

	string deviceModel;
	string deviceType;
	string deviceName;
	string graphicsMemorySize;
#if !UNITY_CHANGE1
	string maxTextureSize;
#endif
	string systemMemorySize;

    struct GMInfo
    {
        public string desc;
        public string cmd;

        public GMInfo(string desc, string cmd)
        {
            this.desc = desc;
            this.cmd = cmd;
        }
    }

    private List<GMInfo> gmList;

	void Awake()
	{
		size = new Vector2(Screen.width / 1280f * 40, Screen.width / 1280f * 40);
		if (!Initialized)
			Initialize();
	}

	void OnEnable()
	{
		if (logs.Count == 0)//if recompile while in play mode
			clear();
	}

	void OnDestroy()
	{
	    
        Resources.UnloadAsset(images.logImage);
        Resources.UnloadAsset(images.warningImage);
        Resources.UnloadAsset(images.errorImage);
        Resources.UnloadAsset(images.barImage);
        Resources.UnloadAsset(images.button_activeImage);
        Resources.UnloadAsset(images.even_logImage);
        Resources.UnloadAsset(images.odd_logImage);
        Resources.UnloadAsset(images.selectedImage);
        Resources.UnloadAsset(images.reporterScrollerSkin);


        images.reporterScrollerSkin = null;
        images.logImage = null;
        images.warningImage = null;
        images.errorImage = null;
    
        images.barImage = null;
        images.button_activeImage = null;
        images.even_logImage = null;
        images.odd_logImage = null;
        images.selectedImage = null;

        samples.Clear();
	    samples = null;
        threadedLogs.Clear();
	    threadedLogs = null;

        //Application.logMessageReceived += CaptureLog ;
        Application.logMessageReceivedThreaded -= CaptureLogThread;

	    created = false;
	}

	void addSample()
	{
		Sample sample = new Sample();
		sample.fps = fps;
		sample.fpsText = fpsText;
		sample.time = Time.realtimeSinceStartup;
		sample.memory = gcTotalMemory;
		samples.Add(sample);

		graphMemUsage = (samples.Count * Sample.MemSize()) / 1024 / 1024;
	}

	bool Initialized = false;
	void Initialize()
	{
		if (!created) {
			try {
				gameObject.SendMessage("OnPreStart");
			}
			catch (System.Exception e) {
				Debug.LogException(e);
			}

			currentScene = SceneManager.GetActiveScene().name;

			DontDestroyOnLoad(gameObject);

			//Application.logMessageReceived += CaptureLog ;
			Application.logMessageReceivedThreaded += CaptureLogThread;

			created = true;
			//addSample();
		}
		else {
			Debug.LogWarning("tow manager is exists delete the second");
			DestroyImmediate(gameObject, true);
			return;
		}


		//initialize gui and styles for gui purpose

		clearContent = new GUIContent("清理");
		collapseContent = new GUIContent("折叠");
		showTimeContent = new GUIContent("时间");
		showSceneContent = new GUIContent("场景");
		showMemoryContent = new GUIContent("内存");
		softwareContent = new GUIContent("系统");
		dateContent = new GUIContent("日期");
		showFpsContent = new GUIContent("帧速");
		infoContent = new GUIContent("设置");
		searchContent = new GUIContent("搜索");
		closeContent = new GUIContent("关闭");
		userContent = new GUIContent("用户");

		systemInfoContent = new GUIContent("硬件");
		graphicsInfoContent = new GUIContent("显卡");
		backContent = new GUIContent("返回");
        gmContent = new GUIContent("GM");
        msgContent = new GUIContent("消息");

        //snapshotContent = new GUIContent("",images.cameraImage,"show or hide logs");
        logContent = new GUIContent("", images.logImage, "show or hide logs");
		warningContent = new GUIContent("", images.warningImage, "show or hide warnings");
		errorContent = new GUIContent("", images.errorImage, "show or hide errors");


        currentView = (ReportView)PlayerPrefs.GetInt("Reporter_currentView", 1);
		//show = (PlayerPrefs.GetInt("Reporter_show") == 1) ? true : false;
	    show = false; //默认不显示
		collapse = (PlayerPrefs.GetInt("Reporter_collapse") == 1) ? true : false;
		clearOnNewSceneLoaded = (PlayerPrefs.GetInt("Reporter_clearOnNewSceneLoaded") == 0) ? true : false;
		showTime = (PlayerPrefs.GetInt("Reporter_showTime") == 1) ? true : false;
		showScene = (PlayerPrefs.GetInt("Reporter_showScene") == 1) ? true : false;
		showMemory = (PlayerPrefs.GetInt("Reporter_showMemory") == 1) ? true : false;
		showFps = (PlayerPrefs.GetInt("Reporter_showFps") == 1) ? true : false;
		showLog = (PlayerPrefs.GetInt("Reporter_showLog", 1) == 1) ? true : false;
		showWarning = (PlayerPrefs.GetInt("Reporter_showWarning", 1) == 1) ? true : false;
		showError = (PlayerPrefs.GetInt("Reporter_showError", 1) == 1) ? true : false;
		filterText = PlayerPrefs.GetString("Reporter_filterText");

		showClearOnNewSceneLoadedButton = (PlayerPrefs.GetInt("Reporter_showClearOnNewSceneLoadedButton", 0) == 1) ? true : false;
		showTimeButton = (PlayerPrefs.GetInt("Reporter_showTimeButton", 1) == 1) ? true : false;
		showSceneButton = (PlayerPrefs.GetInt("Reporter_showSceneButton", 0) == 1) ? true : false;
		showMemButton = (PlayerPrefs.GetInt("Reporter_showMemButton", 1) == 1) ? true : false;
		showFpsButton = (PlayerPrefs.GetInt("Reporter_showFpsButton", 1) == 1) ? true : false;
		showSearchText = (PlayerPrefs.GetInt("Reporter_showSearchText", 0) == 1) ? true : false;


		initializeStyle();

	    InitGMTxt();

		Initialized = true;

		if (show) {
			doShow();
		}

		deviceModel = SystemInfo.deviceModel.ToString();
		deviceType = SystemInfo.deviceType.ToString();
		deviceName = SystemInfo.deviceName.ToString();
		graphicsMemorySize = SystemInfo.graphicsMemorySize.ToString();
#if !UNITY_CHANGE1
		maxTextureSize = SystemInfo.maxTextureSize.ToString();
#endif
		systemMemorySize = SystemInfo.systemMemorySize.ToString();

	}

    void initializeStyle()
	{
		int paddingX = (int)(size.x * 0.2f);
		int paddingY = (int)(size.y * 0.2f);
		nonStyle = new GUIStyle();
		nonStyle.clipping = TextClipping.Clip;
		nonStyle.border = new RectOffset(0, 0, 0, 0);
		nonStyle.normal.background = null;
		nonStyle.fontSize = (int)(size.y / 2);
		nonStyle.alignment = TextAnchor.MiddleCenter;

	    txtStyle = new GUIStyle();
	    txtStyle.clipping = TextClipping.Clip;
	    txtStyle.border = new RectOffset(0, 0, 0, 0);
	    txtStyle.normal.background = null;
	    txtStyle.fontSize = (int)(size.y / 2);
	    txtStyle.alignment = TextAnchor.MiddleLeft;

        lowerLeftFontStyle = new GUIStyle();
		lowerLeftFontStyle.clipping = TextClipping.Clip;
		lowerLeftFontStyle.border = new RectOffset(0, 0, 0, 0);
		lowerLeftFontStyle.normal.background = null;
		lowerLeftFontStyle.fontSize = (int)(size.y / 2);
		lowerLeftFontStyle.fontStyle = FontStyle.Bold;
		lowerLeftFontStyle.alignment = TextAnchor.LowerLeft;


		barStyle = new GUIStyle();
		barStyle.border = new RectOffset(1, 1, 1, 1);
		barStyle.normal.background = images.barImage;
		barStyle.active.background = images.button_activeImage;
		barStyle.alignment = TextAnchor.MiddleCenter;
		barStyle.margin = new RectOffset(1, 1, 1, 1);

		//barStyle.padding = new RectOffset(paddingX,paddingX,paddingY,paddingY); 
		//barStyle.wordWrap = true ;
		barStyle.clipping = TextClipping.Clip;
		barStyle.fontSize = (int)(size.y / 2);

        feildStyle = new GUIStyle(images.reporterScrollerSkin.textField);
        feildStyle.fontSize = (int)(size.y / 2);
        feildStyle.fontStyle = FontStyle.Bold;
        feildStyle.alignment = TextAnchor.MiddleLeft;

	    textAreaStyle = new GUIStyle(images.reporterScrollerSkin.textArea);
	    textAreaStyle.fontSize = (int)(size.y / 2);
	    textAreaStyle.fontStyle = FontStyle.Bold;

        buttonActiveStyle = new GUIStyle();
		buttonActiveStyle.border = new RectOffset(1, 1, 1, 1);
		buttonActiveStyle.normal.background = images.button_activeImage;
		buttonActiveStyle.alignment = TextAnchor.MiddleCenter;
		buttonActiveStyle.margin = new RectOffset(1, 1, 1, 1);
		//buttonActiveStyle.padding = new RectOffset(4,4,4,4);
		buttonActiveStyle.fontSize = (int)(size.y / 2);

		backStyle = new GUIStyle();
		backStyle.normal.background = images.even_logImage;
		backStyle.clipping = TextClipping.Clip;
		backStyle.fontSize = (int)(size.y / 2);

		evenLogStyle = new GUIStyle();
		evenLogStyle.normal.background = images.even_logImage;
		evenLogStyle.fixedHeight = size.y;
		evenLogStyle.clipping = TextClipping.Clip;
		evenLogStyle.alignment = TextAnchor.MiddleLeft;
		evenLogStyle.imagePosition = ImagePosition.ImageLeft;
		evenLogStyle.fontSize = (int)(size.y / 2);
		//evenLogStyle.wordWrap = true;

		oddLogStyle = new GUIStyle();
		oddLogStyle.normal.background = images.odd_logImage;
		oddLogStyle.fixedHeight = size.y;
		oddLogStyle.clipping = TextClipping.Clip;
		oddLogStyle.alignment = TextAnchor.MiddleLeft;
		oddLogStyle.imagePosition = ImagePosition.ImageLeft;
		oddLogStyle.fontSize = (int)(size.y / 2);
		//oddLogStyle.wordWrap = true ;

		logButtonStyle = new GUIStyle();
		//logButtonStyle.wordWrap = true;
		logButtonStyle.fixedHeight = size.y;
		logButtonStyle.clipping = TextClipping.Clip;
		logButtonStyle.alignment = TextAnchor.UpperLeft;
		//logButtonStyle.imagePosition = ImagePosition.ImageLeft ;
		//logButtonStyle.wordWrap = true;
		logButtonStyle.fontSize = (int)(size.y / 2);
		logButtonStyle.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);

		selectedLogStyle = new GUIStyle();
		selectedLogStyle.normal.background = images.selectedImage;
		selectedLogStyle.fixedHeight = size.y;
		selectedLogStyle.clipping = TextClipping.Clip;
		selectedLogStyle.alignment = TextAnchor.UpperLeft;
		selectedLogStyle.normal.textColor = Color.white;
		//selectedLogStyle.wordWrap = true;
		selectedLogStyle.fontSize = (int)(size.y / 2);

		selectedLogFontStyle = new GUIStyle();
		selectedLogFontStyle.normal.background = images.selectedImage;
		selectedLogFontStyle.fixedHeight = size.y;
		selectedLogFontStyle.clipping = TextClipping.Clip;
		selectedLogFontStyle.alignment = TextAnchor.UpperLeft;
		selectedLogFontStyle.normal.textColor = Color.white;
		//selectedLogStyle.wordWrap = true;
		selectedLogFontStyle.fontSize = (int)(size.y / 2);
		selectedLogFontStyle.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);

		stackLabelStyle = new GUIStyle();
		stackLabelStyle.wordWrap = true;
		stackLabelStyle.fontSize = (int)(size.y / 2);
		stackLabelStyle.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);

		scrollerStyle = new GUIStyle();
		scrollerStyle.normal.background = images.barImage;

		searchStyle = new GUIStyle();
		searchStyle.clipping = TextClipping.Clip;
		searchStyle.alignment = TextAnchor.LowerCenter;
		searchStyle.fontSize = (int)(size.y / 2);
		searchStyle.wordWrap = true;


		sliderBackStyle = new GUIStyle();
		sliderBackStyle.normal.background = images.barImage;
		sliderBackStyle.fixedHeight = size.y;
		sliderBackStyle.border = new RectOffset(1, 1, 1, 1);

		sliderThumbStyle = new GUIStyle();
		sliderThumbStyle.normal.background = images.selectedImage;
		sliderThumbStyle.fixedWidth = size.x;

		GUISkin skin = images.reporterScrollerSkin;

		toolbarScrollerSkin = (GUISkin)GameObject.Instantiate(skin);
		toolbarScrollerSkin.verticalScrollbar.fixedWidth = 0f;
		toolbarScrollerSkin.horizontalScrollbar.fixedHeight = 0f;
		toolbarScrollerSkin.verticalScrollbarThumb.fixedWidth = 0f;
		toolbarScrollerSkin.horizontalScrollbarThumb.fixedHeight = 0f;

		logScrollerSkin = (GUISkin)GameObject.Instantiate(skin);
		logScrollerSkin.verticalScrollbar.fixedWidth = size.x * 2f;
		logScrollerSkin.horizontalScrollbar.fixedHeight = 0f;
		logScrollerSkin.verticalScrollbarThumb.fixedWidth = size.x * 2f;
		logScrollerSkin.horizontalScrollbarThumb.fixedHeight = 0f;

		graphScrollerSkin = (GUISkin)GameObject.Instantiate(skin);
		graphScrollerSkin.verticalScrollbar.fixedWidth = 0f;
		graphScrollerSkin.horizontalScrollbar.fixedHeight = size.x * 2f;
		graphScrollerSkin.verticalScrollbarThumb.fixedWidth = 0f;
		graphScrollerSkin.horizontalScrollbarThumb.fixedHeight = size.x * 2f;
		//inGameLogsScrollerSkin.verticalScrollbarThumb.fixedWidth = size.x * 2;
		//inGameLogsScrollerSkin.verticalScrollbar.fixedWidth = size.x * 2;
	}

	void Start()
	{
		logDate = System.DateTime.Now.ToString();
	}

	//clear all logs
	void clear()
	{
		logs.Clear();
		collapsedLogs.Clear();
		currentLog.Clear();
		logsDic.Clear();
		//selectedIndex = -1;
		selectedLog = null;
		selectMsg = -1;
		numOfLogs = 0;
		numOfLogsWarning = 0;
		numOfLogsError = 0;
		numOfCollapsedLogs = 0;
		numOfCollapsedLogsWarning = 0;
		numOfCollapsedLogsError = 0;
		logsMemUsage = 0;
		graphMemUsage = 0;
		samples.Clear();
		System.GC.Collect();
	}

	Rect screenRect = new Rect();
	Rect toolBarRect = new Rect();
	Rect logsRect = new Rect();
	Rect stackRect = new Rect();
	Rect graphRect = new Rect();
	Rect graphMinRect = new Rect();
	Rect graphMaxRect = new Rect();
	Rect buttomRect = new Rect();
	Vector2 stackRectTopLeft;
	Rect detailRect = new Rect();

	Vector2 scrollPosition;
	Vector2 scrollPosition2;
	Vector2 toolbarScrollPosition;

	//int 	selectedIndex = -1;
	Log selectedLog;
    private int selectMsg = -1;

	float toolbarOldDrag = 0;
	float oldDrag;
	float oldDrag2;
	float oldDrag3;
	int startIndex;

	//calculate what is the currentLog : collapsed or not , hide or view warnings ......
	void calculateCurrentLog()
	{
		bool filter = !string.IsNullOrEmpty(filterText);
		string _filterText = "";
		if (filter)
			_filterText = filterText.ToLower();
		currentLog.Clear();
		if (collapse) {
			for (int i = 0; i < collapsedLogs.Count; i++) {
				Log log = collapsedLogs[i];
				if (log.logType == _LogType.Log && !showLog)
					continue;
				if (log.logType == _LogType.Warning && !showWarning)
					continue;
				if (log.logType == _LogType.Error && !showError)
					continue;
				if (log.logType == _LogType.Assert && !showError)
					continue;
				if (log.logType == _LogType.Exception && !showError)
					continue;

				if (filter) {
					if (log.condition.ToLower().Contains(_filterText))
						currentLog.Add(log);
				}
				else {
					currentLog.Add(log);
				}
			}
		}
		else {
			for (int i = 0; i < logs.Count; i++) {
				Log log = logs[i];
				if (log.logType == _LogType.Log && !showLog)
					continue;
				if (log.logType == _LogType.Warning && !showWarning)
					continue;
				if (log.logType == _LogType.Error && !showError)
					continue;
				if (log.logType == _LogType.Assert && !showError)
					continue;
				if (log.logType == _LogType.Exception && !showError)
					continue;

				if (filter) {
					if (log.condition.ToLower().Contains(_filterText))
						currentLog.Add(log);
				}
				else {
					currentLog.Add(log);
				}
			}
		}

		if (selectedLog != null) {
			int newSelectedIndex = currentLog.IndexOf(selectedLog);
			if (newSelectedIndex == -1) {
				Log collapsedSelected = logsDic[selectedLog.condition][selectedLog.stacktrace];
				newSelectedIndex = currentLog.IndexOf(collapsedSelected);
				if (newSelectedIndex != -1)
					scrollPosition.y = newSelectedIndex * size.y;
			}
			else {
				scrollPosition.y = newSelectedIndex * size.y;
			}
		}
	}

	Rect countRect = new Rect();
	Rect timeRect = new Rect();
	Rect timeLabelRect = new Rect();
	Rect sceneRect = new Rect();
	Rect memoryRect = new Rect();
	Rect memoryLabelRect = new Rect();
	Rect fpsRect = new Rect();
	Rect fpsLabelRect = new Rect();

	GUIContent tempContent = new GUIContent();


	Vector2 infoScrollPosition;
	Vector2 oldInfoDrag;
	void DrawInfo()
	{
		GUILayout.BeginArea(screenRect, backStyle);

		Vector2 drag = getDrag();
		if ((drag.x != 0) && (downPos != Vector2.zero)) {
			infoScrollPosition.x -= (drag.x - oldInfoDrag.x);
		}
		if ((drag.y != 0) && (downPos != Vector2.zero)) {
			infoScrollPosition.y += (drag.y - oldInfoDrag.y);
		}
		oldInfoDrag = drag;

		GUI.skin = toolbarScrollerSkin;
		infoScrollPosition = GUILayout.BeginScrollView(infoScrollPosition);
		GUILayout.Space(size.x);
		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Space(size.x);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(systemInfoContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(deviceModel, nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(deviceType, nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(deviceName, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(graphicsInfoContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(SystemInfo.graphicsDeviceName, nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(graphicsMemorySize, nonStyle, GUILayout.Height(size.y));
#if !UNITY_CHANGE1
		GUILayout.Space(size.x);
		GUILayout.Label(maxTextureSize, nonStyle, GUILayout.Height(size.y));
#endif
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Space(size.x);
		GUILayout.Space(size.x);
		GUILayout.Label("Screen Width " + Screen.width, nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label("Screen Height " + Screen.height, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(showMemoryContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(systemMemorySize + " mb", nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Space(size.x);
		GUILayout.Space(size.x);
		GUILayout.Label("Mem Usage Of Logs " + logsMemUsage.ToString("0.000") + " mb", nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		//GUILayout.Label( "Mem Usage Of Graph " + graphMemUsage.ToString("0.000")  + " mb", nonStyle , GUILayout.Height(size.y));
		//GUILayout.Space( size.x);
		GUILayout.Label("GC Memory " + gcTotalMemory.ToString("0.000") + " mb", nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(softwareContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(SystemInfo.operatingSystem, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();


		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(dateContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(System.DateTime.Now.ToString(), nonStyle, GUILayout.Height(size.y));
		GUILayout.Label(" - Application Started At " + logDate, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(showTimeContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(Time.realtimeSinceStartup.ToString("000"), nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(showFpsContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(fpsText, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(userContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(UserData, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(showSceneContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label(currentScene, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Box(showSceneContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.Label("Unity Version = " + Application.unityVersion, nonStyle, GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		/*GUILayout.BeginHorizontal();
		GUILayout.Space( size.x);
		GUILayout.Box( graphContent ,nonStyle ,  GUILayout.Width(size.x) , GUILayout.Height(size.y));
		GUILayout.Space( size.x);
		GUILayout.Label( "frame " + samples.Count , nonStyle , GUILayout.Height(size.y));
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();*/

		drawInfo_enableDisableToolBarButtons();

		GUILayout.FlexibleSpace();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Label("Size = " + size.x.ToString("0.0"), nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		float _size = GUILayout.HorizontalSlider(size.x, 16, 64, sliderBackStyle, sliderThumbStyle, GUILayout.Width(Screen.width * 0.5f));
		if (size.x != _size) {
			size.x = size.y = _size;
			initializeStyle();
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		if (GUILayout.Button(backContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)))
		{
            currentView = ReportView.Logs;
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();



		GUILayout.EndScrollView();

		GUILayout.EndArea();
	}

    Vector2 gmScrollPosition;
    Vector2 oldGmDrag;
    private string _inputText = "/gm ";
    void DrawGM()
    {
        GUILayout.BeginArea(screenRect, backStyle);

        Vector2 drag = getDrag();
        if ((drag.x != 0) && (downPos != Vector2.zero))
        {
            gmScrollPosition.x -= (drag.x - oldGmDrag.x);
        }
        if ((drag.y != 0) && (downPos != Vector2.zero))
        {
            gmScrollPosition.y += (drag.y - oldGmDrag.y);
        }
        oldGmDrag = drag;

        GUI.skin = toolbarScrollerSkin;
        gmScrollPosition = GUILayout.BeginScrollView(gmScrollPosition);
        GUILayout.Space(size.x);
        GUILayout.BeginHorizontal();
        GUILayout.Space(size.x);
        GUILayout.Box("GM命令", nonStyle, GUILayout.Height(size.y));
        _inputText = GUILayout.TextField(_inputText, feildStyle, GUILayout.Width(size.x * 10), GUILayout.Height(size.y));
        GUILayout.Space(size.x);
        if (GUILayout.Button("发送", barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y)))
        {
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(size.y);
        GUILayout.BeginVertical(GUILayout.Height(logsRect.height));
        if (drag.y != 0 && logsRect.Contains(new Vector2(downPos.x, Screen.height - downPos.y)))
        {
            scrollPosition.y += (drag.y - oldDrag);
        }
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        oldDrag = drag.y;
        for (int i = 0; i < gmList.Count; i+=2)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < 2; j++)
            {
                if(i+j>=gmList.Count) break;
                if (GUILayout.Button(gmList[i+j].desc, barStyle, GUILayout.MaxWidth(400), GUILayout.MaxHeight(size.y)))
                {
                    _inputText = gmList[i + j].cmd;

                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.Space(size.x);

        GUILayout.BeginHorizontal();
        GUILayout.Space(size.x);
        if (GUILayout.Button(backContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)))
        {
            currentView = ReportView.Logs;
        }
        if (GUILayout.Button(closeContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)))
        {
            show = false;

            try
            {
                gameObject.SendMessage("OnHideReporter");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }


    void drawInfo_enableDisableToolBarButtons()
	{
		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
		GUILayout.Label("Hide or Show tool bar buttons", nonStyle, GUILayout.Height(size.y));
		GUILayout.Space(size.x);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Space(size.x);
        
        // not need
		//if (GUILayout.Button(clearOnNewSceneContent, (showClearOnNewSceneLoadedButton) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
		//	showClearOnNewSceneLoadedButton = !showClearOnNewSceneLoadedButton;
		//}

		if (GUILayout.Button(showTimeContent, (showTimeButton) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
			showTimeButton = !showTimeButton;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label(tempRect, Time.realtimeSinceStartup.ToString("0.0"), lowerLeftFontStyle);
		if (GUILayout.Button(showSceneContent, (showSceneButton) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
			showSceneButton = !showSceneButton;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label(tempRect, currentScene, lowerLeftFontStyle);
		if (GUILayout.Button(showMemoryContent, (showMemButton) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
			showMemButton = !showMemButton;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label(tempRect, gcTotalMemory.ToString("0.0"), lowerLeftFontStyle);

		if (GUILayout.Button(showFpsContent, (showFpsButton) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
			showFpsButton = !showFpsButton;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label(tempRect, fpsText, lowerLeftFontStyle);
        /*if( GUILayout.Button( graphContent , (showGraph)?buttonActiveStyle:barStyle , GUILayout.Width(size.x*2) ,GUILayout.Height(size.y*2)))
		{
			showGraph = !showGraph ;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label( tempRect , samples.Count.ToString() , lowerLeftFontStyle );
		if (GUILayout.Button(searchContent, (showSearchText) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
			showSearchText = !showSearchText;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.TextField(tempRect, filterText, searchStyle);*/

        if (GUILayout.Button("目标帧", buttonActiveStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)))
        {
            if (Application.targetFrameRate == -1)
            {
                Application.targetFrameRate = 30;
            }
            else if (Application.targetFrameRate == 30)
            {
                Application.targetFrameRate = 45;
            }
            else if (Application.targetFrameRate == 45)
            {
                Application.targetFrameRate = 60;
            }
            else if (Application.targetFrameRate == 60)
            {
                Application.targetFrameRate = -1;
            }
        }
        tempRect = GUILayoutUtility.GetLastRect();
        GUI.Label(tempRect, Application.targetFrameRate.ToString(), lowerLeftFontStyle);

        GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
	}

    private int _frameCountInDragging = 0;
	void drawToolBar()
	{

		toolBarRect.x = 0f;
		toolBarRect.y = 0f;
		toolBarRect.width = Screen.width;
		toolBarRect.height = size.y * 2f;

		//toolbarScrollerSkin.verticalScrollbar.fixedWidth = 0f;
		//toolbarScrollerSkin.horizontalScrollbar.fixedHeight= 0f  ;

		GUI.skin = toolbarScrollerSkin;
		Vector2 drag = getDrag();
		if ((drag.x != 0) && (downPos != Vector2.zero) && (downPos.y > Screen.height - size.y * 2f)) {
			toolbarScrollPosition.x -= (drag.x - toolbarOldDrag);
		    _frameCountInDragging = Time.frameCount;

		}
		toolbarOldDrag = drag.x;
		GUILayout.BeginArea(toolBarRect);
		toolbarScrollPosition = GUILayout.BeginScrollView(toolbarScrollPosition);
		GUILayout.BeginHorizontal(barStyle);

        //如果是拖动动作，就不响应点击事件
	    bool isInDragging = (_frameCountInDragging == Time.frameCount - 1);

		if (GUILayout.Button(clearContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			clear();
        }
		if (GUILayout.Button(collapseContent, (collapse) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			collapse = !collapse;
			calculateCurrentLog();
		}
        // not need
		//if (showClearOnNewSceneLoadedButton && GUILayout.Button(clearOnNewSceneContent, (clearOnNewSceneLoaded) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
		//	clearOnNewSceneLoaded = !clearOnNewSceneLoaded;
		//}

		//if (showTimeButton && GUILayout.Button(showTimeContent, (showTime) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
		//	showTime = !showTime;
		//}
  //      tempRect = GUILayoutUtility.GetLastRect();
  //      GUI.Label(tempRect, Time.realtimeSinceStartup.ToString("0.0"), lowerLeftFontStyle);

  //      if (showSceneButton) {
		//	tempRect = GUILayoutUtility.GetLastRect();
		//	GUI.Label(tempRect, Time.realtimeSinceStartup.ToString("0.0"), lowerLeftFontStyle);
		//	if (GUILayout.Button(showSceneContent, (showScene) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2))) {
		//		showScene = !showScene;
		//	}
		//	tempRect = GUILayoutUtility.GetLastRect();
		//	GUI.Label(tempRect, currentScene, lowerLeftFontStyle);
		//}
		//if (showMemButton) {
		//	if (GUILayout.Button(showMemoryContent, (showMemory) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
		//		showMemory = !showMemory;
		//	}
		//	tempRect = GUILayoutUtility.GetLastRect();
		//	GUI.Label(tempRect, gcTotalMemory.ToString("0.0"), lowerLeftFontStyle);
		//}
		//if (showFpsButton) {
		//	if (GUILayout.Button(showFpsContent, (showFps) ? buttonActiveStyle : barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
		//		showFps = !showFps;
		//	}
		//	tempRect = GUILayoutUtility.GetLastRect();
		//	GUI.Label(tempRect, fpsText, lowerLeftFontStyle);
		//}
		/*if( GUILayout.Button( graphContent , (showGraph)?buttonActiveStyle:barStyle , GUILayout.Width(size.x*2) ,GUILayout.Height(size.y*2)))
		{
			showGraph = !showGraph ;
		}
		tempRect = GUILayoutUtility.GetLastRect();
		GUI.Label( tempRect , samples.Count.ToString() , lowerLeftFontStyle );*/

		if (showSearchText) {
			GUILayout.Box(searchContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2));
			tempRect = GUILayoutUtility.GetLastRect();
			string newFilterText = GUI.TextField(tempRect, filterText, searchStyle);
			if (newFilterText != filterText) {
				filterText = newFilterText;
				calculateCurrentLog();
			}
		}

		if (GUILayout.Button(infoContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			currentView = ReportView.Info;
		}
        if (GUILayout.Button(gmContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging)
        {
            currentView = ReportView.GM;
        }


        GUILayout.FlexibleSpace();


		string logsText = " ";
		if (collapse) {
			logsText += numOfCollapsedLogs;
		}
		else {
			logsText += numOfLogs;
		}
		string logsWarningText = " ";
		if (collapse) {
			logsWarningText += numOfCollapsedLogsWarning;
		}
		else {
			logsWarningText += numOfLogsWarning;
		}
		string logsErrorText = " ";
		if (collapse) {
			logsErrorText += numOfCollapsedLogsError;
		}
		else {
			logsErrorText += numOfLogsError;
		}

		GUILayout.BeginHorizontal((showLog) ? buttonActiveStyle : barStyle);
		if (GUILayout.Button(logContent, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showLog = !showLog;
			calculateCurrentLog();
		}
		if (GUILayout.Button(logsText, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showLog = !showLog;
			calculateCurrentLog();
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal((showWarning) ? buttonActiveStyle : barStyle);
		if (GUILayout.Button(warningContent, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showWarning = !showWarning;
			calculateCurrentLog();
		}
		if (GUILayout.Button(logsWarningText, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showWarning = !showWarning;
			calculateCurrentLog();
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal((showError) ? buttonActiveStyle : nonStyle);
		if (GUILayout.Button(errorContent, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showError = !showError;
			calculateCurrentLog();
		}
		if (GUILayout.Button(logsErrorText, nonStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			showError = !showError;
			calculateCurrentLog();
		}
		GUILayout.EndHorizontal();

		if (GUILayout.Button(closeContent, barStyle, GUILayout.Width(size.x * 2), GUILayout.Height(size.y * 2)) && !isInDragging) {
			show = false;

			try {
				gameObject.SendMessage("OnHideReporter");
			}
			catch (System.Exception e) {
				Debug.LogException(e);
			}
		}


		GUILayout.EndHorizontal();

		GUILayout.EndScrollView();

		GUILayout.EndArea();
	}
    
	Rect tempRect;
	void DrawLogs()
	{

		GUILayout.BeginArea(logsRect, backStyle);

		GUI.skin = logScrollerSkin;
		//setStartPos();
		Vector2 drag = getDrag();

		if (drag.y != 0 && logsRect.Contains(new Vector2(downPos.x, Screen.height - downPos.y))) {
			scrollPosition.y += (drag.y - oldDrag);
		}
		scrollPosition = GUILayout.BeginScrollView(scrollPosition);

		oldDrag = drag.y;


		int totalVisibleCount = (int)(Screen.height * 0.75f / size.y);
		int totalCount = currentLog.Count;
		/*if( totalCount < 100 )
			inGameLogsScrollerSkin.verticalScrollbarThumb.fixedHeight = 0;
		else 
			inGameLogsScrollerSkin.verticalScrollbarThumb.fixedHeight = 64;*/

		totalVisibleCount = Mathf.Min(totalVisibleCount, totalCount - startIndex);
		int index = 0;
		int beforeHeight = (int)(startIndex * size.y);
		//selectedIndex = Mathf.Clamp( selectedIndex , -1 , totalCount -1);
		if (beforeHeight > 0) {
			//fill invisible gap before scroller to make proper scroller pos
			GUILayout.BeginHorizontal(GUILayout.Height(beforeHeight));
			GUILayout.Label("---");
			GUILayout.EndHorizontal();
		}

		int endIndex = startIndex + totalVisibleCount;
		endIndex = Mathf.Clamp(endIndex, 0, totalCount);
		bool scrollerVisible = (totalVisibleCount < totalCount);
		for (int i = startIndex; (startIndex + index) < endIndex; i++) {

			if (i >= currentLog.Count)
				break;
			Log log = currentLog[i];

			if (log.logType == _LogType.Log && !showLog)
				continue;
			if (log.logType == _LogType.Warning && !showWarning)
				continue;
			if (log.logType == _LogType.Error && !showError)
				continue;
			if (log.logType == _LogType.Assert && !showError)
				continue;
			if (log.logType == _LogType.Exception && !showError)
				continue;

			if (index >= totalVisibleCount) {
				break;
			}

			GUIContent content = null;
			if (log.logType == _LogType.Log)
				content = logContent;
			else if (log.logType == _LogType.Warning)
				content = warningContent;
			else
				content = errorContent;
			//content.text = log.condition ;

			GUIStyle currentLogStyle = ((startIndex + index) % 2 == 0) ? evenLogStyle : oddLogStyle;
			if (log == selectedLog) {
				//selectedLog = log ;
				currentLogStyle = selectedLogStyle;
			}
			else {
			}

			tempContent.text = log.count.ToString();
			float w = 0f;
			if (collapse)
				w = barStyle.CalcSize(tempContent).x + 3;
			countRect.x = Screen.width - w;
			countRect.y = size.y * i;
			if (beforeHeight > 0)
				countRect.y += 8;//i will check later why
			countRect.width = w;
			countRect.height = size.y;

			if (scrollerVisible)
				countRect.x -= size.x * 2;

			Sample sample = samples[log.sampleId];
			fpsRect = countRect;
			if (showFps) {
				tempContent.text = sample.fpsText;
				w = currentLogStyle.CalcSize(tempContent).x + size.x;
				fpsRect.x -= w;
				fpsRect.width = size.x;
				fpsLabelRect = fpsRect;
				fpsLabelRect.x += size.x;
				fpsLabelRect.width = w - size.x;
			}


			memoryRect = fpsRect;
			if (showMemory) {
				tempContent.text = sample.memory.ToString("0.000");
				w = currentLogStyle.CalcSize(tempContent).x + size.x;
				memoryRect.x -= w;
				memoryRect.width = size.x;
				memoryLabelRect = memoryRect;
				memoryLabelRect.x += size.x;
				memoryLabelRect.width = w - size.x;
			}
			sceneRect = memoryRect;
			timeRect = sceneRect;
			if (showTime) {
				tempContent.text = sample.time.ToString("0.000");
				w = currentLogStyle.CalcSize(tempContent).x + size.x;
				timeRect.x -= w;
				timeRect.width = size.x;
				timeLabelRect = timeRect;
				timeLabelRect.x += size.x;
				timeLabelRect.width = w - size.x;
			}



			GUILayout.BeginHorizontal(currentLogStyle);
			if (log == selectedLog) {
				GUILayout.Box(content, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
				GUILayout.Label(log.condition, selectedLogFontStyle);
				//GUILayout.FlexibleSpace();
				if (showTime) {
					GUI.Box(timeRect, showTimeContent, currentLogStyle);
					GUI.Label(timeLabelRect, sample.time.ToString("0.000"), currentLogStyle);
				}
				if (showScene) {
					
				}
				if (showMemory) {
					GUI.Box(memoryRect, showMemoryContent, currentLogStyle);
					GUI.Label(memoryLabelRect, sample.memory.ToString("0.000") + " mb", currentLogStyle);
				}
				if (showFps) {
					GUI.Box(fpsRect, showFpsContent, currentLogStyle);
					GUI.Label(fpsLabelRect, sample.fpsText, currentLogStyle);
				}


			}
			else {
				if (GUILayout.Button(content, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y))) {
					//selectedIndex = startIndex + index ;
					selectedLog = log;
				}
				if (GUILayout.Button(log.condition, logButtonStyle)) {
					//selectedIndex = startIndex + index ;
					selectedLog = log;
				}
				//GUILayout.FlexibleSpace();
				if (showTime) {
					GUI.Box(timeRect, showTimeContent, currentLogStyle);
					GUI.Label(timeLabelRect, sample.time.ToString("0.000"), currentLogStyle);
				}
				if (showScene) {
					
				}
				if (showMemory) {
					GUI.Box(memoryRect, showMemoryContent, currentLogStyle);
					GUI.Label(memoryLabelRect, sample.memory.ToString("0.000") + " mb", currentLogStyle);
				}
				if (showFps) {
					GUI.Box(fpsRect, showFpsContent, currentLogStyle);
					GUI.Label(fpsLabelRect, sample.fpsText, currentLogStyle);
				}
			}
			if (collapse)
				GUI.Label(countRect, log.count.ToString(), barStyle);
			GUILayout.EndHorizontal();
			index++;
		}

		int afterHeight = (int)((totalCount - (startIndex + totalVisibleCount)) * size.y);
		if (afterHeight > 0) {
			//fill invisible gap after scroller to make proper scroller pos
			GUILayout.BeginHorizontal(GUILayout.Height(afterHeight));
			GUILayout.Label(" ");
			GUILayout.EndHorizontal();
		}

		GUILayout.EndScrollView();
		GUILayout.EndArea();

		buttomRect.x = 0f;
		buttomRect.y = Screen.height - size.y;
		buttomRect.width = Screen.width;
		buttomRect.height = size.y;

		drawStack();
	}


	float graphSize = 4f;
	int startFrame = 0;
	int currentFrame = 0;
	Vector3 tempVector1;
	Vector3 tempVector2;
	Vector2 graphScrollerPos;
	float maxFpsValue;
	float minFpsValue;
	float maxMemoryValue;
	float minMemoryValue;

	void drawStack()
	{

		if (selectedLog != null) {
			Vector2 drag = getDrag();
			if (drag.y != 0 && stackRect.Contains(new Vector2(downPos.x, Screen.height - downPos.y))) {
				scrollPosition2.y += drag.y - oldDrag2;
			}
			oldDrag2 = drag.y;



			GUILayout.BeginArea(stackRect, backStyle);
			scrollPosition2 = GUILayout.BeginScrollView(scrollPosition2);
			Sample selectedSample = null;
			try {
				selectedSample = samples[selectedLog.sampleId];
			}
			catch (System.Exception e) {
				Debug.LogException(e);
			}

			GUILayout.BeginHorizontal();
			GUILayout.Label(selectedLog.condition, stackLabelStyle);
			GUILayout.EndHorizontal();
			GUILayout.Space(size.y * 0.25f);
			GUILayout.BeginHorizontal();
			GUILayout.Label(selectedLog.stacktrace, stackLabelStyle);
			GUILayout.EndHorizontal();
			GUILayout.Space(size.y);
			GUILayout.EndScrollView();
			GUILayout.EndArea();


			GUILayout.BeginArea(buttomRect, backStyle);
			GUILayout.BeginHorizontal();

			GUILayout.Box(showTimeContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
			GUILayout.Label(selectedSample.time.ToString("0.000"), nonStyle);
			GUILayout.Space(size.x);

			GUILayout.Box(showSceneContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
			GUILayout.Label(selectedSample.GetSceneName(), nonStyle);
			GUILayout.Space(size.x);

			GUILayout.Box(showMemoryContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
			GUILayout.Label(selectedSample.memory.ToString("0.000"), nonStyle);
			GUILayout.Space(size.x);

			GUILayout.Box(showFpsContent, nonStyle, GUILayout.Width(size.x), GUILayout.Height(size.y));
			GUILayout.Label(selectedSample.fpsText, nonStyle);
			/*GUILayout.Space( size.x );
			GUILayout.Box( graphContent ,nonStyle, GUILayout.Width(size.x) ,GUILayout.Height(size.y));
			GUILayout.Label( selectedLog.sampleId.ToString() ,nonStyle  );*/
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();



		}
		else {
			GUILayout.BeginArea(stackRect, backStyle);
			GUILayout.EndArea();
			GUILayout.BeginArea(buttomRect, backStyle);
			GUILayout.EndArea();
		}

	}

    Vector2 msgScrollPosition;
    Vector2 msgScrollPosition2;
    private string editStr, targetMsgID;
    private bool lockOn;
    private int msgStartIndex;

    void OnGUI()
	{

		if (!show) {
			return;
		}

		screenRect.x = 0;
		screenRect.y = 0;
		screenRect.width = Screen.width;
		screenRect.height = Screen.height;

		getDownPos();
        logsRect.x = 0f;
		logsRect.y = size.y * 3f;
		logsRect.width = Screen.width;
		logsRect.height = Screen.height * 0.75f - size.y * 3f;

		stackRectTopLeft.x = 0f;
		stackRect.x = 0f;
		stackRectTopLeft.y = Screen.height * 0.75f;
		stackRect.y = Screen.height * 0.75f;
		stackRect.width = Screen.width;
		stackRect.height = Screen.height * 0.25f - size.y;

		detailRect.x = 0f;
		detailRect.y = Screen.height - size.y * 3;
		detailRect.width = Screen.width;
		detailRect.height = size.y * 3;

		if (currentView == ReportView.Info)
			DrawInfo();
		else if (currentView == ReportView.Logs)
		{
		    drawToolBar();
		    DrawLogs();
		}
        else if (currentView == ReportView.GM)
        {
            DrawGM();
        }


    }

	List<Vector2> gestureDetector = new List<Vector2>();
	Vector2 gestureSum = Vector2.zero;
	float gestureLength = 0;
	int gestureCount = 0;
    Rect _validRect = new Rect(0, 0, Screen.width / 3.0f, Screen.height / 3.0f);  //只在这个区域有效
	bool isGestureDone()
	{
		if (Application.platform == RuntimePlatform.Android ||
			Application.platform == RuntimePlatform.IPhonePlayer) {
			if (Input.touches.Length != 1) {
				gestureDetector.Clear();
				gestureCount = 0;
			}
			else {
				if (Input.touches[0].phase == TouchPhase.Canceled || Input.touches[0].phase == TouchPhase.Ended)
					gestureDetector.Clear();
				else if (Input.touches[0].phase == TouchPhase.Moved) {
					Vector2 p = Input.touches[0].position;

				    if (_validRect.Contains(p))
				    {
                        if (gestureDetector.Count == 0 || (p - gestureDetector[gestureDetector.Count - 1]).magnitude > 10)
                            gestureDetector.Add(p);
                    }
				}
			}
		}
		else {
			if (Input.GetMouseButtonUp(0)) {
				gestureDetector.Clear();
				gestureCount = 0;
			}
			else {
				if (Input.GetMouseButton(0)) {
					Vector2 p = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
				    if (_validRect.Contains(p))
				    {
                        if (gestureDetector.Count == 0 || (p - gestureDetector[gestureDetector.Count - 1]).magnitude > 10)
                            gestureDetector.Add(p);
                    }
                        
				}
			}
		}

		if (gestureDetector.Count < 10)
			return false;

		gestureSum = Vector2.zero;
		gestureLength = 0;
		Vector2 prevDelta = Vector2.zero;
		for (int i = 0; i < gestureDetector.Count - 2; i++) {

			Vector2 delta = gestureDetector[i + 1] - gestureDetector[i];
			float deltaLength = delta.magnitude;
			gestureSum += delta;
			gestureLength += deltaLength;

			float dot = Vector2.Dot(delta, prevDelta);
			if (dot < 0f) {
				gestureDetector.Clear();
				gestureCount = 0;
				return false;
			}

			prevDelta = delta;
		}

		int gestureBase = (Screen.width + Screen.height) / 4;

		if (gestureLength > gestureBase && gestureSum.magnitude < gestureBase / 2) {
			gestureDetector.Clear();
			gestureCount++;
			if (gestureCount >= numOfCircleToShow)
				return true;
		}

		return false;
	}

	float lastClickTime = -1;
	//calculate  pos of first click on screen
	Vector2 startPos;

	Vector2 downPos;
	Vector2 getDownPos()
	{
		if (Application.platform == RuntimePlatform.Android ||
		   Application.platform == RuntimePlatform.IPhonePlayer) {

			if (Input.touches.Length == 1 && Input.touches[0].phase == TouchPhase.Began) {
				downPos = Input.touches[0].position;
				return downPos;
			}
		}
		else {
			if (Input.GetMouseButtonDown(0)) {
				downPos.x = Input.mousePosition.x;
				downPos.y = Input.mousePosition.y;
				return downPos;
			}
		}

		return Vector2.zero;
	}
	//calculate drag amount , this is used for scrolling

	Vector2 mousePosition;
	Vector2 getDrag()
	{

		if (Application.platform == RuntimePlatform.Android ||
			Application.platform == RuntimePlatform.IPhonePlayer) {
			if (Input.touches.Length != 1) {
				return Vector2.zero;
			}
			return Input.touches[0].position - downPos;
		}
		else {
			if (Input.GetMouseButton(0)) {
				mousePosition = Input.mousePosition;
				return mousePosition - downPos;
			}
			else {
				return Vector2.zero;
			}
		}
	}

	//calculate the start index of visible log
	void calculateStartIndex()
	{
		    startIndex = (int)(scrollPosition.y / size.y);
		    startIndex = Mathf.Clamp(startIndex, 0, currentLog.Count);
	}

	// For FPS Counter
	private int frames = 0;
	private bool firstTime = true;
	private float lastUpdate = 0f;
	private const int requiredFrames = 10;
	private const float updateInterval = 0.25f;

#if UNITY_CHANGE1
	float lastUpdate2 = 0;
#endif

	void doShow()
	{
		show = true;
        fpsText = fps.ToString("0.00");

		try {
			gameObject.SendMessage("OnShowReporter");
		}
		catch (System.Exception e) {
			Debug.LogException(e);
		}
	}

	void Update()
	{
        UnityEngine.Profiling.Profiler.BeginSample("Reporter.Update.1");
		gcTotalMemory = (((float)System.GC.GetTotalMemory(false)) / 1024 / 1024);
        //addSample();
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Reporter.Update.2");
        calculateStartIndex();
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Reporter.Update.3");
        if (!show && isGestureDone()) {
            doShow();
		}
        UnityEngine.Profiling.Profiler.EndSample();


        UnityEngine.Profiling.Profiler.BeginSample("Reporter.Update.4");
        if (threadedLogs.Count > 0) {
			lock (threadedLogs) {
				for (int i = 0; i < threadedLogs.Count; i++) {
					Log l = threadedLogs[i];
					AddLog(l.condition, l.stacktrace, (LogType)l.logType);
				}
				threadedLogs.Clear();
			}
		}
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Reporter.Update.5");
        // FPS Counter
        if (firstTime) {
			firstTime = false;
			lastUpdate = Time.realtimeSinceStartup;
			frames = 0;
			return;
		}
		frames++;
		float dt = Time.realtimeSinceStartup - lastUpdate;
		if (dt > updateInterval && frames > requiredFrames) {
			fps = (float)frames / dt;
			lastUpdate = Time.realtimeSinceStartup;
			frames = 0;
		}
        UnityEngine.Profiling.Profiler.EndSample();
	}


	void AddLog(string condition, string stacktrace, LogType type)
	{
		float memUsage = 0f;
		string _condition = "";
		if (cachedString.ContainsKey(condition)) {
			_condition = cachedString[condition];
		}
		else {
			_condition = condition;
			cachedString.Add(_condition, _condition);
			memUsage += (string.IsNullOrEmpty(_condition) ? 0 : _condition.Length * sizeof(char));
			memUsage += System.IntPtr.Size;
		}
		string _stacktrace = "";
		if (cachedString.ContainsKey(stacktrace)) {
			_stacktrace = cachedString[stacktrace];
		}
		else {
			_stacktrace = stacktrace;
			cachedString.Add(_stacktrace, _stacktrace);
			memUsage += (string.IsNullOrEmpty(_stacktrace) ? 0 : _stacktrace.Length * sizeof(char));
			memUsage += System.IntPtr.Size;
		}
		bool newLogAdded = false;

		addSample();
		Log log = new Log() { logType = (_LogType)type, condition = _condition, stacktrace = _stacktrace, sampleId = samples.Count - 1 };
		memUsage += log.GetMemoryUsage();
		//memUsage += samples.Count * 13 ;

		logsMemUsage += memUsage / 1024 / 1024;

		if (TotalMemUsage > maxSize) {
			clear();
			Debug.Log("Memory Usage Reach" + maxSize + " mb So It is Cleared");
			return;
		}

		bool isNew = false;
		//string key = _condition;// + "_!_" + _stacktrace ;
		if (logsDic.ContainsKey(_condition, stacktrace)) {
			isNew = false;
			logsDic[_condition][stacktrace].count++;
		}
		else {
			isNew = true;
			collapsedLogs.Add(log);
			logsDic[_condition][stacktrace] = log;

			if (type == LogType.Log)
				numOfCollapsedLogs++;
			else if (type == LogType.Warning)
				numOfCollapsedLogsWarning++;
			else
				numOfCollapsedLogsError++;
		}

		if (type == LogType.Log)
			numOfLogs++;
		else if (type == LogType.Warning)
			numOfLogsWarning++;
		else
			numOfLogsError++;


		logs.Add(log);
		if (!collapse || isNew) {
			bool skip = false;
			if (log.logType == _LogType.Log && !showLog)
				skip = true;
			if (log.logType == _LogType.Warning && !showWarning)
				skip = true;
			if (log.logType == _LogType.Error && !showError)
				skip = true;
			if (log.logType == _LogType.Assert && !showError)
				skip = true;
			if (log.logType == _LogType.Exception && !showError)
				skip = true;

			if (!skip) {
				if (string.IsNullOrEmpty(filterText) || log.condition.ToLower().Contains(filterText.ToLower())) {
					currentLog.Add(log);
					newLogAdded = true;
				}
			}
		}

		if (newLogAdded) {
			calculateStartIndex();
			int totalCount = currentLog.Count;
			int totalVisibleCount = (int)(Screen.height * 0.75f / size.y);
			if (startIndex >= (totalCount - totalVisibleCount))
				scrollPosition.y += size.y;
		}

        //! renjun.pei: ios上疑似 每次有log的时候 gcallock从16B-->72B
		//try {
		//	gameObject.SendMessage("OnLog", log);
		//}
		//catch (System.Exception e) {
		//	Debug.LogException(e);
		//}
	}

	List<Log> threadedLogs = new List<Log>();
	void CaptureLogThread(string condition, string stacktrace, LogType type)
	{
		Log log = new Log() { condition = condition, stacktrace = stacktrace, logType = (_LogType)type };
		lock (threadedLogs) {
			threadedLogs.Add(log);
		}
	}

    void InvalidInput()
    {
        if (EventSystem.current)
        {
            EventSystem.current.currentInputModule.DeactivateModule();
        }
    }

	//save user config
	void OnApplicationQuit()
	{
		PlayerPrefs.SetInt("Reporter_currentView", (int)currentView);
		PlayerPrefs.SetInt("Reporter_show", (show == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_collapse", (collapse == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_clearOnNewSceneLoaded", (clearOnNewSceneLoaded == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showTime", (showTime == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showScene", (showScene == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showMemory", (showMemory == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showFps", (showFps == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showLog", (showLog == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showWarning", (showWarning == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showError", (showError == true) ? 1 : 0);
		PlayerPrefs.SetString("Reporter_filterText", filterText);
		PlayerPrefs.SetFloat("Reporter_size", size.x);

		PlayerPrefs.SetInt("Reporter_showClearOnNewSceneLoadedButton", (showClearOnNewSceneLoadedButton == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showTimeButton", (showTimeButton == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showSceneButton", (showSceneButton == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showMemButton", (showMemButton == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showFpsButton", (showFpsButton == true) ? 1 : 0);
		PlayerPrefs.SetInt("Reporter_showSearchText", (showSearchText == true) ? 1 : 0);

		PlayerPrefs.Save();
	}
}


