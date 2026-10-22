# M6 — Village-Shop und Core Loop

## Bedienung

Im Village steht links vom Brunnen eine grüne **SHOP**-Station bei `(-3, 0)`.
Die goldene Leveling-Station bleibt rechts bei `(3, 0)`. Beide haben einen
Interaktionsradius von 2 Welteinheiten und getrennte Interaktionsbereiche.
Die bestehende Interact-Action öffnet das jeweilige Menü: **E** oder
**Y / Dreieck halten**. Die vorhandene Hold-Interaction bleibt erhalten.

Shop-Bedienung: Maus, WASD/Pfeiltasten oder Gamepad-Stick/D-Pad; Kauf mit
Mausklick, Enter oder A/Kreuz. Schließen per Button, Escape, B/Kreis oder
erneutem Interagieren. Preise, Gold, HP und Angebotswirkungen sind im Menü sichtbar.
Erfolg und abgelehnte Käufe erhalten eine Textrückmeldung.

Die vorhandene `LegacyRuntime`-Schrift und das uGUI-Layout des Leveling-Menüs
werden wiederverwendet. Es gibt keine zusätzliche Pixel-Schrift, TMP-Installation
oder neue externe Packages.

## Angebote und Kaufregeln

| Angebot | Standardpreis | Wirkung |
| --- | --- | --- |
| Heilung | 5 Gold | Aktuelle HP auf das vorhandene Maximum auffüllen |
| Max-HP +1 | 25 Gold | Dauerhafter Bonus von +1 Max-HP für diese Session |

Beide Preise sind am `ShopTransactions`-Component des ShopStation-Prefabs
im Inspector konfigurierbar; auch kostenlose Angebote sind möglich.
Volle HP verhindern einen Heilungskauf. Zu wenig Gold, ein toter/fehlender Player,
ungültige Angebote oder das Integer-Limit verhindern den Kauf ohne Abbuchung.

`GameSession` bucht Gold und Wirkung vor einer gemeinsamen Benachrichtigung ab.
Reentrante Shop-Käufe während dieser Benachrichtigung werden abgelehnt.
Zusätzlich verarbeitet das Menü höchstens einen Kauf pro Frame und kauft nicht
im Öffnungsframe. Ein einzelner Submit erzeugt damit keinen doppelten Kauf.

Der separate `ShopMaxHealthBonus` wird genau einmal zur bestehenden
Player-Level-Berechnung addiert:

`MaxHP = Progression.PlayerHealth(Prefab-Basiswert, PlayerLevel) + ShopMaxHealthBonus`

Ein Max-HP-Kauf heilt nicht. Beispiel: `3/5 -> 3/6`; ein späterer Player-Level
ergibt mit den M5-Standardwerten `3/8`. Heilung, Beschädigung und Szenenwechsel
setzen den Bonus nicht zurück. PlayerHealth synchronisiert gekaufte Heilung
aus der Session, damit der nächste Treffer von den geheilten HP ausgeht.

## Architektur und Dateien

Neue Dateien, jeweils mit Unity-`.meta`:

- `Scripts/Core/ShopTransactions.cs`: Angebote, Preise und Kaufbedingungen.
- `Scripts/Player/ShopStation.cs`: Interact, Reichweite und Prompt.
- `Scripts/UI/ShopMenu.cs`: Anzeige, Navigation, Rückmeldungen und Steuerungssperre.
- `Editor/M6ShopSetup.cs`: idempotente Einrichtung mit UnityEditor-APIs.
- `Prefabs/Environment/ShopStation.prefab`: editierbarer Sprite und BoxCollider2D.
- `Prefabs/UI/ShopMenu.prefab`: gespeicherter Overlay-Canvas mit echten Buttons.
- Diese Dokumentation.

Gezielt erweitert:

- `Scripts/Core/GameSession.cs`: Shop-Bonus, atomare Shop-Käufe und Session-Reset.
- `Scripts/Player/PlayerProgression.cs`: Level-HP plus separater Shop-Bonus.
- `Scripts/Player/PlayerHealth.cs`: Synchronisation von Session-Heilung.
- `Scripts/Player/LevelingStation.cs`: Prompt während anderer Steuerungssperren ausblenden.
- `Scripts/Scenes/SceneTransitionService.cs`: keine Transition bei gesperrter Bewegung.
- `Scenes/Village.unity`: genau eine echte ShopStation- und ShopMenu-Prefab-Instanz.

Shop und Leveling verwenden genau ein gemeinsames EventSystem und dessen bereits
konfigurierte UI-Actions. Das Shop-Menü erhält die Referenz als Village-Instanzoverride.
Die bestehenden Gameplay-Action-Maps und die Gerätezuordnung bleiben aktiv.
Bewegung und Angriff werden wie beim Leveling/Loading über ihre Komponenten gesperrt;
ein gesperrter Player kann kein zweites Menü und keine Transition beginnen.
Nach dem Schließen liest Movement sofort den weiterhin gehaltenen Stickwert.
Callbacks werden beim Deaktivieren abgemeldet. Die Aufräumlogik toleriert ein bereits
zerstörtes gemeinsames EventSystem beim Ende des Play Modes.

`LEVEL's/Setup M6 Shop` erzeugt fehlende M6-Prefabs und Village-Instanzen, verbindet
Referenzen und erhält bereits vorhandene Layout-/Preis-Anpassungen. Das Werkzeug
verweigert Play Mode oder ungespeicherte Szenen. Es verwendet keine Szenen-YAML-Edits
und stellt die zuvor geöffneten Szenen wieder her.

## Core Loop

Village -> Plateau -> Gegner besiegen -> skalierte EXP/Gold erhalten -> Village ->
Leveling oder Shop -> Plateau mit den neuen Werten -> Village oder Tower.
Der reguläre Gegner erscheint beim erneuten Laden seiner gespeicherten Plateau-Szene
wieder und kann pro Lebenszyklus weiterhin nur einmal Rewards vergeben.
Tower und verschlossene, blockierende Bosstür bleiben erhalten.

Player-/Enemy-Level, EXP, Gold, aktuelle HP und Shop-Bonus liegen in der GameSession.
Szenenwechsel erzeugen keine neuen Player oder Level-Geometrie per Script.
Ein neuer Play-Mode-Start setzt die Session auch ohne Domain Reload zurück.
Player-Despawn bei 0 HP und die bestehenden Regeln beim Tod bleiben erhalten.
Es gibt kein Inventar, Boss-System oder Save-to-Disk.

## Tatsächlich ausgeführte Prüfungen

- Unity-Kompilierung: 0 Fehler und 0 Warnungen nach Abschluss.
- 91 automatisierte Laufzeitprüfungen im echten, nachweislich laufenden Play Mode:
  Kaufbedingungen, keine Überheilung, einmalige Abbuchung/Wirkung, synchronisierte HP,
  reentrante Käufe, Level-Bonus plus Shop-Bonus, gegenseitige Menüsperre,
  Bewegungs-/Angriffs-/Portalsperre, gehaltene Interact-Action, D-Pad-Navigation,
  Submit, Tastatur, echte UI-Mausereignisse, Cancel und gehaltene Bewegung nach Schließen.
- Wiederholtes Öffnen/Schließen und Deaktivieren/Reaktivieren erzeugen keine doppelten
  Button-Callbacks. Zwei Kaufcallbacks im selben Frame ergeben einen Kauf.
- Szenen-/Kampf-Loop über die gespeicherten Portal-Zielkonfigurationen und den bestehenden
  asynchronen Transition-Service: Village -> Plateau -> Village -> Plateau -> Tower ->
  Plateau -> Village. Richtige Spawns, Loading, genau ein Player/eine Session, Kamera
  und der M3.5-Hotfix mit durchgehend gehaltenem virtuellem Analogstick wurden geprüft.
- Tatsächliche RB-Angriffe mit der vorhandenen Physik-Hitbox besiegen den Level-2-Gegner;
  15 EXP und 7 Gold werden einmal vergeben. Nach erneutem Plateau-Laden steht wieder
  genau ein Level-2-Gegner mit 4 HP bereit. Verdientes Gold kann im Village heilen.
- Player-Level, Enemy-Level, EXP, Gold, HP und Shop-Bonus bleiben beim Wechsel erhalten.
- Player verschwindet bei 0 HP; vorhandene Progressionswerte werden dadurch nicht zurückgesetzt.
- 35 zusätzliche Prüfungen der gespeicherten Assets: Build-Szenenliste, echte Prefab-
  Instanzen, fehlende Scripts, UI-/Stationsreferenzen, Kamera, Portale und einziges EventSystem.
- Shop und Village anhand von Game-View-Aufnahmen visuell geprüft.
- Setup erneut ausgeführt: Village und beide M6-Prefabs bleiben bytegleich.
- Neustart bei geöffnetem Shop: ein gefundener MissingReference-Fehler wurde durch eine
  Null-Prüfung korrigiert; der gezielte Wiederholungstest bleibt ohne neue Console-Fehler.
- Frischer Play-Mode-Start: Levels 1/1, EXP/Gold 0, Bonus 0, HP 5/5, geschlossenes Menü
  und aktive Steuerung. Domain Reload bleibt deaktiviert.

Die Laufzeittests verwenden ausschließlich temporär erhöhte EXP/Goldbestände,
gezielt positionierte Figuren und virtuelle Eingabegeräte. Beim gezielten Hitbox-Test
wurde die Gegnerbewegung angehalten. Testwerte und Testeingaben werden nicht gespeichert.
Die Fokusoptionen des Input Systems und die Hintergrundoption des Players wurden nach
den Tests wiederhergestellt. Testhilfen und Berichte liegen nur unter dem ignorierten `Temp/M6`.

Die erste virtuelle Tastaturprüfung scheiterte an der Fokusfilterung des Hintergrund-
Editors; die kontrollierte Testumgebung verifiziert anschließend E, Pfeile, Enter,
Escape und Maus erfolgreich. Dies erforderte keine Gameplay-Input-Änderung.

## Manuelle Abnahme und Grenzen

Mit physischer Tastatur/Maus und Xbox-/PlayStation-Controller den vollständigen Loop
spielen: Gegner besiegen, zurückkehren, EXP ausgeben, heilen/Max-HP kaufen und erneut
kämpfen. Am Shop Y/Dreieck beziehungsweise E halten; beim Schließen und über alle
Portale den Stick weiter halten. Auswahlmarkierung, Lesbarkeit bei den gewünschten
Game-View-Auflösungen und Balancing manuell beurteilen.

Keine neue Pixel-Schrift vorhanden. Keine Änderung an Attack-Bindings, Loading-Dauer,
Death-Penalties, der Tower-Tür oder der grundlegenden Combat-Architektur.
Keine bekannten verbleibenden M6-Gameplay-Fehler aus den ausgeführten Prüfungen.
Ein physischer Controller- und Langzeit-Spieltest wurde nicht durchgeführt.

Kein Commit und kein Push. Die vor M6 vorhandenen fremden Änderungen bleiben erhalten.
