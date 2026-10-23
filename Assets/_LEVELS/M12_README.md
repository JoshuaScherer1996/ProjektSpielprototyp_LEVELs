# M12 — Main Menu und Game Over

Implementiert am 23.10.2026 für Unity 6000.6.2f1. Main Menu, Game Over,
Retry, Back to Main Menu und das Spawnverhalten wurden vom Nutzer in Unity
erfolgreich getestet. Keine neuen Packages.

## Szenen und UI

- `Scenes/MainMenu.unity`: dunkler Hintergrund, Titel **LEVEL's**, **PLAY**, **EXIT**,
  zentriertes Canvas mit CanvasScaler und GraphicRaycaster, ein EventSystem mit
  InputSystemUIInputModule, vorhandener LoadingScreen und SceneTransitionService.
- MainMenu ist erste aktivierte Szene in den gemeinsamen Build-Szenen. Das bei
  der Umsetzung aktive Build-Profil verwendet diese gemeinsamen Szenen; es gab
  keinen separaten Szenen-Override. Das Setup berücksichtigt auch aktive Overrides.
- Der normale Editor-Play-Start verwendet MainMenu unabhängig von der geöffneten
  Gameplay-Szene. Die bestehenden Validatoren können weiterhin eine andere
  Startszene für ihren einzelnen Prüflauf wählen.
- `Prefabs/UI/GameOverScreen.prefab`: **GAME OVER**, **RETRY** und
  **BACK TO MAIN MENU**. Als editierbare Prefab-Instanz in Village, Plateau und Tower.
- UI-Sortierung: Game Over 200, über dem vorhandenen HUD (100) und unter dem
  LoadingScreen (1000). Das Hintergrundbild blockiert Klicks in die Spielwelt.
- `Art/Fonts/RetroMenu.asset`: eigene eingebettete 5×7-Pixelschrift für TMP;
  kein Systemfont und kein externer Download erforderlich.
- Die Button-Auswahl ist farblich hervorgehoben. Explizite Navigation verbindet
  die beiden Buttons. PLAY ist im Main Menu zunächst vorausgewählt; Mausbewegung
  über dem leeren Hintergrund löscht diese Auswahl nicht. Maus-Hover über einem
  Button übernimmt die Auswahl, ohne den vorherigen Button
  zusätzlich hervorzuheben. Tastatur und Controller übernehmen die Auswahl beim
  Navigieren wieder. Maus, Tastatur und Gamepad nutzen die vorhandenen UI Actions.

## Architektur und Reset

`GameFlow` ist eine szenenlokale Komponente mit den Zuständen MainMenu, Playing,
GameOver und Loading. Es gibt einen GameFlow je Szene und weiterhin genau einen
persistenten GameSession-Singleton. MainMenuUI und GameOverUI leiten Button-Aktionen
an GameFlow weiter. MenuFocus verwaltet die anfängliche Auswahl und die Freigabe
des vorhandenen UI-Eingabemoduls. Die feste UI liegt vollständig in gespeicherten
Szenen/Prefabs und wird nicht zur Laufzeit zusammengebaut.

PLAY und RETRY beenden den logischen alten Durchlauf durch vollständiges
Zurücksetzen des bestehenden Session-Singletons. Alle drei Level werden 1,
verfügbare und gesamte EXP sowie Gold werden 0, ShopMaxHealthBonus wird 0.
HP/Max-HP-Initialisierung und Transaktionssperren werden zurückgesetzt. Alte
Szenenbeobachter melden sich beim Entladen ab; neue Komponenten abonnieren die
Session regulär. Es werden keine zusätzlichen Session- oder Player-Objekte behalten.

Der bestehende SceneTransitionService lädt Village im Single-Modus mit seinem
vorhandenen LoadingScreen. PLAY aus dem Main Menu verwendet eine einstellbare
Mindestanzeigezeit von einer Sekunde (`GameFlow.playLoadingSeconds`); die tatsächliche
Ladezeit darf bei langsamer Hardware länger sein. RETRY und normale Szenenwechsel
verwenden weiterhin ihre bestehende Mindestanzeigezeit. Der gespeicherte SpawnPoint
`NewRun` liegt an der bisherigen Player-Startposition. Der neue Player initialisiert
seine Basis-HP/Max-HP (derzeit 5/5) und seine Basiswerte ohne alte Boni. Bewegung und
Angriff werden erst nach abgeschlossener Ladeanzeige freigegeben.

BACK TO MAIN MENU leert die Session, setzt Time.timeScale auf 1 und lädt MainMenu
über denselben asynchronen Loader, ohne LoadingScreen und ohne künstliche Wartezeit.
Gameplay bleibt dort deaktiviert; die UI erhält Eingaben.
Ein erneutes PLAY durchläuft denselben vollständigen Neustart.

## Tod und Spawner

PlayerHealth ruft bei tödlichem Schaden synchron GameFlow auf, bevor weitere
Health-/Schadensbeobachter benachrichtigt werden. GameFlow sperrt Gameplay,
schließt Shop/Leveling, deaktiviert Bewegung/Angriff sowie Spawner, EnemyAI und
ContactDamage und zeigt die szeneneigene GameOverUI. Anschließend darf der Player
wie bisher zerstört werden. Der UI-Controller liegt außerhalb des Players.

Zusätzliche Zustandssperren schützen Angriff, Bewegungseingaben, Interact,
Spieler-/Gegnerschaden, Belohnungen und Session-Käufe. Die Spawner-Timer werden
bei Deaktivierung verworfen, einschließlich zwischengespeicherter Player-Referenz,
Overlap-Liste und letzter Versuchszahl. Die lebenden Gegnerzahlen werden weiterhin
aus den tatsächlichen Szenenobjekten ermittelt. Bei Retry werden sämtliche alten
Gegner mit der Szene entladen. Village enthält keinen Plateau-Spawner. Erst der
erneute Plateau-Eintritt erzeugt dessen normale, frische Intervalluhren.

Time.timeScale bleibt bei Game Over 1; die Sperre hängt nicht von einer angehaltenen
Zeitskala ab. Gehaltenes Submit muss vor der neuen Menübedienung losgelassen werden.
Point wird beim Öffnen neu aktiviert, damit auch eine unbewegte Maus nach einem
Wechsel des gemeinsam genutzten UI-Moduls unmittelbar klicken kann.

R1/RB Attack, E/Gamepad-A Interact, kontinuierliches Stick-Lesen, Gerätezuordnung
bei normalen Szenenwechseln und die Loading-Sperre bleiben erhalten.

## Reproduzierbare Editor-Prüfung

Menüeinträge unter `LEVEL's/M12/`:

1. **Configure Main Menu and Game Over**: explizites Editor-Setup für gespeicherte Assets.
2. **Validate Saved Menu Assets**: Build-Reihenfolge, Startszene, Schrift,
   Komponenten, Referenzen, eindeutige Player/Flow/EventSystem und SpawnPoint.
3. **Run Flow Play Mode Validation**: vollständiger Integrationstest mit gespeicherten
   Szenen, echten Input-System- und UI-Ereignissen sowie temporären simulierten
   Maus-/Tastatur-/Gamepad-Geräten. Die Geräte werden anschließend entfernt.

Prüfberichte und Screenshots liegen unter `Library/` und gehören nicht zu den
Spielassets: `M12SavedAssetsValidation.json`, `M12FlowValidation.json`,
`M12_MainMenu.png`, `M12_GameOver.png`.

Tatsächlich ausgeführt und bestanden:

| Prüfung | Ergebnis |
| --- | --- |
| Unity-Skriptkompilierung | Erfolgreich, keine Compilerfehler |
| M12 gespeicherte Assets/Referenzen/Build-Reihenfolge | 32 Prüfungen bestanden |
| M12 Laufzeitintegration nach Hover-/Loading-/Startauswahl-Korrekturen | 95 Prüfungen bestanden, 0 erfasste Fehler |
| EXIT im Editor | Button-Callback beendet Play Mode; Editor danach weiterhin verbunden |
| M11 gespeicherte Enemy-/UI-/Szenen-Assets | 25 Prüfungen bestanden |
| M11 Laufzeitregression | 129 Prüfungen bestanden, 0 erfasste Fehler |
| Unity Console nach den abschließenden Läufen | 0 Fehler, 0 Warnungen |
| Visuelle Kontrolle | MainMenu- und GameOver-Screenshots angesehen |

Die M12-Laufzeitprüfung umfasst PLAY, normalen Village/Plateau-Wechsel, tatsächlichen
tödlichen Gegnerkontakt, Game Over nach Player-Zerstörung, Spawn-Stopp über beide
Intervalle, Ablehnung weiterer Gegner-/Belohnungsereignisse, vollständige Resets
nach mehreren Retries, Tod bei offenem Shop und Leveling, Menürückkehr und erneutes
PLAY. Sie prüft außerdem alle drei Bedienarten, R1/RB genau einmal pro Angriff,
E/A Interact, gehaltenen Stick, gehaltenes Submit beim Tod, stationären Maus-Retry,
Input-Sperren beim Laden und eindeutige Session/Player/Flow/EventSystem-Instanzen.
Beim erneuten Plateau-Eintritt wurden frische Timer, zunächst 0 Gegner und danach
genau ein neuer Goblin und Cyclops mit Level 1 geprüft. Der abschließende M12-Lauf
lief von Frame 1 bis Frame 18777. Zusätzlich wurden die anfängliche PLAY-Auswahl,
deren Erhalt bei Mausbewegung über dem Hintergrund und die gerenderten Button-Farben
bei Maus-Hover in beiden Menüs, der Wechsel zu Tastatur/Gamepad, die einsekündige
PLAY-Mindestanzeige und die vollständig overlayfreie Menürückkehr geprüft.
`Library/M12EditorExitValidation.json` dokumentiert den separaten EXIT-Test im Editor.

Die M11-Regression deckt zusätzlich Gegnergrenzen, Spawnpositionen und Abstände,
Cyclops-Aggro/Knockback/Kollision, Treffer und Feedback, getrennte Käufe und Skalierung,
Shop-Heilung und Max-HP-Kauf sowie das Beibehalten von Daten bei normalen Wechseln ab.
Diese Prüfungen verwenden simulierte Geräte im echten Unity Input System; ein Test
mit einem physisch angeschlossenen Controller ist damit nicht behauptet.

Bei der abschließenden visuellen und automatisierten Prüfung sind keine bekannten
Funktionsfehler offen geblieben. Der Nutzer hat den Ablauf in Unity erfolgreich
getestet. Tatsächliche Windows- und WebGL-Buildprüfungen stehen weiterhin aus.

## Offene Build-Tests und zusätzliche manuelle Prüfungen

- Windows: tatsächlichen Build starten, MainMenu als erste Szene bestätigen,
  PLAY/Death/Retry/Menu/PLAY prüfen und **EXIT beendet den Prozess** testen.
- WebGL: tatsächlichen Build im Browser öffnen, **EXIT ist ausgeblendet** und
  PLAY/Death/Retry/Menu/PLAY inklusive UI-Navigation prüfen.
- Physische Tastatur/Maus und echten Controller prüfen: Auswahl, D-Pad/Stick,
  Submit, R1/RB, E/A und ohne Loslassen weitergehaltener Bewegungsstick.
- Mehrfach mit gekauften Upgrades sterben; 5/5 HP, Level 1/1/1, EXP/Gold 0 und
  Shop-Bonus 0 nach Retry prüfen. Auch bei offenem Shop/Leveling sterben.
- Game Over auf Plateau länger als beide Spawnintervalle offen lassen; anschließend
  Retry und erneuten Plateau-Eintritt auf frische Gegner und Timer prüfen.
- Tod in Village und Tower sowie Darstellung bei den gewünschten Fenstergrößen
  manuell ansehen. Tower bleibt ohne Phase-3-Bosskampf.

EXIT verwendet außerhalb von Editor/WebGL Application.Quit(). Im WebGL-Player
wird der Button samt Navigation ausgeblendet; im Editor beendet EXIT den Play Mode,
während der Unity-Editor geöffnet bleibt.
Windows-Quit und WebGL-Ausblendung sind implementiert, aber ohne tatsächliche
Build-Ausführung ausdrücklich nicht als erfolgreich getestet dokumentiert.

## Geänderte Dateien

Alle folgenden Pfade liegen unter `Assets/_LEVELS/`, sofern nicht anders angegeben.
Neue Unity-Assets enthalten ihre erforderlichen `.meta`-Dateien.

Neu:

- `Scenes/MainMenu.unity`
- `Prefabs/UI/GameOverScreen.prefab`
- `Art/Fonts/RetroMenu.asset`
- `Scripts/Core/GameFlow.cs`
- `Scripts/UI/MainMenuUI.cs`, `GameOverUI.cs`, `MenuFocus.cs`
- `Editor/M12MenuSetup.cs`, `M12FlowValidation.cs`
- `M12_README.md`

Angepasst:

- `Scenes/Village.unity`, `Plateau.unity`, `Tower.unity`
- `Scripts/Core/GameSession.cs`
- `Scripts/Player/PlayerHealth.cs`, `PlayerAttack.cs`, `PlayerInputReader.cs`
- `Scripts/Enemies/EnemySpawner.cs`, `EnemyAI.cs`, `ContactDamage.cs`,
  `EnemyHealth.cs`, `EnemyReward.cs`
- `Scripts/Scenes/SceneTransitionService.cs`
- `Scripts/UI/ShopMenu.cs`, `LevelingMenu.cs`
- `Editor/LevelsPlayModeSetup.cs`
- `Editor/M11EnemyValidation.cs`: die zwei bisherigen Start-in-Village-Erwartungen
  an den durch M12 bewusst geänderten MainMenu-Start angepasst.
- `ProjectSettings/EditorBuildSettings.asset`

Bereits vor M12 vorhandene fremde Änderungen bleiben erhalten: Löschung von
`Assets/Editor` mit HubForceResolve, Änderung an
`Assets/Settings/InputSystem_Actions.inputactions` und
`ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`.
AGENTS.md bleibt lokal ignoriert und ungestagt.
