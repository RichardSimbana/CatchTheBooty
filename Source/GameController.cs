using UnityEngine;
using System.Collections;
using UnityEngine.Advertisements;
using UnityEngine.Rendering;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public GameState State { get; set; }

    public enum GameState { Start, Gameplay, Setting, Continue, End, None }

    public int Score { get; set; }
    public int ColorTracker { get; set; }

    public int HighScore
    {
        get { return PlayerPrefs.GetInt(HighScoreKey); }
        set
        {
            PlayerPrefs.SetInt(HighScoreKey, value);
            PlayerPrefs.Save();
        }
    }

    public int HasPlayedTutorial // 0 - False | 1 - True
    {
        get { return PlayerPrefs.GetInt(TutorialKey); }
        set
        {
            PlayerPrefs.SetInt(TutorialKey, value);
            PlayerPrefs.Save();
        }
    }

    public string HighScoreKey { get; set; }
    public string TutorialKey { get; set; }

    public bool SettingsOpen { get; set; }

    public bool Interrupt { get; set; }

    public bool PracticeFiring { get; set; }

    int _adTracker;

    [SerializeField]
    private GameObject _settingButton;

    void Awake()
    {
        if (Instance == null) Instance = this;

        Application.targetFrameRate = 60;

        _adTracker = 0;
        ColorTracker = 0;

        Interrupt = false;
        SettingsOpen = false;
        PracticeFiring = true;

        HighScoreKey = "highscore";
        TutorialKey = "tutorial";

        //Checks if theres a proper key set for HighScore
        if (PlayerPrefs.HasKey(HighScoreKey) == false) PlayerPrefs.SetInt(HighScoreKey, HighScore);
    }

    IEnumerator Start()
    {
        //State - Start
        State = GameState.Start;

        UIManager.Instance.InitializeHighScore();

        //In-case there is a SplashScreen, let's wait in till it's over to begin the game.
        while (!SplashScreen.isFinished) yield return null;

        //Display the intro text
        UIManager.Instance.CanvasMsgStart.SetActive(true);

        //Wait for the player to touch the screen
        yield return StartCoroutine(PNHelper.WaitForClick());

        //Remove the intro text
        UIManager.Instance.CanvasMsgStart.SetActive(false);

        if (PlayerPrefs.HasKey(TutorialKey) == false)
        {
            PlayerPrefs.SetInt(TutorialKey, 1);

            //Display the How To
            UIManager.Instance.CanvasMsgHowTo.SetActive(true);

            //Wait for the player to touch the screen
            yield return StartCoroutine(PNHelper.WaitForClick());

            //Remove the How to
            UIManager.Instance.CanvasMsgHowTo.SetActive(false);
        }

        while (true)
        {
            yield return null;

            IEnumerator playSession = PlaySession();
            yield return StartCoroutine(playSession);
            StopCoroutine(playSession);

            yield return new WaitForSeconds(0.001f);
        }
    }

    IEnumerator PlaySession()
    {
        yield return null;

        //GameState - Gameplay
        State = GameState.Gameplay;

        //Main Core Loop
        IEnumerator coreLoop = CoreLoop();
        yield return StartCoroutine(coreLoop);
        StopCoroutine(coreLoop);

        yield return new WaitForSeconds(1f);

        //Ending of the Core Loop
        IEnumerator endLoop = EndLoop();
        yield return StartCoroutine(endLoop);
        StopCoroutine(endLoop);
    }

    IEnumerator StartCore()
    {
        yield return null;

        ColorRenderController.Instance.SwitchCurrentColor();
        TreasureChestController.Instance.SetMaterials();

        IEnumerator updateCrowsNest = UIManager.Instance.UpdateCrowsNest();
        yield return StartCoroutine(updateCrowsNest);
        StopCoroutine(updateCrowsNest);
    }

    IEnumerator CoreLoop()
    {
        yield return null;

        while (!Interrupt)
        {
            yield return null;

            /*
             * Round starts, game chooses the next color to pick and than displays the
             * speech bubble and updates the flag and laterns.
             */
            IEnumerator start = StartCore();
            yield return StartCoroutine(start);
            StopCoroutine(start);

            /*
             * Main Core beginning. Cannons beginning to shoot.
            */
            State = GameState.Gameplay;

            IEnumerator cannonFiring;

            if (PracticeFiring)
            {
                //Practice Firing is the first round and serves as the tutorial.
                cannonFiring = CannonFireController.Instance.StartFiring();
                PracticeFiring = false;
            }
            else
            {
                //After the Practice Firing, game goes into the regular flow of the game.
                cannonFiring = CannonFireController.Instance.Firing();
            }

            yield return StartCoroutine(cannonFiring);
            StopCoroutine(cannonFiring);

            IEnumerator waitForBags = PNHelper.CheckIfAllObjectsDisabled();
            yield return StartCoroutine(waitForBags);
            StopCoroutine(waitForBags);

            CannonFireController.Instance.IncreaseDifficulty();
        }

        Interrupt = false;
    }

    IEnumerator EndLoop()
    {
        yield return null;

        if (Score >= 100 && ContinueUIElement.Instance.ContinueAvailable && Advertisement.IsReady("rewardedVideo"))
        {
            State = GameState.Continue;

            IEnumerator continueSeq = Continue();
            yield return StartCoroutine(continueSeq);
            StopCoroutine(continueSeq);
        }
        else
        {
            if (!ContinueUIElement.Instance.PressedContinue)
            {
                _adTracker++;
                if (_adTracker >= 2)
                {
                    _adTracker = 0;

                    IEnumerator showAd = AdManager.Instance.ShowAd("video");
                    yield return StartCoroutine(showAd);
                    StopCoroutine(showAd);
                }
            }

            State = GameState.End;

            IEnumerator endSeq = End();
            yield return StartCoroutine(endSeq);
            StopCoroutine(endSeq);
        }
    }

    IEnumerator Continue()
    {
        yield return null;

        //TODO: Could be cleaner?
        _settingButton.SetActive(false);

        IEnumerator displayContinueMenu = ContinueUIElement.Instance.DisplayContinueMenu();
        yield return StartCoroutine(displayContinueMenu);
        StopCoroutine(displayContinueMenu);

        _settingButton.SetActive(true);

        if (ContinueUIElement.Instance.PressedContinue)
        {
            IEnumerator showAd = AdManager.Instance.ShowAd();
            yield return StartCoroutine(showAd);
            StopCoroutine(showAd);

            TreasureChestController.Instance.DisableAllText();
        }
        else
        {
            State = GameState.End;

            _adTracker++;
            if (_adTracker >= 2)
            {
                _adTracker = 0;

                IEnumerator showAd = AdManager.Instance.ShowAd("video");
                yield return StartCoroutine(showAd);
                StopCoroutine(showAd);
            }

            IEnumerator endSeq = End();
            yield return StartCoroutine(endSeq);
            StopCoroutine(endSeq);
        }
    }

    IEnumerator End()
    {
        yield return null;

        //Update Highscore
        UIManager.Instance.UpdateHighScore();

        //Update Score
        UIManager.Instance.UpdateScore();

        //UIManager should be toggling. OOP should be follow, able to reuse it
        //Display the End Message
        UIManager.Instance.CanvasMsgEnd.SetActive(true);

        yield return StartCoroutine(PNHelper.WaitForClick());

        //Remove the End Message
        UIManager.Instance.CanvasMsgEnd.SetActive(false);

        //Reset Everything
        Reset();
    }

    void Reset()
    {
        //Set score too 0
        Score = 0;

        PracticeFiring = true;

        //Update the score back to zero
        UIManager.Instance.UpdateScore();

        //CannonController.Instance.Reset();
        ContinueUIElement.Instance.Reset();

        TreasureChestController.Instance.DisableAllText();

        CannonFireController.Instance.Reset();
    }
}