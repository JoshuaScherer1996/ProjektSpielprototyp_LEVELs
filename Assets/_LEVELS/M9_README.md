# M9 – World Design und sichtbare Szenenübergänge

Stand: 23.10.2026, Unity 6000.6.2f1 / Unity CLI 1.0.0-beta.14.
Umgesetzt im verbundenen Unity-Editor. Kein Commit, kein Push, nichts gestagt.

## Gespeicherte Weltgestaltung

Alle festen Weltobjekte sind gespeicherte, editierbare Unity-Objekte beziehungsweise
Prefab-Instanzen. Zur Laufzeit wird keine feste Weltgeometrie erzeugt.

Neue Sprites in `Assets/_LEVELS/Art/Sprites/`:

| Asset | Verwendung |
| --- | --- |
| `M9_StoneGate.png` | Steinbogen mit zwei Pfosten und offenem Durchgang |
| `M9_RoundTower.png` | Abgerundete dunkelgraue Turmfassade mit Eingang und Schlitzen |
| `M9_BrownCliff.png` | Braune Felsflächen mit gestuften Kanten und unregelmäßigen Rissen |
| `M9_ExitLight.png` | Halbtransparenter, pixelig abgestufter Lichtkegel |

Die neuen Texturen verwenden Point-Filterung, 16 Pixel pro Einheit, keine Mipmaps
und keine Texturkompression. Die bestehenden Square-Sprites und das vorhandene
GreyboxUnlit-Material werden für Wegflächen und Schwellen wiederverwendet.

Neue Prefabs in `Assets/_LEVELS/Prefabs/Environment/`:

- `VillageGate.prefab`: Torbogen, zwei feste Pfosten, editierbares Zielschild;
  in Village eingesetzt. Am südlichen Plateau-Ausgang bleiben nur das Zielschild
  und der separate Weg aktiv; Bogen und Pfosten sind dort per Szenen-Override deaktiviert.
- `TowerEntrance.prefab`: abgerundete Turmfassade und zwei seitliche Steinwände;
  am nördlichen Plateau-Ausgang eingesetzt.
- `PlateauCliffs.prefab`: sechs sichtbare Klippensegmente und zwei nur für
  Nicht-Player geltende Grenzen hinter den Portal-Durchgängen.
- `TowerExit.prefab`: helle Schwelle und einfallender Lichtsprite ohne Torbogen oder Pfosten;
  am südlichen Tower-Ausgang eingesetzt.

`LockedBossDoor.prefab` wurde um Interaktionslogik, einen Reichweitenhinweis
und die gespeicherte Nachrichten-UI ergänzt. Der ursprüngliche blockierende
Tür-Collider bleibt erhalten.

## Geänderte Szenen und Collider

| Szene | Änderungen |
| --- | --- |
| Village | Nördliches Tor, beschrifteter Weg, geteilte nördliche Weltgrenze |
| Plateau | Offener südlicher Rückweg zum Village, nördlicher Tower-Eingang, Klippenrahmen, Wege, kleinere Spawnfläche |
| Tower | Südlicher Ausgang mit Licht und Weg, geteilte südliche Weltgrenze; interaktive Bosstür über das vorhandene Prefab |

Es bleiben exakt die vier vorhandenen ScenePortal-Komponenten erhalten:

| Übergang | Ziel-SpawnPoint |
| --- | --- |
| Village → Plateau | `Spawn_FromVillage` |
| Plateau → Village | `Spawn_FromPlateau` |
| Plateau → Tower | `Spawn_FromPlateau` |
| Tower → Plateau | `Spawn_FromTower` |

Die ursprünglichen Portal-Rechtecke sind als Grafik deaktiviert; ihre Trigger,
Positionen, Ziele und Service-Verweise bleiben erhalten. Die neuen Welt-Prefabs
enthalten keine zusätzlichen Transition-Trigger.

- Alle festen Hindernisse verwenden den vorhandenen Physics-Layer `World`.
- Torpfosten: zwei BoxCollider2D mit freier Mitte von drei Einheiten Breite.
  Der sichtbare Bogen liegt auf `Foreground`; seine Überkopf-Geometrie hat
  keinen quer durch den Laufweg gelegten Player-Collider. Am Plateau-Rückweg
  und Tower-Ausgang gibt es keine aktiven Torpfosten-Collider und keinen Bogen.
- Turm: zwei PolygonCollider2D entlang der abgerundeten seitlichen Wände.
- Klippen: sechs BoxCollider2D. Innenkanten bleiben bei x = ±12 und y = ±7;
  die sichtbaren Felswände erstrecken sich nach außen. Die Ecken überlappen,
  die nördliche und südliche Öffnung sind jeweils drei Einheiten breit.
- Zwei zusätzliche BoxCollider2D schließen diese Öffnungen ausschließlich
  für Nicht-Player am äußeren Rand: `NorthEnemyLimit` und `SouthEnemyLimit`.
  `excludeLayers = Player` lässt den Player und seine Attack-Hitbox frei
  passieren. Dadurch können Goblins auch durch die Portalöffnungen nicht
  aus der Welt geschoben werden. ScenePortal selbst akzeptiert nur Player.
- Ground, Props und Foreground sind die vorhandenen Sorting Layers.
- Der Lichtkegel hat keinen Collider und benötigt keine neue URP-Einstellung.

## Spawnmanagement

Der bestehende EnemySpawner wurde nicht umgebaut. Seine Laufzeit-Skripte und
Hindernis-LayerMask bleiben unverändert. Die SpawnArea ist jetzt **22 × 9**
Einheiten um (0, 0): x = −11…11, y = −4,5…4,5. Zuvor waren es 22 × 12.
Die kleinere rechteckige Fläche liegt vollständig auf freiem Boden und schneidet
keine Torpfosten. Die vollständige Goblin-Colliderfläche einschließlich Padding
muss weiterhin innerhalb liegen.

Unverändert: maximal fünf lebende Goblins, Zwei-Sekunden-Intervall,
Player-SafeDistance = 3, EntranceClearance = 2, ColliderPadding = 0,1,
maximal 20 Kandidaten pro Intervall, BlockingLayers = Default / World / Player /
Enemy. Portal- und SpawnPoint-Sperrbereiche bleiben aktiv.

Die bestehende M8-Validierung wurde auf die neue gespeicherte Flächengröße
angepasst. Nach ihrem Test mit vergrößerter Fläche stellt sie nun die zuvor
gelesene Originalgröße wieder her. `M8SpawnSetup` bleibt ein Werkzeug zum
Wiederherstellen der alten M8-Konfiguration; danach M9-Authoring erneut ausführen,
wenn die M9-Spawnfläche wiederhergestellt werden soll.

## Bosstür

`LockedBossDoorInteraction.cs` erweitert die vorhandene StationInteractionSelector-
Auswahl. E beziehungsweise Gamepad A öffnet die Nachricht in Reichweite;
konfigurierbar sind Reichweite (1,25 Einheiten Abstand der Collider) und
Anzeigedauer (vier Sekunden).

Ein Linksklick auf die sichtbare Tür kann die Nachricht aus jeder Bildschirm-
Entfernung anzeigen. Der bestehende PlayerAttack-Callback prüft den Türklick
synchron vor dem Angriff: Es entsteht dabei weder ein Slash noch ein Cooldown.
UI-Treffer werden berücksichtigt. Linksklick außerhalb der Tür und RB/R1
lösen weiterhin normale Angriffe aus.

Die Meldung lautet exakt:

> Der Boss schläft noch.
> Komm in Phase 3 wieder!

Sie schließt nach vier unskalierten Sekunden, mit Esc oder mit erneutem E/A
in Reichweite. Der dunkle Hintergrund und die große helle Schrift sind als
uGUI-Objekte im Tür-Prefab gespeichert. Verwendet wird die vorhandene UI-Schrift
in Fettdruck; eine neue externe Retro-Schrift wurde nicht eingebunden.
Es gibt keinen Bosskampf, keine Türöffnung und keinen Szenenladeaufruf der Tür.

## Tatsächlich durchgeführte Prüfungen

Automatisiert im laufenden Unity-Editor, mit fortschreitenden Frames:

- **32/32 Prüfungen gespeicherter Szenen bestanden**: fehlende Skripte,
  Player-/Portal-Anzahl, Loading-Verweise, SpawnPoints, vollständige Player-
  Durchgänge, Collider-Ecken und -Kanten, Spawnflächenboden, LayerMasks,
  Bosstür-UI und unveränderte Spawnparameter.
- **62/62 M9-Play-Mode-Prüfungen bestanden**: alle vier Übergänge über echte
  Triggerkontakte, korrekte Zielpositionen, genau ein Player, Lade-Overlay,
  mindestens zwei Sekunden Ladezeit, gesperrte Steuerung während des Ladens,
  gehaltener Controller-Stick nach jedem Wechsel und keine Rücksprungschleifen.
- 4.361 Positionen durch die tatsächliche M8-Footprint-Prüfung getestet:
  2.349 gültige Bodenpositionen und 2.012 Ausschlüsse; keine akzeptierte
  Goblinfläche schneidet Klippen, Tore oder Portale.
- Reale Rigidbody-Kollisionen für Player gegen Klippe und Turmwand sowie für
  Goblins gegen Klippe und beide offenen Portalränder geprüft.
- Bosstür mit synthetischen Input-System-Geräten geprüft: E innerhalb und
  außerhalb der Reichweite, wiederholtes E/A, Mausklick ohne Angriff/Cooldown,
  Esc, automatisches Schließen, wiederholter Türklick, normaler Linksklick
  daneben, RB/R1 sowie blockierende Tür ohne Szenenwechsel.
- GameSession-Identität und exakte Gold-/EXP-Werte über die Szenenwechsel geprüft;
  Leveling-Brunnen und Shop nach Rückkehr vorhanden.
- **53/53 M8-Regressionsprüfungen bestanden**: natürliche Spawnabstände,
  Fünferlimit, Nachbesetzen, Pause, Safe Zone an wechselnden Player-Positionen,
  Hindernis- und Triggerprüfung, SpawnPoint-/Portal-Abstände, begrenzte
  Fehlversuche, Enemy-Leveling, HP, Rewards, AI, Knockback, ContactDamage,
  PlayerAttack mit echter Hitbox, Todeseffekt und Entladen/Neuladen.
  Im abschließenden Lauf wurden elf natürliche Spawns beobachtet; die kleinste
  gemessene Player-Distanz der gepaddeten Goblinfläche war 3,235 Einheiten
  bei einer vorgeschriebenen SafeDistance von 3.
- Kompilierung ohne Fehler oder Warnungen; abschließende Unity-Konsole geprüft.

Ergebnisdateien unter `Library/` (generierte, nicht versionierte Testausgaben):

- `M9SavedWorldValidation.json`
- `M9WorldValidation.json`
- `M8SpawnValidation.json`
- `M9_Village.png`, `M9_Plateau.png`, `M9_Tower.png`, `M9_BossMessage.png`

Die vier Szenen-/UI-Aufnahmen wurden visuell angesehen. Die Übersichtsaufnahmen
verwenden ausschließlich während des Tests eine weiter herausgezoomte Kamera;
die gespeicherte CameraFollow-Konfiguration wird dadurch nicht verändert.

Wiederholbare Editor-Menüs:

- `LEVEL's/M9/Validate Saved World`
- `LEVEL's/M9/Run World Play Mode Validation`
- `LEVEL's/M8/Run Spawn Play Mode Validation`
- `LEVEL's/M9/Author World Design` schreibt die M9-Standardgestaltung erneut.
  Dieses explizite Authoring-Werkzeug ist zum Wiederherstellen der Standardwerte
  gedacht; individuell veränderte M9-Prefabs vorher sichern.
- `LEVEL's/M9/Open Return Exits` entfernt gezielt den Tower-Ausgangsbogen und
  deaktiviert Bogen/Pfosten nur an der Plateau-Rückweg-Instanz. Boden, Licht,
  Schilder und Portal-Trigger bleiben erhalten. Auch das vollständige M9-Authoring
  berücksichtigt diese Nachbesserung.

Weitere geänderte beziehungsweise neue Skripte:

- `Scripts/Player/LockedBossDoorInteraction.cs`: Nachrichteninteraktion der Tür.
- `Scripts/Player/PlayerAttack.cs`: zwei Zeilen zur Priorisierung des Türklicks.
- `Editor/M9WorldDesignSetup.cs`: explizites Speichern von Sprites, Prefabs und Szenen.
- `Editor/M9WorldValidation.cs`: Szenen- und Play-Mode-Integrationstests.
- `Editor/M8SpawnValidation.cs`: erwartete M9-Flächengröße und Wiederherstellung
  der ursprünglich gelesenen Größe nach dem Test mit vergrößerter Fläche.

Alle neuen importierten Unity-Assets besitzen die zugehörigen `.meta`-Dateien.

## Offene manuelle Abnahme

Keine bekannten funktionalen Fehler aus den abschließenden Prüfungen.
Nicht als manuell gespielt ausgegeben: Die Eingabetests liefen automatisiert
mit Input-System-Testgeräten, nicht mit einem physisch bedienten Controller.

Für die Abnahme bitte in Unity selbst prüfen:

1. Village-Tor, Plateau-Rückweg, Turmform und braune Klippen im gewünschten Stil.
2. Alle vier Übergänge zu Fuß; mit einem echten Controller den Stick während
   des Ladebildschirms weiter halten.
3. An Torpfosten und Klippenecken entlanglaufen; Goblins an beide Ausgänge locken.
4. Tower-Ausgang und Licht bei normaler Spielkamera beurteilen.
5. Bosstür per Mausklick, E und A untersuchen; daneben normale Angriffe auslösen.
6. Lesbarkeit der Nachricht bei der gewünschten Fenstergröße/Auflösung und
   Spielgefühl von Combat, Brunnen und Shop prüfen.

Vor Beginn vorhandene fremde Änderungen an `Assets/Editor*`,
`Assets/Settings/InputSystem_Actions.inputactions` und den IET-Einstellungen
wurden nicht zurückgesetzt oder gestagt. AGENTS.md bleibt lokal/ignoriert.
