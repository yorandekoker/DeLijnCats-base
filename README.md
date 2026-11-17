# How many cats fit on one bus?

> **Gebruik van AI:** de Unity-code, de scène-opbouw en het ontwerpdocument heb ik gemaakt met hulp van Claude (AI). De commits met "Co-Authored-By: Claude" komen daaruit voort. Het onderzoek, de keuze van de locatie en het storyboard zijn mijn eigen werk. Ik heb het spel zelf getest.

Werkstuk 1, Interaction 1 (Unity). De Lijn x zakenreizigers x museumzaal.

Een touchscreen aan de ingang van een museum. Katten stappen in een De Lijn-bus en de CO2-teller per kat daalt:
samen reizen = minder CO2 per persoon. Alle tekst op het scherm is Engels.

## Starten (eerste keer)

1. Maak in Unity Hub een nieuw project: **3D (URP)**, bijvoorbeeld in Unity 6.
2. Kopieer de mappen `Assets` en het bestand `.gitignore` uit deze repo over je project (overschrijf wat er al staat).
3. Open `Window > Package Manager` en installeer **Cinemachine** (en **Input System** als Unity het vraagt).
4. Klik op de menuknop **Tools > De Lijn > Build Scene**. De scène wordt gebouwd en opgeslagen in `Assets/Scenes/Museum.unity`.
5. Open het **Game**-venster, kies **Simulator** (Device Simulator) en druk op **Play**.

Gaat er iets mis? Kijk in het Console-venster. Staat er een rode fout, stuur die dan door.

## Wat zit erin (checklist van de opdracht)

| Eis | Waar |
|---|---|
| Attract mode die voorbijgangers uitnodigt | `ExperienceController.Attract.cs` (pulserende hint, kat springt, camera zwaait) |
| Omgeving met minstens twee zones in de merkstijl | `BuildDeLijnScene.cs`: zone auto (grijs) en zone bus (geel) |
| Minstens 3 Cinemachine-camera's, vloeiende overgangen | `Cam_Overview`, `Cam_Cat`, `Cam_Bus` (rijdt mee met de bus) |
| Minstens 3 interacties met signifier en feedback | 1 tik op een kat, 2 tik op Go, 3 tik op de CO2-meter |
| Animator Controller die op de kijker reageert | `CatAnimator.controller`: Idle en Hop (trigger bij tik) |
| Vier geluidssoorten in een Audio Mixer | `ExperienceController.Audio.cs` (zie hieronder, mixer nog zelf maken) |
| UI die meeschaalt en met enkel vingers werkt | Canvas Scale With Screen Size (1920x1080), enkel tikken |
| Reset naar attract mode | Na 25 seconden zonder aanraking, en na het einde |
| Persoonlijke toevoeging | Katten als passagiers |

## Nog zelf te doen

- **Audio Mixer:** maak `Assets > Create > Audio Mixer`, voeg vier groepen toe (Ambient, Music, SFX, UI) en sleep ze in de vier velden op het object `Experience`. Vervang desgewenst de gegenereerde klanken door echte (rustige) geluiden.
- **Testen:** met enkel tikken in de Device Simulator, en met een medestudent zonder uitleg. Noteer wat ze deden.
- **README, video (40 tot 60 seconden), push en indienen** op Canvas. Vul bovenaan aan wie wat deed (bij duo).

## Mogelijke aanpassingen

- Tijd tot reset: `idleSeconds` op het object `Experience`.
- Aantal katten nodig voor Go: `CatsNeededToLeave` in `ExperienceController.Journey.cs`.
- De CO2-vergelijking is bewust vereenvoudigd (de uitstoot van één voertuig gedeeld door de passagiers).

## Stappen (zie ook de commits)

1. Scène bouwen in één klik
2. Tik op een kat: instappen, CO2-meter
3. Attract mode en reset
4. Go, vertrek, vergelijking, einde
5. Geluid
6. README en checklist


## Gameplay (v2)
1. Attract mode: the camera swings between the road and the driver cat, the hint pulses.
2. Tap the driver cat (cap + scarf): he hops into the bus and it drives off.
3. The bus drives straight on for 30 seconds. Tap the waiting cats you pass to let them board; cats you miss stay behind.
4. More cats on the bus = lower CO2 per cat (100% / number of cats). After 30 s a result screen appears; tap "Play again" or wait 25 s.

Run: open the project, menu Tools > De Lijn > Build Scene, press Play.
