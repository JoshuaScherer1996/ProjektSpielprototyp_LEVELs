# M8 – Dynamisches Enemy-Spawnmanagement

## Gespeicherte Einrichtung

`Plateau.unity` enthält unter `Systems/Plateau_EnemySpawner` genau einen
`EnemySpawner` und eine `EnemySpawnArea`. Die feste initiale
Enemy_Basic-Prefab-Instanz wurde entfernt. Auch der erste Goblin entsteht nach
zwei Sekunden an einer zufälligen, vollständig geprüften Position.
Village und Tower erhalten keinen Spawner. Es wurde keine Szene angelegt.

| Inspector-Einstellung | Startwert |
| --- | --- |
| Kategorie / Prefab | Goblin / vorhandenes Enemy_Basic |
| Spawnintervall | 2 skalierte Sekunden |
| MaxAlive | 5 |
| SafeDistance | 3 Einheiten |
| Spawnfläche | Mittelpunkt (0, 0), Größe 22 × 12; x = −11…11, y = −6…6 |
| EntranceClearance | 2 Einheiten um Eintrittspunkte und Portalbounds |
| ColliderPadding | 0,1 Einheiten zusätzlich pro Seite |
| AttemptsPerInterval | 20 |
| BlockingLayers | Default, World, Player, Enemy |

Die grüne Fläche ist mit aktivierten Gizmos in der Scene View sichtbar.
Position, Rotation, Skalierung, Mittelpunkt und Größe sind im Editor editierbar.
Die ausgewählte Spawner-Komponente zeigt im Play Mode die Player-Sperrzone cyan.

## Komponenten und Verhalten

- `EnemySpawner.cs`: zentraler Controller mit serialisierbaren
  `EnemySpawnConfig`-Einträgen. Jeder Eintrag hat eine eigene Kategorie,
  Prefab-Referenz, Obergrenze und Uhr. Vor Ablauf des ersten Intervalls erfolgt
  kein Spawn; je fälligem Intervall entsteht höchstens ein Gegner pro Kategorie.
- `EnemySpawnArea.cs`: editierbare rechteckige Fläche mit Gizmo und Prüfung,
  ob die vollständige Gegnerfläche innerhalb liegt.
- `EnemySpawnIdentity.cs`: stabile Kategorie am Enemy_Basic-Prefab.
  Bereits platzierte und neu instanziierte Gegner werden damit gleich behandelt.
- `EnemyHealth.IsAlive`: berücksichtigt auch noch nicht initialisierte,
  deaktivierte Instanzen. HP = 0 gibt den Platz sofort frei, auch vor Destroy.
- `SceneTransitionService.IsTransitioning`: lesbarer bestehender Ladezustand;
  während eines Portalwechsels ruht der Spawn-Timer.

Die Zählung liest vor einem Spawnversuch die tatsächlichen Szenenobjekte.
Sie umfasst lebende deaktivierte Instanzen, zählt jede Identität einmal und
ignoriert tote oder zerstörte Gegner sowie Gegner anderer Szenen. Es gibt keine
statischen Gegnerlisten, Death-Abonnements oder fortgeschriebenen Zähler.

Die Collider-Abmessungen werden aus dem Prefab einschließlich Offsets,
verschachtelter Transformationen und Skalierung ermittelt. Eine konservative
achsenparallele Hülle mit Padding muss vollständig in der Spawnfläche liegen
und mindestens SafeDistance von der aktuellen Player-Position entfernt sein.
Box-, Circle-, Capsule- und PolygonCollider2D werden unterstützt; unbekannte
feste Colliderformen verhindern den Spawn, statt eine unsichere Größe zu raten.

Physics2D-OverlapBox prüft die Hülle gegen die konfigurierten BlockingLayers.
Portale und alle SpawnPoint-Marker derselben Szene erhalten zusätzliche
Ausschlussbereiche. Trigger blockieren ebenfalls: Die globale
queriesHitTriggers-Einstellung wird für die synchrone Physikabfrage kurz
aktiviert und im finally-Block sofort auf ihren ursprünglichen Wert gesetzt.
Das ist wegen des im installierten Unity 6000.6.2f1 nachgewiesenen globalen
Trigger-Filters auch bei explizitem ContactFilter2D notwendig.

Sind alle 20 Kandidaten ungültig, entfällt der Spawn bis zum nächsten Intervall.
Es gibt keine erzwungene Ersatzposition, Nachholwelle oder Physikabfrage pro
Frame. Ohne Player wird nicht gespawnt. Time.timeScale = 0 pausiert den Timer;
Deaktivierung setzt ihn zurück, Entladen zerstört den Szenencontroller.

Jeder Spawn instanziiert das originale Prefab frisch. EnemyProgression,
EnemyAI, ContactDamage, EnemyHealth, EnemyReward und die Todeseffekte bleiben
zuständig. Dadurch gelten aktuelles EnemyLevel, frische volle HP, skalierter
Schaden sowie skalierte EXP- und Gold-Rewards.

## Vorbereitung für M11

Später kann ein weiterer Konfigurationseintrag einen eigenen Prefab mit einer
eigenen EnemySpawnIdentity-Kategorie, MaxAlive = 2 und eigenem Intervall erhalten.
Die Goblin-Obergrenze bleibt unabhängig bei 5; beide Limits ergeben zusammen 7.
M8 enthält keinen Cyclops-Eintrag und keine Cyclops-Objekte. Beim späteren
Einrichten müssen auch dessen fest platzierte Prefab-Instanzen dieselbe
Kategorie tragen. Neue Klippen müssen auf einem BlockingLayer liegen.

## Reproduzierbare Prüfung

`LEVEL's/M8/Run Spawn Play Mode Validation` führt automatisierte Integrationstests
in den gespeicherten Szenen im echten Unity-Play-Mode aus. Dies ist ein eigenes
Editor-Prüfwerkzeug, kein NUnit-/Unity-Test-Runner-Testpaket. Es benötigt keine
neuen Packages oder Änderungen der Runtime-Assembly-Struktur.

Vor dem Start müssen offene Szenen gespeichert sein. Der Lauf beginnt auf
Plateau, erzeugt Testzustände nur im Play Mode und beendet diesen anschließend.
Ergebnis und Einzelprüfungen: `Library/M8SpawnValidation.json` (Git-ignoriert).
Einige isolierte Prüfungen stoppen vorübergehend die Gegner-AI oder setzen
Inspector-Werte; separate Prüfungen aktivieren und testen AI und Combat.

Prüfumfang:

- gespeicherter Spawner, Prefab-Kategorie, zufälliger Erstspawn und Zählung extern platzierter Gegner;
- 2-Sekunden-Takt, Fünferlimit über mehrere Intervalle, kein Start-Burst;
- zwei Tote werden nacheinander ersetzt, sofortige Freigabe beim Tod;
- einmalige Basis- und skalierte Rewards, externe Zerstörung, deaktivierte Gegner;
- echte Gameplay-Pause, Wiederaufnahme, Spawner deaktivieren/aktivieren;
- bewegte Player-Sperrzone und vollständige Colliderfläche;
- Flächenrand, Weltgrenze, Hindernismitte und nur am Colliderrand auftretende Überlappung;
- andere Gegner, Portal- und Eintrittsbereiche, Trigger bei deaktivierter globaler Suche;
- genau 20 erfolglose Versuche je Intervall, Erholung nach blockierten Positionen;
- aktuelle HP-, Damage-, EXP- und Gold-Skalierung nach Enemy-Level-Kauf;
- Verfolgung, Knockback, Kontakt-Cooldown und Angriff über den echten Player-Hitbox;
- Todessmoke und vollständige Bereinigung;
- echte Portalwechsel Plateau → Village → Plateau mit Loading-Screen;
- frischer Timer nach Rückkehr, keine Spawns in Village/Tower;
- unterschiedliche Erstspawn-Positionen bei erstmaligem Eintritt und nach Plateau-Neuladen;
- fortschreitende Frames, überwachte Spawnabstände und Konsolenfehler.

Tatsächlich ausgeführt am 23.10.2026 mit Unity 6000.6.2f1 über Unity CLI
1.0.0-beta.14:

- abschließende Kompilierung erfolgreich;
- Abschließender Play-Mode-Lauf nach der Erstspawn-Korrektur:
  **53/53 Prüfungen bestanden**, 11 natürliche Spawns überwacht, keine Fehler.
  Erstspawn-Positionen: (4,572; −2,949) beim ersten Eintritt und
  (6,474; −5,107) nach dem Plateau-Neuladen. Bericht:
  `Library/M8SpawnValidation.json`.
- abschließende Konsole: 0 Fehler, 0 Warnungen;
- separate Referenzprüfung aller drei gespeicherten Szenen: keine fehlenden
  Skripte, Spawner nur auf Plateau, keine fest platzierte Gegnerinstanz;
- Prefab-Verweise auf Health, AI, Progression, Reward, ContactDamage,
  Rigidbody und Todessmoke aufgelöst;
- Attack = Gamepad/rightShoulder; Interact = Keyboard/e und
  Gamepad/buttonSouth in allen drei Szenen bestätigt;
- vollständiges Referenzprotokoll: `Library/M8ReferenceValidation.json`.

Es wurden keine funktionalen offenen Fehler festgestellt. Ein früherer
Testlauf deckte die globale Trigger-Filterung auf; die korrigierte Abfrage
einschließlich Wiederherstellung des globalen Werts ist im abschließenden
Lauf erfolgreich geprüft.

`LEVEL's/M8/Configure Plateau Spawner` kann die gespeicherte M8-Konfiguration
erneut herstellen. Es setzt die oben dokumentierten Startwerte und ist kein
Werkzeug zum Bewahren später individuell angepasster Spawnwerte. Feste
Enemy_Basic-Instanzen werden dabei entfernt, damit auch der Erstspawn zufällig ist.

## Manuelle Abnahme

Noch vom Nutzer auszuführen:

1. Plateau öffnen, Gizmos aktivieren und Fläche/Inspectorwerte kontrollieren.
2. Mit Tastatur und Controller umherlaufen; R1/RB-Angriff und kontinuierlich
   gehaltenen Stick auch nach dem Village-Portalwechsel prüfen.
3. Mehrere Gegner töten und das ruhige Nachspawnen im Spielgefühl beurteilen.
4. Während des Laufens die Player-Sperrzone und die freien Portalzugänge beobachten.
5. E/Gamepad-A-Interaktion, Shop/Leveling und den Loading-Screen kurz abnehmen.

Physische Eingabegeräte und subjektives Spielgefühl sind nicht durch die
automatisierten Tests abgedeckt. Ein Standalone-Build wurde nicht erstellt.
Die konservative Collider-Hülle kann sichere, sehr enge Lücken ablehnen;
das ist beabsichtigt und erzeugt keine unsicheren Ersatzspawns.

## Geänderte Milestone-Dateien

Neu, jeweils mit Unity-.meta:

- `Scripts/Enemies/EnemySpawner.cs` (einschließlich EnemySpawnConfig)
- `Scripts/Enemies/EnemySpawnArea.cs`
- `Scripts/Enemies/EnemySpawnIdentity.cs`
- `Editor/M8SpawnSetup.cs`
- `Editor/M8SpawnValidation.cs`
- `M8_README.md`

Angepasst:

- `Scenes/Plateau.unity`: gespeicherter Spawner und Spawnfläche unter Systems,
  feste Erstgegnerinstanz entfernt;
- `Prefabs/Characters/Enemy_Basic.prefab`: Kategorie-Komponente;
- `Scripts/Enemies/EnemyHealth.cs`: lesbarer Alive-Zustand;
- `Scripts/Scenes/SceneTransitionService.cs`: lesbarer Übergangszustand.

Vorhandene fremde Änderungen an InputSystem_Actions, IET-Einstellungen und
Assets/Editor wurden beibehalten. Nichts wurde gestaged, committet oder gepusht.
