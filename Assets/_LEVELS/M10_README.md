# M10 – Health Bars und Floating Damage Numbers

Implementiert am 23.10.2026. Bereit zur manuellen Abnahme. Kein Commit, kein Push, keine Dateien gestagt.

## Dateien

Neu, jeweils mit Unity-Metadatei:

- `Prefabs/UI/EnemyHealthBar.prefab`
- `Prefabs/UI/FloatingDamageNumber.prefab`
- `Scripts/UI/EnemyHealthBar.cs`
- `Scripts/UI/FloatingDamageNumber.cs`
- `Scripts/UI/DamageFeedback.cs`
- `Art/Fonts/RetroDigits.asset` mit eingebetteter SDF-Textur und Outline-Material
- `Editor/M10FeedbackSetup.cs`
- `Editor/M10FeedbackValidation.cs`
- diese Dokumentation

Geändert:

- `Scripts/Enemies/EnemyHealth.cs`: Events `HealthChanged(current, maximum)` und `DamageApplied(actualLoss)`.
- `Scripts/Player/PlayerHealth.cs`: Event `DamageApplied(actualLoss)`; vorhandenes `HealthChanged` bleibt für das HUD erhalten.
- `Prefabs/Characters/Enemy_Basic.prefab`: ein verschachteltes HP-Bar-Prefab und ein `DamageFeedback`.
- `Prefabs/Characters/Player.prefab`: ein `DamageFeedback`.

Zusätzlich wurden die Basisressourcen des bereits installierten TextMeshPro unter `Assets/TextMesh Pro/` importiert. Der Benutzer hat den Unity-Importdialog bestätigt. Keine neuen Packages und keine Änderungen am Package-Manifest. Die alte LiberationSans-FontImporter-Metadatei aus dem TMP-Import wurde über Unity auf Version 4 aktualisiert.

Alle übrigen Pfade in dieser Dokumentation sind relativ zu `Assets/_LEVELS/`.

## Architektur

`EnemyHealth` bleibt die einzige Quelle für aktuelle und maximale Gegner-HP. `TakeDamage` berechnet einmal `min(amount, CurrentHealth)`, zieht genau diesen Wert ab und veröffentlicht ihn als angewendeten Schaden. Nichtpositive Werte und bereits tote Ziele liefern weiterhin `false` und veröffentlichen kein Damage-Event. Rewards, Knockback, Raucheffekt und Zerstörung bleiben im bestehenden Ablauf.

Die HP-Bar ist ein editierbares, verschachteltes Prefab direkt unter dem Gegner. Sie löst ausschließlich ihren eigenen übergeordneten `EnemyHealth` auf, abonniert dessen `HealthChanged` und setzt die Breite des grünen Füllbereichs. Eine Start- und Reaktivierungsabfrage berücksichtigt die Initialisierungsreihenfolge. Änderungen der Max-HP durch `EnemyProgression` senden ebenfalls das Event. Es gibt weder eine globale Enemy-Suche noch kopierte HP-Werte. Der dunkle Hintergrund sitzt 0,64 Welteinheiten über dem Gegner. Bei 0 HP wird das Canvas sofort ausgeblendet; beim Zerstören des Gegners verschwindet das gesamte Kindobjekt.

`DamageFeedback` sitzt einmal auf jedem betroffenen Charakter und abonniert dessen lokales `DamageApplied`. Nur dieses Event erzeugt ein `FloatingDamageNumber`-Prefab. Es gibt keine zusätzliche Anzeige in `PlayerAttack` oder `ContactDamage`, deshalb erzeugen Misses und abgewiesene Angriffe keine Zahl. Die bestehende Mehrfach-Collider-Deduplizierung in `PlayerAttack` bleibt erhalten.

Zahlen erscheinen an der Trefferposition mit konfigurierbarem Höhenversatz. Fünf seitliche Positionen pro Ziel verteilen schnelle Treffer. Enemy-HP-Verlust ist goldgelb, Player-HP-Verlust rot-orange. Das TMP-Material besitzt eine dunkle Kontur. Die eigenen 5x7-Retro-Ziffern decken alle positiven Integer-Schadenswerte ab und benötigen keine externe Schriftdatei.

Das Zahlenobjekt bleibt beim Tod seines Ziels bestehen, gehört aber dessen Szene und wird beim Entladen mit entfernt. Standardmäßig steigt es 0,65 Welteinheiten auf, blendet im letzten Teil aus und zerstört sich nach 0,8 Sekunden. Die Animation nutzt unskalierte Zeit und endet deshalb auch während einer Spielpause. Es gibt keine persistenten Feedback-Manager oder statischen Zielreferenzen.

## Einstellmöglichkeiten

- `EnemyHealthBar.prefab`: Position, Größe, Hintergrund- und Füllfarbe, Canvas-Sortierung.
- `FloatingDamageNumber.prefab`: `Duration`, `Rise Distance`, `Fade Start`, TMP-Schriftgröße und Schriftmaterial.
- `DamageFeedback` auf den Charakter-Prefabs: Zahlen-Prefab, `Offset`, beide Farben, `Horizontal Spread`.
- Vorhandene Sortierlage `Foreground`, keine zusätzlichen Collision- oder Input-Layer. Die Anzeigen blockieren keine UI-Eingaben.

`LEVEL's > M10 > Configure Damage Feedback` ergänzt fehlende Prefabs und Komponenten. Bereits vorhandene Feedback-Prefabs werden dabei nicht neu aufgebaut; manuelle Gestaltung bleibt erhalten. Die originalen Ziffernmuster stehen in `M10FeedbackSetup.cs`.

## Tatsächlich ausgeführte Validierung

Unity 6000.6.2f1, verbundener Editor, echte Play-Mode-Frames. Die folgenden Ergebnisse stammen vom jeweils letzten vollständigen Lauf am 23.10.2026:

| Prüfung | Ergebnis | Bericht im Projekt |
| --- | --- | --- |
| M10 Feedback-Integration | 39 bestanden, 0 Fehler; Frames 1–14534 | `Library/M10FeedbackValidation.json` |
| Bestehende M8 Spawner-/Kampf-Regression | 53 bestanden, 0 Fehler; 11 Spawns beobachtet | `Library/M8SpawnValidation.json` |
| Bestehende M9 gespeicherte Welt und Referenzen | 32 bestanden, 0 Fehler | `Library/M9SavedWorldValidation.json` |
| Bestehende M9 Play-Mode-/Controller-Regression | 62 bestanden, 0 Fehler | `Library/M9WorldValidation.json` |
| Unity-Kompilierung | keine Compilerfehler oder Compilerwarnungen | Unity CLI / Console |
| Gespeicherte Charakter-Prefabs | je ein Feedback, genau eine verschachtelte Enemy-Bar, keine fehlenden Skripte | zusätzliche Editor-Abfrage |
| Font/Material | alle Integer-Ziffern vorhanden, Shader unterstützt | zusätzliche Editor-Abfrage |
| Visuelle Kontrolle | Gold und Rot-orange, Retro-Ziffern, Kontur und teilgefüllte Bar geprüft | `Library/M10Feedback.png` |

M10 prüft natürliche fünf Goblin-Spawns, individuelle Bindungen und Positionen, volle und reduzierte Bars, Max-HP-Skalierung, echten Schwertangriff gegen zwei Gegner, zusätzliches Collider-Ziel, 3 nominellen Schaden gegen 1 Rest-HP, abgewiesenen Schaden, Misses, Cooldown, einmalige EXP/Gold-Rewards, Raucheffekt, Lebensdauer und Aufwärtsbewegung, Reaktivierung ohne doppelte Abonnements, echte ContactDamage-Kollision, Farben, fünf schnelle Player-Treffer, Pausen-Cleanup, tödlichen Player-Schaden und Szenen-Cleanup. Der Kontakt-Test isoliert vorbereitende Player-Kollisionen ausdrücklich, da Unity Collision-Callbacks auch an deaktivierte MonoBehaviours sendet.

Die M8-/M9-Regressionen prüfen zusätzlich Nachspawns, Safe Zone, Kollisionsflächen, Chase-AI, Knockback, ContactDamage-Cooldown, Levelwerte, Schwert-Hitbox, alle vier Szenenübergänge, Ladebildschirm, gehaltenen Stick über Szenenwechsel, RB/R1 Attack, E/Gamepad-A Interact und die weiterhin gesperrte Boss-Tür.

Die Controller-Tests verwenden virtuelle Unity-Input-System-Geräte. Sie ersetzen keine manuelle Prüfung mit physischem Controller. Frühe M10-Testläufe enthielten Testaufbaufehler bei temporären Damage-Overrides bzw. vorbereitenden Kollisionen; der finale 39-Prüfungen-Lauf verwendet echtes Player-Leveling und isolierte Testkollisionen.

Abschließender Console-Status: 0 Fehler, 0 Warnungen in der Unity Console, keine fehlgeschlagene Kompilierung. Der Pipeline-Verlauf enthält noch die historischen FontImporter-Warnungen vom TMP-Import; das betroffene Importformat wurde anschließend aktualisiert.

## Manuelle Abnahme

1. `Plateau` öffnen und Play starten. Mindestens zehn Sekunden warten: bis zu fünf Goblins, jeder mit eigener grüner HP-Bar über der Figur. Bewegung und Verfolgung beobachten.
2. Mit Schwert bzw. RB/R1 angreifen. HP-Füllung, goldene Zahlen, Blickrichtung, Cooldown und Knockback prüfen. Ins Leere angreifen: keine Zahl.
3. Zwei Goblins mit einem Schwung treffen: zwei eigene Zahlen und getrennt sinkende Bars. Kontakt mit mehreren Goblins: rot-orange Zahlen am Player, passender Verlust im HUD.
4. Einen Gegner töten: Bar sofort weg, letzte Zahl endet nach ungefähr 0,8 Sekunden, Rauch bleibt vorhanden; EXP/Gold steigen genau einmal und der Spawner ersetzt den Gegner weiter intervallweise.
5. Player im Village auf Level 3 bringen, Gegner auf Level 1 belassen: der Player verursacht 2 Schaden. Einen frischen Goblin mit 3 HP zweimal treffen: Zahlen 2 und anschließend 1.
6. Enemy-Level am Brunnen steigern und zum Plateau zurückkehren: neu gespawnte Goblins haben die skalierten Max-HP, starten mit voller Bar und zeigen den passenden prozentualen Verlust.
7. Village–Plateau–Tower und zurück wechseln; Stick über den Wechsel halten. Ladebildschirm, E/Gamepad-A, verschlossene Boss-Tür und Abwesenheit zurückbleibender Bars/Zahlen kontrollieren.
8. Mit dem eigenen Controller und der üblichen Spielauflösung Lesbarkeit sowie die leere Fehlerliste in der Console prüfen.

Wiederholbarer M10-Test: `LEVEL's > M10 > Run Feedback Play Mode Validation`. Vorher Szenen speichern und Play Mode verlassen. Der Test verändert nur den temporären Play-Mode-Zustand und beendet ihn selbst.

## Bekannte Probleme und verbleibende Prüfungen

Keine bekannten funktionalen M10-Probleme nach den genannten Tests. Persönliche Lesbarkeitsabnahme bei normalem Spieltempo und der physische Controller-Test stehen aus. Die Schrift ist bewusst auf Ziffern für positive Ganzzahl-Schäden begrenzt.

Die vor M10 vorhandenen Input-/Tutorial-Einstellungsänderungen und gelöschten `Assets/Editor/HubForceResolve`-Dateien wurden erhalten. Szenendateien, Kampf-Input, Spawner-Konfiguration und Package-Manifest wurden für M10 nicht geändert. Kein Phase-3-Boss-Gameplay implementiert.
