using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels.Core;
using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    [InitializeOnLoad]
    public static class M9WorldValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public string startedUtc;
            public List<string> passed = new();
            public List<string> errors = new();
            public int firstFrame, lastFrame;
        }
        private const string Pending = "LEVELS.M9.Pending";
        private const string Path = "Library/M9WorldValidation.json";
        private static Report report;
        private static Gamepad pad;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static M9WorldValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("LEVEL's/M9/Validate Saved World")]
        public static void ValidateSavedWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before validation.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            report = new Report { startedUtc = DateTime.UtcNow.ToString("o") };
            try
            {
                int portalCount = 0;
                foreach (string name in new[] { "Village", "Plateau", "Tower" })
                {
                    var scene = EditorSceneManager.OpenScene(M9WorldDesignSetup.Root+"Scenes/"+name+".unity");
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                    Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0), name+": no missing scripts");
                    Check(all.Count(t => t.GetComponent<PlayerMovement>() != null)==1, name+": exactly one saved player");
                    var portals = all.Select(t => t.GetComponent<ScenePortal>()).Where(p => p!=null).ToArray();
                    Check(portals.Length == (name=="Plateau" ? 2 : 1),name+": original portal count, no duplicate triggers");
                    foreach (var portal in portals)
                    {
                        portalCount++;
                        Check(portal.GetComponent<BoxCollider2D>().isTrigger && portal.TransitionService != null && portal.TransitionService.Screen != null,
                            name+" -> "+portal.DestinationScene+": trigger, service and loading-screen references");
                        float sign = Mathf.Sign(portal.transform.position.y);
                        Physics2D.SyncTransforms();
                        bool clear = true;
                        for (float y=4.6f;y<=7f;y+=.1f)
                            if (SolidAt(new Vector2(0,sign*y),new Vector2(.7f,.95f),true)) clear=false;
                        Check(clear,name+" -> "+portal.DestinationScene+": full player footprint has a clear central approach");
                    }
                    foreach (var spawn in all.Select(t => t.GetComponent<SpawnPoint>()).Where(p=>p!=null))
                    {
                        Check(!SolidAt(spawn.transform.position,new Vector2(.7f,.95f)) && portals.All(p =>
                            !p.GetComponent<Collider2D>().bounds.Intersects(new Bounds(spawn.transform.position,new Vector3(.7f,.95f,0)))),
                            name+"/"+spawn.Id+": spawn is clear of solids and portals");
                    }
                    if (name=="Plateau")
                    {
                        var cliffs = all.Single(t => t.name=="PlateauCliffs");
                        var cliffColliders=cliffs.GetComponentsInChildren<BoxCollider2D>();
                        Check(cliffColliders.Count(c=>!c.name.EndsWith("EnemyLimit"))==6,"Cliffs: six solid editable wall segments");
                        Check(cliffColliders.Count(c=>c.name.EndsWith("EnemyLimit") &&
                            (c.excludeLayers.value & LayerMask.GetMask("Player"))!=0)==2,
                            "Both outer portal edges stop enemies while excluding Player collisions");
                        bool sealedEdges = true;
                        for (float y=-8f;y<=8f;y+=.1f)
                            foreach (int side in new[] {-1,1}) if (!SolidAt(new Vector2(side*12.3f,y),Vector2.one*.02f)) sealedEdges=false;
                        for (float x=-12.3f;x<=12.3f;x+=.1f)
                            if (Mathf.Abs(x)>1.51f)
                                foreach (int side in new[] {-1,1}) if (!SolidAt(new Vector2(x,side*7.3f),Vector2.one*.02f)) sealedEdges=false;
                        Check(sealedEdges,"Cliffs: dense edge/corner sweep finds no collider gaps outside exits");
                        var spawner=all.Select(t=>t.GetComponent<EnemySpawner>()).Single(p=>p!=null);
                        var settings=new SerializedObject(spawner);
                        int mask=settings.FindProperty("blockingLayers").intValue;
                        Check((mask & LayerMask.GetMask("Default","World","Player","Enemy"))==LayerMask.GetMask("Default","World","Player","Enemy"),
                            "Spawner includes all existing obstacle, portal, player and enemy layers");
                        Check(spawner.SpawnArea.Size==new Vector2(22,9) && spawner.Enemies[0].maxAlive==5 && spawner.Enemies[0].spawnInterval==2 && spawner.SafeDistance==3,
                            "M9 interior area 22x9; five Goblins, two-second interval and three-unit safe zone preserved");
                        bool clearArea = true;
                        for(float x=-11;x<=11;x+=.25f) for(float y=-4.5f;y<=4.5f;y+=.25f)
                            if (SolidAt(new Vector2(x,y),Vector2.one*.02f)) clearArea=false;
                        Check(clearArea,"Entire spawn-area rectangle lies on unobstructed Plateau floor");
                    }
                    if (name=="Tower")
                    {
                        var door=all.Select(t=>t.GetComponent<LockedBossDoorInteraction>()).Single(d=>d!=null);
                        Check(door.GetComponent<Collider2D>().enabled && !door.GetComponent<Collider2D>().isTrigger &&
                            door.GetComponentsInChildren<ScenePortal>(true).Length==0,"Boss door remains solid with no transition component");
                        var text=door.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t=>t.name=="BossMessage");
                        Check(text.text=="Der Boss schläft noch.\nKomm in Phase 3 wieder!" && text.font!=null && text.fontSize>=28,
                            "Exact German boss message, readable font and saved UI references");
                    }
                }
                Check(portalCount==4,"Exactly four directed scene transitions");
                Check(!Physics2D.GetIgnoreLayerCollision(8,9) && !Physics2D.GetIgnoreLayerCollision(8,10),"World collides with both Player and Enemy");
                report.status="passed";
            }
            catch(Exception e) { report.errors.Add(e.ToString()); report.status="failed"; throw; }
            finally { File.WriteAllText("Library/M9SavedWorldValidation.json",JsonUtility.ToJson(report,true)); EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        private static bool SolidAt(Vector2 center, Vector2 size, bool forPlayer = false)
        {
            var filter=new ContactFilter2D { useTriggers=false }; filter.SetLayerMask(LayerMask.GetMask("World"));
            var hits=new List<Collider2D>(); Physics2D.OverlapBox(center,size,0,filter,hits);
            return hits.Any(c=>!forPlayer || (c.excludeLayers.value & LayerMask.GetMask("Player"))==0);
        }
        private static void Check(bool condition,string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.passed.Add(message);
        }
        [MenuItem("LEVEL's/M9/Run World Play Mode Validation")]
        public static void Start()
        {
            ValidateSavedWorld();
            SessionState.SetBool(Pending,true);
            EditorSceneManager.OpenScene(M9WorldDesignSetup.Root+"Scenes/Village.unity");
            LevelsPlayModeSetup.UseValidationScene(LevelsPlayModeSetup.VillagePath);
            EditorApplication.isPlaying=true;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
            SessionState.SetBool(Pending,false);
            report=new Report { startedUtc=DateTime.UtcNow.ToString("o"), firstFrame=Time.frameCount };
            Application.logMessageReceived+=CaptureError;
            GameSession.Instance.StartCoroutine(GuardedRun());
        }
        private static void CaptureError(string message,string stack,LogType type)
        {
            if (type==LogType.Error || type==LogType.Exception || type==LogType.Assert) report.errors.Add(message+"\n"+stack);
        }
        private static IEnumerator GuardedRun()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(Run());
            while(stack.Count>0)
            {
                object next=null; bool moved;
                try { moved=stack.Peek().MoveNext(); if (moved) next=stack.Peek().Current; }
                catch(Exception e) { report.errors.Add(e.ToString()); break; }
                if (!moved) { stack.Pop(); continue; }
                if(next is IEnumerator nested) { stack.Push(nested); continue; }
                File.WriteAllText(Path,JsonUtility.ToJson(report,true)); yield return next;
            }
            if (pad!=null && pad.added) InputSystem.RemoveDevice(pad);
            if (keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse!=null && mouse.added) InputSystem.RemoveDevice(mouse);
            Time.timeScale=1;
            report.lastFrame=Time.frameCount; report.status=report.errors.Count==0 ? "passed" : "failed";
            Application.logMessageReceived-=CaptureError; File.WriteAllText(Path,JsonUtility.ToJson(report,true));
            Debug.Log("M9 play validation: "+report.status+", "+report.passed.Count+" checks. "+Path);
            EditorApplication.isPlaying=false;
        }
        private static PlayerMovement Player() => Object.FindAnyObjectByType<PlayerMovement>();
        private static void MoveTo(Vector2 point)
        {
            var body=Player().GetComponent<Rigidbody2D>(); body.position=point; body.linearVelocity=Vector2.zero;
            Player().transform.position=point; Physics2D.SyncTransforms();
        }
        private static void Stick(Vector2 value, params GamepadButton[] buttons)
        {
            var state = new GamepadState { leftStick=value };
            foreach (var button in buttons) state = state.WithButton(button);
            InputSystem.QueueStateEvent(pad,state);
        }
        private static IEnumerator WaitUntil(Func<bool> condition,float timeout,string error)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!condition()) { if(Time.realtimeSinceStartup>end) throw new InvalidOperationException(error); yield return null; }
        }
        private static IEnumerator Through(string destination, Vector2 start, Vector2 direction, Vector2 expected)
        {
            var source=Player(); source.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad",pad);
            MoveTo(start); Stick(direction);
            float began=Time.realtimeSinceStartup;
            yield return WaitUntil(()=>SceneTransitionService.IsTransitioning,3,"Portal did not start: "+destination);
            Check(!source.enabled && !source.GetComponent<PlayerAttack>().enabled,"Transition to "+destination+": movement and combat disabled");
            var screen=Object.FindAnyObjectByType<LoadingScreen>(); Check(screen!=null && screen.IsVisible,"Transition to "+destination+": loading overlay visible");
            yield return WaitUntil(()=>!SceneTransitionService.IsTransitioning,12,"Transition did not complete: "+destination);
            var player=Player();
            Check(SceneManager.GetActiveScene().name==destination && Object.FindObjectsByType<PlayerMovement>().Length==1,
                "Arrived in "+destination+" with exactly one player");
            Check(Vector2.Distance(player.transform.position,expected)<.3f,"Arrived at correct SpawnPoint in "+destination);
            Check(Time.realtimeSinceStartup-began>=1.99f && !Object.FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include).Single().IsVisible,
                "Transition to "+destination+": two-second loading minimum and overlay cleanup");
            Check(player.GetComponent<PlayerInputReader>().MoveInput.sqrMagnitude>.9f,"Held controller stick survives transition to "+destination);
            Vector2 before=player.transform.position;
            yield return new WaitForSeconds(.15f);
            Check(Vector2.Dot((Vector2)player.transform.position-before,direction)>.3f,"Held stick moves the new player in "+destination);
            Stick(Vector2.zero); yield return new WaitForSeconds(.1f);
            Check(!SceneTransitionService.IsTransitioning,"No immediate portal loop in "+destination);
        }
        private static IEnumerator Snapshot(string name)
        {
            var follow=Object.FindAnyObjectByType<CameraFollow>(); follow.enabled=false;
            var camera=Camera.main; var pos=camera.transform.position; float size=camera.orthographicSize;
            camera.transform.position=new Vector3(0,0,-10); camera.orthographicSize=11.5f;
            ScreenCapture.CaptureScreenshot("Library/M9_"+name+".png"); yield return null; yield return null;
            camera.transform.position=pos; camera.orthographicSize=size; follow.enabled=true;
        }
        private static IEnumerator Run()
        {
            yield return null;
            pad=InputSystem.AddDevice<Gamepad>("M9_TestPad");
            keyboard=InputSystem.AddDevice<Keyboard>("M9_TestKeyboard"); mouse=InputSystem.AddDevice<Mouse>("M9_TestMouse");
            var session=GameSession.Instance; session.AddGold(37); session.AddExperience(73);
            yield return Snapshot("Village");
            yield return Through("Plateau",new Vector2(0,5),Vector2.up,new Vector2(0,-5));
            yield return Snapshot("Plateau");
            yield return Through("Village",new Vector2(0,-5),Vector2.down,new Vector2(0,5));
            Check(Object.FindAnyObjectByType<LevelingStation>()!=null && Object.FindAnyObjectByType<ShopStation>()!=null,
                "Village retains leveling well and shop after round trip");
            yield return Through("Plateau",new Vector2(0,5),Vector2.up,new Vector2(0,-5));
            var spawner=Object.FindAnyObjectByType<EnemySpawner>(); spawner.enabled=false;
            foreach(var enemy in Object.FindObjectsByType<EnemySpawnIdentity>()) Object.Destroy(enemy.gameObject);
            yield return null;
            // Sample the actual M8 validator with the real prefab footprint, not a point approximation.
            var args=new object[] {spawner.Enemies[0].prefab,default(Bounds)};
            Check((bool)typeof(EnemySpawner).GetMethod("TryGetFootprint",Private).Invoke(spawner,args),"Actual Goblin collider footprint supported");
            Bounds shape=(Bounds)args[1]; int valid=0,rejected=0;
            var portals=Object.FindObjectsByType<ScenePortal>(); var entrances=Object.FindObjectsByType<SpawnPoint>();
            for(float x=-11;x<=11;x+=.25f) for(float y=-6;y<=6;y+=.25f)
            {
                Bounds footprint=shape; footprint.center+=(Vector3)new Vector2(x,y);
                bool accepted=(bool)typeof(EnemySpawner).GetMethod("IsPositionValid",Private).Invoke(spawner,new object[] {footprint,(Vector2)Player().transform.position,portals,entrances});
                if(!accepted) { rejected++; continue; } valid++;
                CheckSample(spawner.SpawnArea.Contains(footprint) && !SolidAt(footprint.center,footprint.size),"Accepted spawn intersects new geometry or leaves area");
                CheckSample(portals.All(p=>!p.GetComponent<Collider2D>().bounds.Intersects(footprint)),"Accepted spawn touches portal");
            }
            Check(valid>1000 && rejected>100,"Dense Goblin footprint grid: "+valid+" valid floor samples, "+rejected+" exclusions; no cliff/gate/portal intersections");
            // Real rigidbody movement into visible boundary and gate jamb.
            MoveTo(new Vector2(11,0)); Stick(Vector2.right); yield return new WaitForSeconds(.6f);
            Check(Player().transform.position.x<11.72f,"Player physically blocked by east cliff");
            MoveTo(new Vector2(2.25f,4)); Stick(Vector2.up); yield return new WaitForSeconds(.5f);
            Check(Player().transform.position.y<6.2f && Player().GetComponent<Collider2D>().IsTouchingLayers(LayerMask.GetMask("World")),
                "Player physically blocked by rounded tower stone jamb");
            Stick(Vector2.zero);
            var enemyTest=Object.Instantiate(spawner.Enemies[0].prefab,new Vector3(11,0,0),Quaternion.identity);
            enemyTest.GetComponent<ContactDamage>().enabled=false;
            var enemyBody=enemyTest.GetComponent<Rigidbody2D>();
            enemyTest.GetComponent<EnemyAI>().enabled=false;
            for(int i=0;i<25;i++) { enemyBody.linearVelocity=Vector2.right*4; yield return new WaitForFixedUpdate(); }
            Check(enemyTest.transform.position.x<11.8f,"Enemy rigidbody physically blocked by cliff");
            foreach(int direction in new[] {-1,1})
            {
                enemyBody.position=new Vector2(0,direction*6); enemyTest.transform.position=enemyBody.position; Physics2D.SyncTransforms();
                for(int i=0;i<25;i++) { enemyBody.linearVelocity=Vector2.up*(direction*4); yield return new WaitForFixedUpdate(); }
                Check(Mathf.Abs(enemyTest.transform.position.y)<7 && enemyTest.GetComponent<Collider2D>().IsTouchingLayers(LayerMask.GetMask("World")),
                    "Enemy cannot leave Plateau through "+(direction>0 ? "north" : "south")+" portal gap");
            }
            Object.Destroy(enemyTest.gameObject);
            yield return Through("Tower",new Vector2(0,5),Vector2.up,new Vector2(0,-5));
            yield return Snapshot("Tower");
            yield return BossDoorTests();
            yield return Through("Plateau",new Vector2(0,-5),Vector2.down,new Vector2(0,5));
            Check(GameSession.Instance==session && session.CurrentGold==37 && session.TotalExperience==73,
                "Same GameSession and exact gold/EXP survive all four transitions");
            Check(Time.frameCount>report.firstFrame+100,"Real player loop advanced across the integration tests");
        }
        private static void CheckSample(bool value,string error) { if(!value) throw new InvalidOperationException(error); }
        private static IEnumerator BossDoorTests()
        {
            var door=Object.FindAnyObjectByType<LockedBossDoorInteraction>();
            MoveTo(new Vector2(0,2.7f));
            var input=Player().GetComponent<PlayerInput>(); input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E)); yield return new WaitForSeconds(.08f);
            Check(door.IsMessageVisible,"E in range shows boss message");
            yield return Snapshot("BossMessage");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E)); yield return new WaitForSeconds(.08f);
            Check(!door.IsMessageVisible,"Second E closes boss message without reopening it");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            MoveTo(new Vector2(0,0)); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E)); yield return new WaitForSeconds(.08f);
            Check(!door.IsMessageVisible,"E outside range cannot interact with boss door");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            // Pointer click through the actual Attack action must not set its cooldown or start a slash.
            var attack=Player().GetComponent<PlayerAttack>();
            float cooldown=(float)typeof(PlayerAttack).GetField("nextAttackTime",Private).GetValue(attack);
            Vector2 screen=Camera.main.WorldToScreenPoint(door.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}.WithButton(MouseButton.Left)); yield return new WaitForSeconds(.08f);
            Check(door.IsMessageVisible && (float)typeof(PlayerAttack).GetField("nextAttackTime",Private).GetValue(attack)==cooldown,
                "Clicking door shows message without triggering attack or cooldown");
            InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return new WaitForSeconds(.08f);
            Check(!door.IsMessageVisible,"Escape closes boss message");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}.WithButton(MouseButton.Left)); yield return new WaitForSeconds(.08f);
            Check(door.IsMessageVisible,"Repeated door click reopens the message");
            InputSystem.QueueStateEvent(mouse,new MouseState {position=screen}); yield return null;
            yield return new WaitForSecondsRealtime(4.1f);
            Check(!door.IsMessageVisible,"Boss message automatically closes after four seconds");
            Vector2 empty=Camera.main.WorldToScreenPoint(new Vector3(5,0,0));
            InputSystem.QueueStateEvent(mouse,new MouseState {position=empty}.WithButton(MouseButton.Left)); yield return new WaitForSeconds(.08f);
            Check((float)typeof(PlayerAttack).GetField("nextAttackTime",Private).GetValue(attack)>cooldown && !door.IsMessageVisible,
                "Left-click away from door retains normal Attack action");
            InputSystem.QueueStateEvent(mouse,new MouseState {position=empty}); yield return new WaitForSeconds(.4f);
            MoveTo(new Vector2(0,2.7f)); input.SwitchCurrentControlScheme("Gamepad",pad);
            Stick(Vector2.zero,GamepadButton.South); yield return new WaitForSeconds(.08f);
            Check(door.IsMessageVisible,"Gamepad A in range shows message");
            Stick(Vector2.zero); yield return null;
            Stick(Vector2.zero,GamepadButton.South); yield return new WaitForSeconds(.08f);
            Check(!door.IsMessageVisible,"Gamepad A closes message");
            Stick(Vector2.zero); yield return null;
            cooldown=(float)typeof(PlayerAttack).GetField("nextAttackTime",Private).GetValue(attack);
            Stick(Vector2.zero,GamepadButton.RightShoulder); yield return new WaitForSeconds(.08f);
            Check((float)typeof(PlayerAttack).GetField("nextAttackTime",Private).GetValue(attack)>cooldown,"RB/R1 still triggers a normal attack near the boss door");
            Stick(Vector2.up); yield return new WaitForSeconds(.5f);
            Check(Player().transform.position.y<3.2f && door.GetComponent<Collider2D>().enabled && !SceneTransitionService.IsTransitioning,
                "Boss door stays closed and blocks movement; no fight or scene transition");
            Stick(Vector2.zero);
        }
    }
}
