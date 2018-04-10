﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Com.LuisPedroFonseca.ProCamera2D;
using CameraTransitions;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Space(10)]
    public Transform playerTransform;
    public GameObject[] PanelObjects;
    public Camera[] Cameras;

    public float transitionDuration;
    [Range(0, 1)] public float transitionEdgeSmoothness;

    private int previousCameraIndex;
    private int currentCameraIndex;
    private CameraTransition cameraTransition;
    private bool isCurrentlyTransitioning = false;

    [Space(10)]
    [Header("Visual Settings")]
    [Range(0.0f, 1.0f)]
    public float PlayerMoveSpeed;
    public Camera parallaxCamera;

    //private FreeParallax[] parallaxes;
    private PlayerController charController;
    private AccountManager accountManager;

    //  Audio
    public AudioClip[] timeWarps;
    private AudioSource menuSound;

    [Space(10)]
    [Header("Login UI")]
    public float fadeDuration;
    public GameObject mainMenuGO;
    public GameObject loggedStatusPanelGO;
    public Button LogoutButton;
    public Button SignInButton;
    public Text loggedInText;
    private Graphic[] mainMenuGraphics;
    private Graphic[] loginPanelGraphics;
    private Graphic[] loggedStatusGraphics;
    private Button[] mainMenuButtons;
    private LoginPanel loginPanel;

    //  Continue button
    private GameObject continueButtonGO;

    //  Parallax stuff
    public GameObject[] MenuParallaxes;
    private int oldMenuIndex = 0;
    private int nextMenuIndex = 0;

    private void OnEnable()
    {
        AccountManager.OnLoggedIn += OnSignedIn;
        LoginPanel.OnPlayOffline += OnPlayOffline;
    }

    private void OnDisable()
    {
        AccountManager.OnLoggedIn -= OnSignedIn;
        LoginPanel.OnPlayOffline -= OnPlayOffline;
    }

    void Awake()
    {
        //  Find and assign references
        cameraTransition = FindObjectOfType<CameraTransition>();
        charController = FindObjectOfType<PlayerController>();
        accountManager = FindObjectOfType<AccountManager>();
        loginPanel = FindObjectOfType<LoginPanel>();
        menuSound = GetComponent<AudioSource>();

        //  UI
        mainMenuGraphics = mainMenuGO.GetComponentsInChildren<Graphic>();
        mainMenuButtons = mainMenuGO.GetComponentsInChildren<Button>();
        loginPanelGraphics = loginPanel.GetComponentsInChildren<Graphic>();
        loggedStatusGraphics = loggedStatusPanelGO.GetComponentsInChildren<Graphic>();

        continueButtonGO = GetComponentInChildren<ContinueGame>().gameObject;
    }

    void Start()
    {
        //  Initial setups
        currentCameraIndex = 0;

        //  Disable all menu objects at start
        for (int i = 1; i < PanelObjects.Length; i++)
            PanelObjects[i].SetActive(false);

        //  Disable main menu
        mainMenuGO.SetActive(false);
        loggedStatusPanelGO.SetActive(false);

        if (AccountManager.IsLoggedIn)
            OnSignedIn();

        //  Set up the cape helper
        CapePhysicsHelper capeHelper = FindObjectOfType<CapePhysicsHelper>();
        capeHelper.transform.position = charController.transform.position;
        capeHelper.capeControlNode = GameObject.FindGameObjectWithTag(Tags.bone_Cape_CTRL).transform;
        capeHelper.GetComponent<DistanceJoint2D>().connectedBody = GameObject.FindGameObjectWithTag(Tags.bone_Cape).GetComponent<Rigidbody2D>();

        //  Disable unused parallaxes
        for (int i = 1; i < MenuParallaxes.Length; i++)
        {
            MenuParallaxes[i].SetActive(false);
        }
    }

    void LateUpdate()
    {
        //  Set the charController move speed to simulate walking
        charController.velocity = new Vector3(PlayerMoveSpeed, charController.velocity.y, charController.velocity.z);
        charController.animator.SetFloat("xVelocity", PlayerMoveSpeed);

        //  Update parallaxes
        //for (int i = 0; i < parallaxes.Length; i++)
            //parallaxes[i].Speed = parallaxCamera.velocity.x * PlayerMoveSpeed * -1;
    }
    
    public void TransitionToCamera(int CameraIndex)
    {
        previousCameraIndex = currentCameraIndex;

        PanelObjects[CameraIndex].SetActive(true);

        //  Cache the button position in normalized screen space coordinates.
        Vector2 transitionCenter = Cameras[currentCameraIndex].ScreenToViewportPoint(Input.mousePosition);

        playerAudio.randomizePitch(menuSound);
        int randomIndex = Random.Range(0, timeWarps.Length);
        menuSound.PlayOneShot(timeWarps[randomIndex]);

        //  Perform the transition
        cameraTransition.DoTransition(CameraTransitionEffects.SmoothCircle, Cameras[currentCameraIndex], Cameras[CameraIndex], transitionDuration, new object[] { false, transitionEdgeSmoothness, transitionCenter });

        //  Update player position
        playerTransform.position = new Vector3(playerTransform.position.x, playerTransform.position.y, Cameras[CameraIndex].transform.position.z + 5);

        currentCameraIndex = CameraIndex;
    }

    public void BroadcastTransitionCompleteEvent()
    {
        PanelObjects[previousCameraIndex].SetActive(false);
        MenuParallaxes[oldMenuIndex].SetActive(false);
        oldMenuIndex = nextMenuIndex;
    }

    private void OnSignedIn()
    {
        LogoutButton.gameObject.SetActive(true);
        SignInButton.gameObject.SetActive(false);

        TransitionToMainMenu();

        //  Continue button
        if (AccountManager.CurrentUser.checkpointID <= 0)
            continueButtonGO.SetActive(false);
        else
            continueButtonGO.SetActive(true);
    }

    private void OnPlayOffline()
    {
        AccountManager.IsLoggedIn = false;

        LogoutButton.gameObject.SetActive(false);
        SignInButton.gameObject.SetActive(true);

        TransitionToMainMenu();

        //  Continue button
        //  Determine at start if a save file exist, if so, the continue button should be enabled
        if (GameManager.DoesLocalSaveExist())
            continueButtonGO.SetActive(true);
        else
            continueButtonGO.SetActive(false);
    }

    public void OnSignOut()
    {
        accountManager.ProcessSignOut();

        //  Reset status msg
        loginPanel.StatusText.text = "";

        TransitionToLoginMenu();
    }

    public void TransitionToMainMenu()
    {
        StartCoroutine(CoTransitionFromLoginToMainMenu());
    }

    public void TransitionToLoginMenu()
    {
        StartCoroutine(CoTransitionFromMainMenuToLogin());
    }

    IEnumerator CoTransitionFromMainMenuToLogin()
    {
        //  Disable buttons
        ToggleMainMenuButtons(false);
        ToggleLoggedInButtons(false);

        //  Fade out main menu
        for (int i = 0; i < mainMenuGraphics.Length; i++)
            mainMenuGraphics[i].CrossFadeAlpha(0, fadeDuration, true);

        //  Fade out logged in panel
        for (int i = 0; i < loggedStatusGraphics.Length; i++)
            loggedStatusGraphics[i].CrossFadeAlpha(0, fadeDuration, true);

        yield return new WaitForSeconds(fadeDuration);

        mainMenuGO.SetActive(false);
        loggedStatusPanelGO.SetActive(false);

        loginPanel.gameObject.SetActive(true);

        //  Fade out login menu
        for (int i = 0; i < loginPanelGraphics.Length; i++)
            loginPanelGraphics[i].CrossFadeAlpha(0, 0, true);

        //  Fade in login menu
        for (int i = 0; i < loginPanelGraphics.Length; i++)
            loginPanelGraphics[i].CrossFadeAlpha(1, fadeDuration, true);

        yield return new WaitForSeconds(fadeDuration);

        loginPanel.toggleInputField(true);
        loginPanel.ToggleLoginButtons(true);
    }

    IEnumerator CoTransitionFromLoginToMainMenu()
    {
        //  Disable button interaction for the login panel
        loginPanel.ToggleLoginButtons(false);
        loginPanel.toggleInputField(false);

        //  Fade out login panel
        for (int i = 0; i < loginPanelGraphics.Length; i++)
            loginPanelGraphics[i].CrossFadeAlpha(0, fadeDuration, true);    

        //  Update the logged in text
        if (AccountManager.IsLoggedIn)
            loggedInText.text = "Welcome\n" + AccountManager.CurrentUser.username + "!";
        else
            loggedInText.text = "Welcome! Login to save to the cloud!";

        yield return new WaitForSeconds(fadeDuration);

        loginPanel.gameObject.SetActive(false);

        mainMenuGO.SetActive(true);
        loggedStatusPanelGO.SetActive(true);

        //  Disable button interaction for the main menu panel
        ToggleMainMenuButtons(false);
        ToggleLoggedInButtons(false);

        //  Fade out main menu instantly
        for (int i = 0; i < mainMenuGraphics.Length; i++)
            mainMenuGraphics[i].CrossFadeAlpha(0, 0, true);

        //  Fade out LoggedIn Info instantly
        for (int i = 0; i < loggedStatusGraphics.Length; i++)
            loggedStatusGraphics[i].CrossFadeAlpha(0, 0, true);

        //  Fade in main menu
        for (int i = 0; i < mainMenuGraphics.Length; i++)
            mainMenuGraphics[i].CrossFadeAlpha(1, fadeDuration, true);

        //  Fade in logged in panel
        for (int i = 0; i < loggedStatusGraphics.Length; i++)
            loggedStatusGraphics[i].CrossFadeAlpha(1, fadeDuration, true);

        yield return new WaitForSeconds(fadeDuration);

        //  Enable buttons
        ToggleLoggedInButtons(true);
        ToggleMainMenuButtons(true);
    }

    public void ToggleMainMenuButtons(bool isEnabled)
    {
        for (int i = 0; i < mainMenuButtons.Length; i++)
            mainMenuButtons[i].interactable = isEnabled;
    }

    public void ToggleLoggedInButtons(bool isEnabled)
    {
        LogoutButton.interactable = isEnabled;
        SignInButton.interactable = isEnabled;
    }

    public void SetActiveMenuParallax(int nextIndex)
    {
        nextMenuIndex = nextIndex;
        MenuParallaxes[nextIndex].SetActive(true);
    }
}