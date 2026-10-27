# Dreigingsmodel

**DeskVeil is een afschrikmiddel tegen meekijkers, geen security boundary.**

## Wat DeskVeil wél doet

- Voorkomt dat iemand die even achter je computer kruipt (of over je schouder meekijkt) per ongeluk of
  gemakzuchtig de inhoud van een vergrendeld bureaublad ziet nadat erheen geswitcht is.
- Werkt ongeacht hoe er geswitcht wordt: Task View, Win+Ctrl+pijl, taakbalk, Alt+Tab of een app.
- Vraagt Windows Hello (gezicht, vingerafdruk of pincode) om het gordijn weg te halen of om een
  vergrendeling op te heffen.

## Wat DeskVeil níet doet

| Beperking | Toelichting |
|---|---|
| Geen bescherming tegen iemand met toegang tot je ontgrendelde sessie die het echt wil | DeskVeil draait in dezelfde gebruikerssessie, zonder verhoogde rechten. Iemand aan het toetsenbord kan het proces beëindigen (Taakbeheer), de autostart verwijderen of `config.json` bewerken. |
| Task View-voorbeelden | Task View toont thumbnails van vensters op elk bureaublad wanneer je met de muis over een bureaublad in de balk beweegt. DeskVeil kan die niet afschermen (zie `docs/architecture.md`). |
| Taakbalk en Alt+Tab | Als Windows is ingesteld om vensters van **alle** bureaubladen te tonen, zijn vensters van vergrendelde bureaubladen daar zichtbaar (inclusief hover-thumbnails). De **Instellingen-audit** waarschuwt hiervoor, maar past niets aan. |
| Het moment vóór het gordijn | Het gordijn komt ná de switch. Tussen de switch en het gordijn (doel: < 100 ms) kan de inhoud heel kort zichtbaar zijn. |
| Andere zichtpaden | Vensters die op alle bureaubladen staan (vastgemaakt), meldingen, schermopnames, remote desktop, een tweede gebruiker met beheerrechten: allemaal buiten scope. |
| Geen versleuteling | Er wordt niets versleuteld of verborgen op schijf; DeskVeil dekt alleen het scherm af. |

## Aannames

- De vergrendelde Windows-sessie (Win+L) is de echte beveiliging. DeskVeil is bedoeld voor momenten waarop
  je je sessie open laat maar één bureaublad (werk, privé, financiën) uit beeld wilt houden.
- Windows Hello is ingesteld. Zonder Hello blijft een vergrendeld bureaublad vergrendeld; er is bewust
  geen terugval naar een wachtwoord in DeskVeil zelf.

## Aanbevolen instellingen

In **Instellingen > Systeem > Multitasking > Bureaubladen**:

- Taakbalk: toon alleen vensters van **het bureaublad dat ik gebruik**
- Alt+Tab: toon alleen vensters van **het bureaublad dat ik gebruik**

Deze komen overeen met de registerwaarden `VirtualDesktopTaskbarFilter` en `VirtualDesktopAltTabFilter`
(`1`) onder `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`.
