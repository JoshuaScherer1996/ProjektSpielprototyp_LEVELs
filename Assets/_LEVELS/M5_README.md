# M5 — Player-Leveling und Enemy-Leveling

## Bedienung und Architektur

Im Village steht rechts neben dem Brunnen eine goldene Leveling-Station bei (3, 0).
Innerhalb von 2 Welteinheiten öffnet die vorhandene `Player/Interact`-Action das Menü:
E oder Y/Dreieck **halten**. Die bestehende Action besitzt bereits eine Hold-Interaction;
das Input-Actions-Asset wird durch M5 nicht verändert.

Mausklick, WASD/Pfeiltasten und Enter sowie Gamepad-Stick/D-Pad und A/Kreuz
bedienen die drei Buttons. Escape, B/Kreis, der Close-Button oder erneutes Interagieren
schließen das Menü. Während es offen ist, sind Bewegung und Angriff gesperrt.
Der Player-Action-Map und die Gerätezuordnung bleiben aktiv, sodass gehaltene
Bewegungseingaben nach dem Schließen und beim Szenenwechsel wieder gelesen werden.

`GameSession` verwaltet Levels, verfügbare und insgesamt verdiente EXP sowie Gold/HP.
Käufe ziehen EXP ab und erhöhen genau ein Level vor der Benachrichtigung der UI.
Ein Schutz verhindert reentrante Käufe während derselben Benachrichtigung.
Rewards lösen keine automatischen Level-ups aus. Gesamte EXP werden beim Kauf nicht reduziert.
Ein neuer Play-Mode-Start setzt die Session zurück, auch ohne Domain Reload.
Szenenwechsel behalten die Session. Es gibt keine Speicherung auf Festplatte.

`PlayerProgression` berechnet Stats aus den unveränderten Basiswerten des Player-Prefabs.
`EnemyProgression` berechnet normale Gegnerwerte einmal beim Initialisieren aus Basiswerten
und dem globalen EnemyLevel. Bereits lebende Gegner werden nicht nachträglich verstärkt;
beim nächsten Betreten des Plateau gilt das neue EnemyLevel.
Health, Angriff, Kontaktschaden und einmalige Rewards bleiben eigenständige Komponenten.

## Konfigurierbares Balancing

Alle Parameter: `Assets/_LEVELS/Resources/LevelsProgression.asset`.
`L` ist das jeweilige aktuelle Level, beginnend bei 1; `floor` rundet ab.

| Wert | Standardformel |
| --- | --- |
| Kosten für nächsten Player-Level | `20 + 20 * (L - 1)` EXP |
| Kosten für nächsten Enemy-Level | `20 + 20 * (L - 1)` EXP |
| Player-Max-HP | `5 + 2 * (L - 1)` |
| Player-Schaden | `1 + floor((L - 1) / 2)` |
| Enemy-Max-HP | `3 + (L - 1)` |
| Enemy-Kontaktschaden | `1 + floor((L - 1) / 2)` |
| Enemy-EXP | `10 + 5 * (L - 1)` |
| Enemy-Gold | `5 + 2 * (L - 1) + floor((L - 1) / 2)` |

Basiswerte für HP, Schaden und Rewards stammen weiterhin aus den jeweiligen Prefab-Komponenten.
Die Zahlen oben zeigen die vorhandenen Basiswerte. Inkremente und Level-Abstände sind konfigurierbar.
Berechnungen sättigen bei `int.MaxValue`, Levels werden niemals unter 1 gesetzt.
Bei unzureichender EXP scheitert der Kauf ohne Abbuchung und das Menü zeigt eine Rückmeldung.

**HP-Regel:** Ein höheres Maximum lässt die absoluten aktuellen HP unverändert.
Beispiel: 3/5 HP werden beim Upgrade zu 3/7 HP. Ein Szenenwechsel heilt ebenfalls nicht.

| Enemy-Level | HP | Schaden | EXP | Gold |
| --- | --- | --- | --- | --- |
| 1 | 3 | 1 | 10 | 5 |
| 2 | 4 | 1 | 15 | 7 |
| 3 | 5 | 2 | 20 | 10 |

## Dateien und Editor-Integration

Neue Scripts mit zugehörigen `.meta`-Dateien:

- `Scripts/Core/ProgressionSettings.cs`
- `Scripts/Player/PlayerProgression.cs`
- `Scripts/Enemies/EnemyProgression.cs`
- `Scripts/Player/LevelingStation.cs`
- `Scripts/UI/LevelingMenu.cs`
- `Editor/M5ProgressionSetup.cs`

Neue Assets mit `.meta`-Dateien:

- `Resources/LevelsProgression.asset` und `Resources.meta`
- `Prefabs/Environment/LevelingStation.prefab`
- `Prefabs/UI/LevelingMenu.prefab`
- diese Dokumentation

Erweitert:

- `Scripts/Core/GameSession.cs`
- `Scripts/Player/PlayerHealth.cs`, `Scripts/Player/PlayerAttack.cs`
- `Scripts/Enemies/EnemyHealth.cs`, `ContactDamage.cs`, `EnemyReward.cs`
- `Scripts/UI/PlayerHealthHUD.cs`
- `Prefabs/Characters/Player.prefab`, `Enemy_Basic.prefab`
- `Prefabs/UI/PlayerHUD.prefab`
- `Scenes/Village.unity`

Der Editor-Menüpunkt `LEVEL's/Setup M5 Progression` richtet die Assets über UnityEditor-APIs ein.
Mehrmaliges Ausführen erzeugt keine doppelten Stationen, Menüs oder Progression-Komponenten.
Vorhandene Balancing-Assets und manuell bearbeitete Menülayouts bleiben erhalten.
Das Werkzeug verweigert die Ausführung bei ungespeicherten Szenenänderungen oder laufendem Play Mode.
Station und Menü sind gespeicherte Prefabs mit echten Instanzen im Village.
Das Menü verwendet uGUI, genau ein EventSystem und persistente Referenzen auf die vorhandene UI-Action-Map.
Die vorhandene LegacyRuntime-Schrift wird wie im bisherigen HUD genutzt; keine TMP-Ressourcen oder Packages hinzugefügt.
Player-, Enemy- und Level-Geometrie werden nicht zur Laufzeit erzeugt.

## Durchgeführte Validierung

- Unity-Kompilierung: 0 Compilerfehler, 0 Compilerwarnungen nach Abschluss der Script-Korrekturen.
- Balancing-Formeln für Level 1–10 automatisiert gegen konkrete erwartete Werte geprüft.
- Neue Session: Levels 1/1, EXP/Gold 0, HP 5/5, Schaden 1, beide ersten Käufe 20 EXP.
- Käufe mit zu wenig EXP verändern weder EXP noch Levels; Menü zeigt verständliche Rückmeldung.
- Player-Kauf 20 EXP: EXP 100 → 80, Gesamt-EXP bleiben 100, HP 3/5 → 3/7, EnemyLevel bleibt 1.
- Weiterer Player-Kauf 40 EXP: Level 3, Max-HP 9, Schaden 2, aktuelle HP unverändert.
- Enemy-Kauf über virtuelles Gamepad: D-Pad wählt den zweiten Button, A/Submit kauft genau ein Enemy-Level.
- Mauskauf, Tastatur-Navigation, Enter/Submit und Escape/Cancel über Input-System-Ereignisse geprüft.
- E/Interact und Gamepad-North/Interact öffnen das Menü nach der vorhandenen Hold-Dauer.
- Offenes Menü sperrt Bewegung (Velocity 0) und `TryAttack`; Gamepad-Cancel gibt Bewegung wieder frei.
- Alle vier Richtungen über die bestehenden Portal-/Transition-Service-Konfigurationen geladen.
  Levels 4/3, HP 3/11, EXP und Gold bleiben erhalten; genau eine Session und ein Player, Kamera korrekt gebunden.
- Level-3-Gegner im Plateau: HP 5, Schaden 2, EXP 20, Gold 10. Nach erneutem Laden identische Werte.
- Wiederholte tödliche Damage-/Reward-Aufrufe vergeben nur einmal 20 EXP und 10 Gold.
- Reentranter Kauf während `GameSession.Changed` wird abgelehnt, auch bei ausreichender EXP.
- Menü dreimal geöffnet/geschlossen und Komponente deaktiviert/reaktiviert: ein Button-Ereignis kauft weiterhin genau ein Level zum angezeigten Preis.
- Zwei gezielt platzierte RB-Treffer auf Level 3: jeweils 2 Schaden, Enemy-HP 3 → 1 → Tod, einmal 10 EXP/5 Gold.
- Alle vier Richtungen zusätzlich mit durchgehend gehaltenem virtuellem Analogstick getestet:
  MoveInput vor/nach Wechsel 0.47, Velocity nach Freigabe 2.34, Loading-Sperre und Kamera korrekt.
- Vollständiger Play-Mode-Neustart bei deaktiviertem Domain Reload setzt Werte wieder auf 1/1, 0 EXP/Gold und 5/5 HP.
- Station, HUD und Menü über Game-View-Aufnahmen visuell geprüft.
- Setup zweimal wiederholt: Szene und alle fünf betroffenen Prefabs bleiben bytegleich;
  weiterhin genau eine Station, ein Menü und ein EventSystem im Village.

Die Tests verwendeten zeitweise direkt vergebene Test-EXP, gezielt positionierte Figuren und
virtuelle Eingabegeräte ausschließlich im Play Mode. Diese Testzustände werden nicht gespeichert.
Ein verworfener CLI-Screenshot-Aufruf außerhalb des zugelassenen Projektpfads erzeugte einen
Tool-Konsolenfehler; die Aufnahme im zulässigen Pfad funktionierte. Keine Gameplay-Exception festgestellt.

## Grenzen und manueller Abnahmetest

- Physische Xbox-/PlayStation-Controller, tatsächliches Spielgefühl und längere Balancing-Sessions bitte manuell abnehmen.
- Ohne zusätzliche Heilungsmöglichkeit bleiben beschädigte HP nach Level-up niedrig; das ist die gewählte HP-Regel.
- Die vorhandenen Szenen haben bereits vor M5 eine Loading-Mindestdauer von **2 Sekunden** gespeichert.
  Diese vorhandene Konfiguration wurde durch M5 nicht geändert.
- Kein Shop, Inventar, Equipment, Boss, weiterer Gegnertyp oder Save-System implementiert.

Manuell: Auf dem Plateau Gegner besiegen, ins Village zurückkehren, EXP an der Station
wahlweise für Player oder Gegner ausgeben und danach erneut kämpfen. Menü mehrfach mit
Maus, Tastatur und physischem Gamepad öffnen/schließen; Stick beim Schließen und über alle
Portale halten. HP-Erhalt, stärkere Gegner/Rewards und Neustartwerte kontrollieren.

Kein Commit und kein Push durch M5. Vorhandene fremde Änderungen bleiben unberührt.
