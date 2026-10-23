# M13 — Pause Menu mit Tastatur und Controller

Implementiert am 23.10.2026 mit Unity 6000.6.2f1. Keine neuen Packages.
Die manuelle Abnahme durch den Nutzer steht aus. Kein Staging, Commit oder Push.

## Bedienung und gespeicherte UI

- Das vorhandene `Assets/Settings/InputSystem_Actions.inputactions` enthält
  `System/Pause`: Button mit `<Keyboard>/escape` und `<Gamepad>/start`.
  Xbox Menu/Start und PlayStation Options verwenden den gemeinsamen Start-Control.
  Keine Hold-Interaction; die Aktion reagiert beim Drücken.
- Ein weiterer Tastendruck oder **CONTINUE** setzt fort. Halten, Loslassen und
  gleichzeitige Escape-/Start-Eingaben erzeugen keine zusätzlichen Toggles.
- `Prefabs/UI/PauseMenu.prefab` ist als editierbare Instanz in Village, Plateau
  und Tower gespeichert: dunkler Hintergrund mit 80 % Deckkraft, **PAUSED**,
  **CONTINUE**, **BACK TO MAIN MENU** und die vorhandene eingebettete Pixelschrift.
- Canvas-Sortierung 150: über dem HUD (100), unter Game Over (200) und Loading (1000).
  CanvasScaler und GraphicRaycaster werden wie in M12 verwendet.
- Die zwei Buttons haben explizite Navigation und eine sichtbare Auswahl.
  Das vorhandene `MenuFocus` und der eine vorhandene EventSystem/UI-Input-Modul
  bedienen Maus, Tastatur, D-Pad und Stick. UI-Fokus und Button-Übergänge
  funktionieren bei angehaltener Gameplay-Zeit.

## Architektur und Lebenszyklus

`GameFlow` erhält den Zustand `Paused`. Die vorhandene zentrale
`IsGameplayActive`-Sperre blockiert damit Schaden, Angriff, Interact, Belohnungen,
Transaktionen und Spawnversuche. Pause setzt zuerst diesen Zustand und
deaktiviert die zuvor aktiven PlayerMovement-/PlayerAttack-Komponenten.
Das stoppt auch einen sichtbaren Slash. Die zuvor aktiven Komponenten werden
zum Fortsetzen vorgemerkt; bereits vorher deaktivierte Komponenten werden
nicht versehentlich aktiviert.

`PauseController` liegt szenenlokal neben GameFlow. Er verwendet eine lokale
Kopie genau der konfigurierten Asset-Aktion. Damit bleibt Pause unabhängig von
PlayerInput-Gerätezuordnung und aktivem Gameplay-/UI-Map verfügbar. Die Kopie
wird beim Deaktivieren abgemeldet und freigegeben; es gibt keinen persistenten
Pause-Singleton. Ein Halte-Latch und eine Bearbeitung pro Frame verhindern
mehrfache Toggles. Die Menüpriorität wird bereits beim Eingabeereignis erfasst,
bevor UI Cancel ein Stationsmenü schließen kann.

`PauseMenuUI` verbindet die beiden gespeicherten Buttons mit PauseController.
Das Overlay wird beim Start verborgen und vollständig aus der Szene entladen.

PlayerInput und die Move-Aktion bleiben während Pause aktiv. Gameplay ist durch
GameFlow und die deaktivierten Steuerungskomponenten gesperrt. Beim Fortsetzen
liest der bestehende PlayerInputReader den weiterhin gehaltenen Stick direkt;
erneutes Loslassen ist nicht erforderlich. R1/RB, E/A, die vorhandene zusätzliche
Space-Angriffsbindung und der Loading-Screen bleiben erhalten.

## TimeScale und Spawner

- Pause: `Time.timeScale = 0`. Player-/Enemy-Geschwindigkeiten werden sofort null.
  FixedUpdate und skalierte Gameplay-Zeit stehen still; Gegner behalten Aggro,
  Kontakt- und Knockback-Zustand für das Fortsetzen.
- Continue/Escape/Start: Overlay schließen, `Time.timeScale = 1`, Zustand Playing
  und vorherige Bewegung-/Angriffskomponenten wieder freigeben.
  Die GameSession und sämtliche ihrer Werte werden dabei nicht verändert.
- EnemySpawner bleibt während Pause aktiviert. Sein bereits vorhandener
  GameFlow-/TimeScale-Guard überspringt Update vor jeder Timeränderung und jedem
  Spawnversuch. `OnDisable` wird durch Pause nicht ausgelöst und verwirft die
  Timer daher nicht. Beide unabhängigen Timer behalten exakt ihre Restzeiten.
- Beim Fortsetzen läuft die skalierte Zeit weiter. Kein Aufholen verstrichener
  Echtzeit und keine Burst-Schleife. Unverändert maximal 5 Goblins und 2 Cyclops,
  mit den vorhandenen getrennten Intervallen von 2 bzw. 3 Sekunden.
- Back to Main Menu nutzt den bestehenden asynchronen Run-Loader ohne zusätzliche
  Ladeanzeige. GameFlow setzt Loading/TimeScale 1, beendet Spawner und AI,
  deaktiviert Gameplay-Input und ruft das vorhandene `GameSession.EndRun()` auf.
  Der bestehende Session-Singleton bleibt als geleerte Sitzung erhalten.
  MainMenu erhält seine reguläre UI-Navigation; alte Welt-/Pauseobjekte werden entladen.
- GameFlow-Subsystem-Reset und Szenen-/Komponentenbereinigung setzen TimeScale
  zurück. Verlassen des Play Mode während Pause wurde tatsächlich geprüft.

## Menüpriorität

Loading, Main Menu und Game Over erlauben keine Pause. Shop und Leveling werden
mit Escape oder Start zuerst geschlossen; derselbe Druck öffnet keine Pause.
Auch die Stationsmenüs selbst können während Pause nicht geöffnet werden.
GameFlow prüft diese Sperren zusätzlich zum Eingaberouter.

## Tatsächlich ausgeführte Prüfungen

Die Editor-Menüeinträge unter `LEVEL's/M13/` bieten explizites Asset-Setup,
gespeicherte Assetprüfung und vollständige Play-Mode-Integrationstests.
Die Tests verwenden temporäre simulierte Geräte im echten Unity Input System
und die gespeicherten Spielszenen. Es werden echte UI-Navigation, Maus-Raycasts
und Klicks, Gameplay-APIs, Physik, AI und asynchrone Szenenwechsel ausgeführt.
Die Geräte werden anschließend entfernt; Testberichte bleiben unter Library.

| Prüfung | Ergebnis |
| --- | --- |
| Unity-Skriptkompilierung | Erfolgreich, keine Compilerfehler |
| M13 gespeicherte Assets und Referenzen | 27 Prüfungen bestanden |
| M13 mit deaktiviertem Domain Reload | 66 Prüfungen bestanden, 0 erfasste Fehler; Frame 1 bis 23653 |
| M13 mit normalem Domain Reload | 66 Prüfungen bestanden, 0 erfasste Fehler; Frame 1 bis 24918 |
| M12 gespeicherte Menü-/Szenen-Assets | 32 Prüfungen bestanden |
| M12 Laufzeitregression | 95 Prüfungen bestanden, 0 erfasste Fehler |
| Unity Console nach den vollständigen Läufen | 0 Fehler, 0 Warnungen |
| Visuelle Kontrolle | Tatsächlichen Pause-Screenshot angesehen |

Abgedeckt sind kurze 60-ms-Eingaben, gehaltene Tasten und gleichzeitige Eingaben,
Continue über alle drei Bedienarten, erneutes Escape/Start, gehaltener Analogstick,
unveränderte Sitzung, laufende UI bei eingefrorener Gameplay-Zeit, tatsächliches
Stoppen/Fortsetzen einer bewegten EnemyAI, gesperrte Angriffs-/Schadens-APIs,
Menüpriorität, Run-Neustart und alte Handler nach Szenenwechseln.
Die Spawnerprüfung hält länger als beide Spawnintervalle an, vergleicht die
Identität und Werte des Timerarrays, Versuchszahl und Gegnerzahlen und prüft
anschließend verschiedene Timer-Restzeiten, fehlende Sofort-Bursts und die
tatsächliche Auffüllung bis zu beiden getrennten Limits.

Berichte: `Library/M13SavedAssetsValidation.json`,
`Library/M13PauseValidation_NoDomainReload.json`,
`Library/M13PauseValidation_DomainReload.json`.
Vorschau: `Library/M13_Paused.png`.

## Manuelle Abnahme und Grenzen

Keine bekannten offenen Funktionsfehler aus den abgeschlossenen M13-Prüfungen.
Die Tests mit simulierten Geräten ersetzen keine physische Controller-Abnahme.

- Mit echter Tastatur/Maus und Xbox-/PlayStation-Controller prüfen:
  kurzer Escape-/Menu-/Options-Druck, Auswahl, Continue und erneutes Pausieren.
- Stick durch Pause und Resume durchgehend halten; Bewegung soll ohne Loslassen
  wieder beginnen. R1/RB und E/A anschließend regulär verwenden.
- In Plateau während Bewegung, Angriff und Gegnerkontakt pausieren; länger als
  beide Spawnintervalle warten und Fortsetzung ohne Burst bei Limits 5/2 ansehen.
- Shop und Leveling mit Escape/Start schließen, erst mit neuem Druck pausieren.
- Back to Main Menu und neues PLAY prüfen; keine alten Gegner, Pausezustände oder
  Sessionwerte. Darstellungen auch in Tower und gewünschten Fenstergrößen ansehen.
- Ein tatsächlicher Windows-/WebGL-Build wurde für M13 nicht ausgeführt.

## Dateien

Neu unter `Assets/_LEVELS/`, jeweils mit erforderlicher `.meta`:

- `Scripts/Core/PauseController.cs`
- `Scripts/UI/PauseMenuUI.cs`
- `Prefabs/UI/PauseMenu.prefab`
- `Editor/M13PauseSetup.cs`, `Editor/M13PauseValidation.cs`
- `M13_README.md`

Angepasst: `Scripts/Core/GameFlow.cs`, `Scenes/Village.unity`, `Plateau.unity`,
`Tower.unity`, außerdem `Assets/Settings/InputSystem_Actions.inputactions`.
`Editor/M12FlowValidation.cs` wählt weiterhin gezielt den M12-Menüfokus aus,
da nun ein zusätzlicher Pause-Menüfokus existiert.
GameSession, EnemySpawner und die bestehenden Combat-/AI-Klassen brauchten
keine Quelltextänderungen; ihre vorhandenen zentralen Sperren werden genutzt.

Vorhandene fremde Änderungen an `Assets/Editor`, der Space-Angriffsbindung und
`ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json` bleiben erhalten.
AGENTS.md bleibt lokal ignoriert und ungestagt. Nutzerabnahme ausstehend.
