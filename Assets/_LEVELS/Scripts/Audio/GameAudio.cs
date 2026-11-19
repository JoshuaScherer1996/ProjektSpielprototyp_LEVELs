using System.Collections;
using Levels.Core;
using Levels.Scenes;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Levels.Audio
{
    [DefaultExecutionOrder(900), DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private AudioLibrary library;
        private AudioListener fallbackListener;
        private Transform player;
        private Button lastSelection;
        private bool bossFight;
        private bool wasPaused;
        private bool loading;
        private bool quitting;
        private double endingUntil;
        private float duck = 1f;
        private float musicFade;
        private string sceneName;
        private AudioClip currentTrack;
        private AudioClip mainMenu;
        private AudioClip village;
        private AudioClip plateau;
        private AudioClip tower;
        private AudioClip boss;
        private AudioClip gameOver;
        private AudioClip victoryIntro;
        private AudioClip victoryLoopTrack;
        private AudioSource music;
        private AudioSource victoryLoop;
        private GameSfx sfx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance == null) new GameObject("Game Audio").AddComponent<GameAudio>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            library = Resources.Load<AudioLibrary>("LevelsAudio");
            if (library == null)
            {
                Debug.LogError("Missing Resources/LevelsAudio audio library.", this);
                enabled = false;
                return;
            }
            mainMenu = Resources.Load<AudioClip>("Main Menu");
            village = Resources.Load<AudioClip>("Town");
            plateau = Resources.Load<AudioClip>("Battle");
            tower = Resources.Load<AudioClip>("Ancient Dungeon");
            boss = Resources.Load<AudioClip>("Monstrosity");
            gameOver = Resources.Load<AudioClip>("7. Emotional Theme (Sentimental Wishes)");
            victoryIntro = Resources.Load<AudioClip>("Victory (Intro)");
            victoryLoopTrack = Resources.Load<AudioClip>("Victory (Loop)");
            fallbackListener = gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = false;
            AudioMixerGroup musicGroup = FindMixerGroup("Music");
            AudioMixerGroup sfxGroup = FindMixerGroup("SFX");
            music = CreateSource(gameObject, "Music", musicGroup, 0);
            victoryLoop = CreateSource(gameObject, "Victory Loop", musicGroup, 0);
            sfx = new GameSfx(gameObject, library, sfxGroup);
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private AudioMixerGroup FindMixerGroup(string name)
        {
            if (library.mixer != null)
                foreach (AudioMixerGroup group in library.mixer.FindMatchingGroups(name))
                    if (group.name == name) return group;
            Debug.LogError($"The audio library needs a '{name}' mixer group.", this);
            return null;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (instance == this) instance = null;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneName = scene.name;
            bossFight = false;
            lastSelection = null;
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            fallbackListener.enabled = false;
            sfx.StopWorld();
            foreach (AudioListener listener in FindObjectsByType<AudioListener>())
                if (listener.isActiveAndEnabled) return;
            fallbackListener.enabled = true;
        }

        private void LateUpdate()
        {
            if (loading && !SceneTransitionService.IsTransitioning) loading = false;
            bool paused = GameFlow.State == RunState.Paused;
            if (paused != wasPaused)
            {
                sfx.SetPaused(paused);
                wasPaused = paused;
                lastSelection = null;
            }
            sfx.Update();
            UpdateMusic(paused);
            UpdateSelection();
        }

        public static void Play(Cue cue)
        {
            if (instance == null || instance.sfx == null) return;
            instance.PlayCue(cue, 0f, false, null);
        }

        public static void PlayAt(Cue cue, Vector2 point)
        {
            if (instance == null || instance.sfx == null) return;
            float distance = instance.player != null ? Vector2.Distance(instance.player.position, point) : 0f;
            instance.PlayCue(cue, distance, false, null);
        }

        public static int StartLoop(Cue cue, Transform owner)
        {
            if (instance == null || instance.sfx == null) return 0;
            return instance.PlayCue(cue, 0f, true, owner);
        }

        public static void StopLoop(int handle) => instance?.sfx?.StopLoop(handle);

        public static void ConfirmAndQuit()
        {
            if (instance == null || instance.sfx == null) { Quit(); return; }
            if (instance.quitting) return;
            instance.quitting = true;
            instance.sfx.Play(Cue.Confirm, 0f, false, null, false, out double duration);
            instance.StartCoroutine(QuitAfterConfirmation(duration));
        }

        private static IEnumerator QuitAfterConfirmation(double duration)
        {
            if (duration > 0d) yield return new WaitForSecondsRealtime((float)duration);
            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }

        private int PlayCue(Cue cue, float distance, bool loop, Transform owner)
        {
            if (cue == Cue.Pause) sfx.StopCue(Cue.Continue);
            if (cue == Cue.Continue) sfx.StopCue(Cue.Pause);
            int handle = sfx.Play(cue, distance, loop, owner, loading || SceneTransitionService.IsTransitioning,
                out double duration);
            if (cue == Cue.PlayerDeath && handle != 0)
                endingUntil = AudioSettings.dspTime + duration;
            return handle;
        }

        public static double BeginLoading()
        {
            if (instance == null || instance.sfx == null || instance.loading) return 0d;
            instance.loading = true;
            instance.sfx.StopWorld();
            return Time.realtimeSinceStartupAsDouble + .16;
        }

        public static void RevealLoadingScreen()
        {
            if (instance == null || instance.sfx == null) return;
            instance.StopMusic();
            instance.sfx.StopForLoading();
        }

        public static void BeginBossFight()
        {
            if (instance != null) instance.bossFight = true;
        }

        public static void EndCombatAudio() => instance?.sfx?.StopWorld();

        public static void PlayerStride(ref float distance, float traveled)
        {
            if (instance == null || instance.library == null) return;
            Cue surface = instance.sceneName switch { "Village" => Cue.Gravel, "Plateau" => Cue.Grass, _ => Cue.Stone };
            if (ReachedStride(ref distance, traveled, instance.library.playerStride)) Play(surface);
        }

        public static void EnemyStride(ref float distance, float traveled, EnemyType type, Vector2 point)
        {
            if (instance == null || instance.library == null || instance.sceneName != "Plateau") return;
            bool cyclops = type == EnemyType.Cyclops;
            float stride = cyclops ? instance.library.cyclopsStride : instance.library.goblinStride;
            if (ReachedStride(ref distance, traveled, stride)) PlayAt(cyclops ? Cue.CyclopsStep : Cue.GoblinStep, point);
        }

        public static void SpiderStride(ref float distance, float traveled, Vector2 point)
        {
            if (instance == null || instance.library == null) return;
            if (ReachedStride(ref distance, traveled, instance.library.spiderStride)) PlayAt(Cue.SpiderScuttle, point);
        }

        private static bool ReachedStride(ref float distance, float traveled, float stride)
        {
            if (traveled <= .001f) return false;
            distance += traveled;
            if (distance < stride) return false;
            distance %= stride;
            return true;
        }

        private void UpdateSelection()
        {
            if (loading || SceneTransitionService.IsTransitioning) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Button button = selected != null ? selected.GetComponent<Button>() : null;
            if (button == null || !button.IsActive() || !button.IsInteractable())
            {
                if (lastSelection == null || !lastSelection.IsActive()) lastSelection = null;
                return;
            }
            if (button != lastSelection && lastSelection != null && lastSelection.IsActive()) Play(Cue.Navigate);
            lastSelection = button;
        }

        private AudioClip DesiredTrack()
        {
            if (loading || SceneTransitionService.IsTransitioning) return null;
            if (GameFlow.State == RunState.GameOver)
                return AudioSettings.dspTime >= endingUntil ? gameOver : null;
            return sceneName switch
            {
                "MainMenu" => mainMenu,
                "Village" => village,
                "Plateau" => plateau,
                "Tower" => tower,
                "SpiderArena" when bossFight && GameFlow.State != RunState.Victory => boss,
                "Win" => victoryIntro != null ? victoryIntro : victoryLoopTrack,
                _ => null
            };
        }

        private void UpdateMusic(bool paused)
        {
            var desired = DesiredTrack();
            float step = Time.unscaledDeltaTime / (loading ? .12f : library.musicFade);
            duck = Mathf.MoveTowards(duck, paused ? library.pausedMusicGain : 1f, step * 1.5f);
            if (currentTrack != desired)
            {
                musicFade = Mathf.MoveTowards(musicFade, 0f, step);
                if (musicFade == 0f)
                {
                    StopMusic();
                    if (desired != null) StartMusic(desired);
                }
            }
            else if (currentTrack != null)
                musicFade = Mathf.MoveTowards(musicFade, 1f, step);

            music.volume = victoryLoop.volume = library.musicGain * duck * musicFade;
        }

        private void StartMusic(AudioClip track)
        {
            currentTrack = music.clip = track;
            music.loop = track != gameOver && track != victoryIntro;
            if (track == victoryIntro && victoryLoopTrack != null)
            {
                double start = AudioSettings.dspTime + .12;
                double end = start + (double)track.samples / track.frequency;
                victoryLoop.clip = victoryLoopTrack;
                victoryLoop.loop = true;
                music.PlayScheduled(start);
                music.SetScheduledEndTime(end);
                victoryLoop.PlayScheduled(end);
                musicFade = 1f;
            }
            else music.Play();
        }

        private void StopMusic()
        {
            music.Stop();
            victoryLoop.Stop();
            music.clip = victoryLoop.clip = currentTrack = null;
            musicFade = 0f;
        }

        internal static AudioSource CreateSource(GameObject parent, string name, AudioMixerGroup group, int priority)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = priority;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
