# M11 — Goblin, Cyclops und unabhängiges Enemy-Leveling

Implementiert am 23.10.2026 für Unity 6000.6.2f1. Noch nicht manuell abgenommen.
Kein Commit, kein Push und keine Dateien gestagt.

## Prefabs und Basiswerte

| Typ | Prefab unter `Prefabs/Characters/` | HP | Kontaktschaden | EXP | Gold |
| --- | --- | ---: | ---: | ---: | ---: |
| Goblin | `Enemy_Goblin.prefab` | 3 | 1 | 10 | 5 |
| Cyclops | `Enemy_Cyclops.prefab` | 8 | 3 | 25 | 15 |

`Enemy_Basic.prefab` wurde über Unity `AssetDatabase.MoveAsset` umbenannt.
Seine ursprüngliche GUID `ab0967e253465da43a841bc0b15404c2` und seine lokalen
Komponenten-IDs bleiben erhalten. Die vorhandene Spawner-Referenz bleibt gültig.
Der Goblin behält sein rotes Rechteck, seine Größe von 0,7 × 0,8 und seine
bisherige Geschwindigkeit von 2,5 sowie die reichweitenabhängige AI.

`Enemy_Cyclops.prefab` ist ein eigenes editierbares Prefab, keine Variante.
Es verwendet einen braunen Körper von 1,2 × 1,45 mit gleich großem BoxCollider2D.
`Eye` und `Pupil` sind gespeicherte SpriteRenderer-Children mit dem vorhandenen
Circle-Sprite: ein großes weißes Auge und eine kleinere dunkle Pupille.
Körper, Auge, Collider, Basiswerte und AI sind im Inspector bearbeitbar.

Beide Typen verwenden die vorhandenen Komponenten für Health, ContactDamage,
Knockback, Reward, Death Smoke, Health Bar und Damage Numbers. Der Cyclops
erbt dasselbe verschachtelte Health-Bar-Prefab mit höherem Abstand zum Körper.
`ContactDamage` ignoriert deaktivierte Komponenten und bereits tote Gegner,
auch wenn Unity noch einen Physik-Callback zustellt.

## AI

`EnemyAI` bietet jetzt konfigurierbare Optionen `persistentAggro`,
`usePlayerSpeed` und `playerSpeedRatio`. Beim Cyclops sind die ersten beiden
aktiv. Die aktuelle Speedvorgabe beträgt 1,4 Einheiten pro Sekunde. Bei einer
Player-Speed von 5 wird der passende Faktor 0,28 verwendet. Der Cyclops bleibt
damit langsamer als der Goblin mit 2,5. Änderungen der Player-Speed werden
berücksichtigt; der Cyclops-Faktor wird beim erneuten Konfigurieren aus der
Speedvorgabe und der Player-Speed berechnet.
Der Erkennungsradius bleibt im Inspector einstellbar; Standard ist 4.

Nach der ersten Erkennung verfolgt der Cyclops den Player auch außerhalb des
Radius. Knockback unterbricht die Bewegung kurz, ohne die Aggro zu löschen.
Tod des Cyclops oder des Players stoppt die Verfolgung. Beim Szenenwechsel
werden die szeneneigenen Gegner entladen. Bewegung erfolgt weiterhin über den
dynamischen Rigidbody2D und die vorhandenen World-Collider. Die bestehenden
North/SouthEnemyLimit-Collider schließen die Portalöffnungen für Gegner;
ScenePortal akzeptiert ausschließlich Player. Es wurde kein Pathfinding ergänzt.

## Getrennte Level und Progression

`GameSession.GoblinLevel` liest bewusst den vorhandenen `EnemyLevel`-Speicher.
So wird der bisherige globale Gegnerwert ohne Kopieren oder Zurücksetzen zum
Goblin-Level. `EnemyLevel`, `NextEnemyLevelExperience` und `TryLevelUpEnemies`
bleiben als Goblin-kompatible API verfügbar. `CyclopsLevel` besitzt eigenen
Speicher und startet bei 1. Beide Gegnerlevel sowie das Player-Level bleiben
in derselben GameSession bei Szenenwechseln erhalten. Ein neuer Run startet
weiterhin bei Level 1; M11 ergänzt kein Savegame über Spielstarts hinweg.

Jedes `EnemyProgression` hat einen gespeicherten `EnemyType`. Im einmaligen
Start übernimmt es den jeweiligen aktuellen Level und berechnet Werte aus den
unveränderten Basiswerten. Laufende Gegner werden durch einen Kauf nicht
rückwirkend verstärkt; neue Spawns verwenden den neuen Wert. Deaktivieren und
erneutes Aktivieren wendet keinen zweiten Multiplikator an.

Alle Einstellungen liegen im vorhandenen `Resources/LevelsProgression.asset`.
Die bisherigen `enemy...`-Felder bleiben die Goblin-Konfiguration. Eigene
`cyclops...`-Felder steuern Cyclops-Kosten, HP, Schaden, EXP und Gold unabhängig.

| Standard-Skalierung | Goblin | Cyclops |
| --- | --- | --- |
| Max HP je weiterem Level | +1 | +2 |
| Schaden je zwei weiteren Leveln | +1 | +1 |
| EXP-Reward je weiterem Level | +5 | +10 |
| Gold je weiterem Level | +2 | +5 |
| Zusätzlicher Goldbonus je zwei weiteren Leveln | +1 | +1 |
| Erster Levelkauf / Kostenanstieg | 20 / +20 EXP | 20 / +20 EXP |

Beispiel: Goblin-Level 3 ergibt 5 HP, 2 Schaden, 20 EXP und 10 Gold;
Cyclops-Level 1 bleibt bei 8 HP, 3 Schaden, 25 EXP und 15 Gold.
Cyclops-Level 2 ergibt 10 HP, 3 Schaden, 35 EXP und 20 Gold.
Player-Kosten und -Skalierung sind unverändert. Berechnungen sättigen bei
`int.MaxValue`; Käufe prüfen verfügbare EXP und belasten sie genau einmal vor
der Änderungsbenachrichtigung. Wiedereintretende Levelkäufe werden abgelehnt.

## Brunnen und HUD

Das vorhandene LevelingMenu-Prefab wurde erweitert und neu angeordnet:

1. LEVEL UP PLAYER
2. LEVEL UP GOBLIN
3. LEVEL UP CYCLOPS
4. CLOSE

Das Menü zeigt alle drei Level, verfügbare EXP und die drei aktuellen Kosten.
Fehlende EXP erzeugen eine Meldung. Es nutzt dieselben UI Input Actions und
explizite zyklische Navigation für Tastatur und Gamepad sowie Maus-Buttons.
Ein zusätzlicher Schutz verhindert mehrere Kaufcallbacks im selben Frame.
Der Brunnen öffnet weiterhin per E oder Gamepad-A. Schließen stellt Bewegung
und Kampf wieder her. Das HUD zeigt jetzt beide Gegnerlevel.

## Spawning

Der vorhandene M8-Spawner bleibt der gemeinsame Controller auf dem Plateau.
Es wurden ausschließlich zwei gespeicherte Kategorien konfiguriert:

| Kategorie | MaxAlive | SpawnInterval |
| --- | ---: | ---: |
| Goblin | 5 | 2 Sekunden |
| Cyclops | 2 | 3 Sekunden |

Der erste Goblin erscheint nach 2 Sekunden, der erste Cyclops nach 3 Sekunden.
Die unterschiedlichen Takte laufen unabhängig; bei gemeinsamen Vielfachen
können beide Kategorien gleichzeitig spawnen. Jede Kategorie erzeugt pro abgelaufenem
Intervall höchstens eine Instanz. Insgesamt leben höchstens sieben Gegner.
Die tatsächlichen lebenden EnemySpawnIdentity-Objekte werden pro Kategorie
gezählt; Tod gibt damit den passenden Platz ohne doppelte Zähler frei.
Respawns folgen den laufenden Kategorieintervallen und erfolgen nicht direkt
im Tod-Callback. Es gibt keinen Nachhol-Burst bei verzögerten Frames.

Beide Typen verwenden dieselbe zufällige Spawn Area mit mindestens 3 Einheiten
Abstand zwischen gepolstertem Gegner-Collider und Player-Position. Die
vorhandene Spawnprüfung berechnet die tatsächliche Collider-Größe einschließlich
Transforms und 0,1 Einheiten Padding. Dadurch kann eine Goblin-Position für
den größeren Cyclops ungültig sein. World-Collider, Klippen, Tore, Trigger,
Portale, Eingänge und andere Gegner werden ausgeschlossen. Nach 20 erfolglosen
Versuchen wird das Intervall ohne Spawn beendet.

## Wiederholbare Prüfung und Nachweise

Die neuen Editor-Menüs sind unter `LEVEL's/M11` verfügbar:

- `Validate Saved Enemy Assets`: gespeicherte Prefabs, GUID, Werte, Komponenten,
  Navigation und Szenenreferenzen.
- `Run Enemy Play Mode Validation`: echte Spieler-, Input-, Physik-, Spawn-,
  Reward-, Kauf- und Szenenwechselpfade. Testobjekte und Eingabegeräte bleiben
  auf den Testlauf begrenzt. Ergebnis unter `Library/M11EnemyValidation.json`.
- `Configure Enemy Types`: bewusster Editor-Setup-Aufruf zum erneuten Setzen der
  M11-Standardkonfiguration. Nicht zum Beibehalten eigener späterer Tuningwerte
  erneut ausführen.

Tatsächlich ausgeführte Prüfungen, jeweils mit bestandenem Abschlussbericht:

| Prüfung | Bestandene Checks | Nachweis |
| --- | ---: | --- |
| M11 gespeicherte Assets nach Abnahmekorrektur | 25 | `Library/M11SavedAssetsValidation.json` |
| M11 Play Mode nach Abnahmekorrektur | 129 | `Library/M11EnemyValidation.json` |
| M9 gespeicherte Welt, vor Abnahmekorrektur | 32 | `Library/M9SavedWorldValidation.json` |
| M9 Welt-/Input-/Szenenwechsel, vor Abnahmekorrektur | 62 | `Library/M9WorldValidation.json` |
| M10 Kampf-/Feedback, vor Abnahmekorrektur | 39 | `Library/M10FeedbackValidation.json` |

In der Abnahmekorrektur wurden 154 M11-Checks erneut erfolgreich ausgeführt.
Die M9-/M10-Berichte stammen aus dem vorangegangenen M11-Durchlauf und wurden
in dieser Korrekturrunde nicht erneut ausgeführt. Der letzte M11-Lauf ging von
Frame 1 bis 31544, beobachtete 8 reguläre Goblin- und 5 Cyclops-Spawns über die
einzelnen Testphasen und maß mindestens 3,88226 Einheiten gepolsterten
Player-Abstand. Die Startpopulation wurde mit 5 + 2 Gegnern und den jeweils
gespeicherten Intervallen geprüft. Zur Kontrolle unabhängiger
Uhren wurden während einer weiteren Testphase ausschließlich im Play Mode
unterschiedliche Intervalle von 0,4 und 0,8 Sekunden gesetzt und danach entfernt.

Die erste Korrekturrunde reduzierte die Cyclops-Speed von 3,25 auf 2,1667.
Auf erneute Anforderung wurde sie auf 1,0833 halbiert. Der aktuelle Wunsch
setzt sie auf 1,4 Einheiten pro Sekunde. Die Play-Mode-Suite wurde nach diesen
drei aufeinanderfolgenden Speed-Anpassungen nicht erneut ausgeführt.

Die Korrekturrunde bestätigt zusätzlich: nach 2,2 Sekunden existiert ein Goblin
und noch kein Cyclops; nach 3,2 Sekunden ist der erste Cyclops erschienen.
Der Cyclops-Rigidbody bewegte sich in früheren Läufen mit rund 2,1667 und 1,0833.
Der aktuelle gespeicherte Prefabwert beträgt 1,4 Einheiten pro Sekunde. Ein normaler Play-Start
bei geöffneter Plateau-Szene wurde separat geprüft: tatsächlich aktive Szene
Village, Player in Village bei (0, -4), laufender Playerloop bei Frame 22904.
Nachweis: `Library/M11VillageStartValidation.json`. Nach Testende wird der
Village-Start wiederhergestellt; normale Spiele starten nicht in der Testszene.

Die Tests liefen über Unity CLI 1.0.0-beta.14 im verbundenen Unity-Editor und
realen Playerloop. Der erfolgreiche Updatecheck bestätigte beta.14 als aktuelle
CLI-Version. Es wurde kein zusätzlicher Test-Package installiert. Die M11-Suite
prüfte echte Kontakte mit 1 bzw. 3 Schaden, HP-Balken, Damage Numbers, einmalige
Rewards, Death Smoke, Limits und Respawn, Collider-Ausschlüsse, dauerhafte Aggro,
Knockback, Tod, getrennte Skalierung, reentrante Käufe und Session-Erhalt.
Virtuelle Input-System-Geräte prüften R1/RB gegen beide Gegner, E/Gamepad-A am
echten Brunnen, Käufe per Gamepad/Tastatur/Maus, Navigation und Schließen.
Die M9-Regression bestätigte weiterhin gehaltene Stick-Eingaben über die
Szenenwechsel, den Loading-Screen und die geschlossene Boss-Tür ohne Bosskampf.
Die M10-Regression prüfte zusätzlich Angriffscooldown, Mehrfach-Collider,
Overkill, Farben, unabhängige Zahlen, Effektdauer und vollständige Bereinigung.

Die abschließende Skriptkompilierung meldete keine Fehler oder Warnungen.
Nach den letzten Laufzeittests meldeten sowohl die Unity-Konsole als auch der
Pipeline-Logpuffer 0 Fehler und 0 Warnungen. Die C#-/Markdown-Diffs bestehen
die Whitespace-Prüfung. Unity erzeugt in neuen YAML-Abschnitten standardmäßig
Leerzeichen hinter leeren `m_Name:`-Feldern; diese wurden nicht manuell geändert.

Die aufgenommenen Spielansichten liegen in `Library/M11_Enemies.png` und
`Library/M11_LevelingMenu.png`. Sie wurden visuell auf Darstellung, lesbare
Werte und überlappungsfreie Menükontrollen geprüft. Die Bilder zeigen
Testlevel aus dem Prüflauf, keine gespeicherten Startwerte.

## Geänderte Dateien

Unter `Assets/_LEVELS/`:

- Umbenannt: `Prefabs/Characters/Enemy_Basic.prefab` und `.meta` zu
  `Enemy_Goblin.prefab` und `.meta`.
- Neu: `Prefabs/Characters/Enemy_Cyclops.prefab` und `.meta`.
- Neu: `Scripts/Core/EnemyType.cs`, `Editor/M11EnemySetup.cs`,
  `Editor/M11EnemyValidation.cs`, diese Dokumentation und jeweils `.meta`.
- Runtime: `Scripts/Core/GameSession.cs`, `ProgressionSettings.cs`,
  `Scripts/Enemies/EnemyAI.cs`, `EnemyProgression.cs`, `ContactDamage.cs`,
  `Scripts/UI/LevelingMenu.cs`, `PlayerHealthHUD.cs`.
- Assets: `Resources/LevelsProgression.asset`, `Scenes/Plateau.unity`,
  `Prefabs/UI/LevelingMenu.prefab`, `PlayerHUD.prefab`.
- Bestehende Editor-Helfer: `Editor/M5ProgressionSetup.cs` und `M8SpawnSetup.cs`
  referenzieren den neuen Goblin-Pfad. `M10FeedbackValidation.cs` begrenzt
  ausschließlich während seines eigenen Laufes die Population auf Goblins;
  die gemeinsame M11-Suite prüft beide Typen mit den gespeicherten 5/2-Limits.
- Abnahmekorrektur: `Editor/LevelsPlayModeSetup.cs` und `.meta` legen Village
  als normale Editor-Startszene fest, unabhängig von der gerade bearbeiteten
  Szene. Die Validatoren M8/M9/M10/M11 wählen ihre Testszene ausdrücklich für
  einen Testlauf; anschließend wird wieder Village als Startszene gesetzt.
  Die erste aktivierte Build-Szene bleibt unverändert Village.

Die bereits vor M11 vorhandenen Löschungen unter `Assets/Editor`, Änderungen
an `Assets/Settings/InputSystem_Actions.inputactions` und an den Learn-Settings
gehören nicht zu M11 und wurden erhalten. `AGENTS.md` wurde nicht gestagt.

## Manuelle Abnahme

Noch vom Benutzer auszuführen:

- In einem normalen Run Village → Plateau spielen und die Gegner beobachten.
- Cyclops erkennen lassen, weglaufen, an Klippen/Portalöffnungen vorbeilaufen;
  dauerhafte Verfolgung, Geschwindigkeit und Kollisionsverhalten beurteilen.
- Mit physischem Controller und Maus/Tastatur kämpfen und alle drei
  Brunnenkäufe ausprobieren; Lesbarkeit und gewünschtes Spielgefühl prüfen.
- Getrennte Level erhöhen, neue Spawns abwarten, zum Shop zurückkehren und
  nach einem Szenenwechsel die Level und Ressourcen vergleichen.

Diese manuelle Abnahme wurde nicht vorweggenommen. Kein Bosskampf ergänzt.

## Bekannte Probleme und Grenzen

In den ausgeführten Prüfungen sind keine offenen Laufzeit- oder Referenzfehler
verblieben. Physischer Controller, subjektives Balancing und längeres normales
Spielen wurden nicht manuell getestet. Die AI kann erwartungsgemäß an Hindernissen
anstehen, da kein Pathfinding vorgesehen ist. Die alten M8-spezifischen
Ein-Kategorie-Testannahmen werden für M11 durch die neue Zwei-Kategorien-Suite
abgedeckt; der historische M8-Validator wurde nicht als M11-Test ausgeführt.

Der Editor wurde nach den Tests im Edit-Modus mit geöffneter Village-Szene
zurückgelassen. Die nur für
die Tests aktivierte Hintergrundausführung wird auf den ursprünglichen Wert
zurückgestellt. Abnahme durch den Benutzer steht aus.
