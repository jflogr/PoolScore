Pool Score Tracker
==================

A simple offline scoreboard for pool matches:
  - Round robin (5 players) : doubles round-robin, 15 sets
  - Round robin (4 players) : doubles round-robin, 9 sets
  - Singles (1 v 1)         : one-off race
  - Doubles (2 v 2)         : one-off race, pick the two teams yourself
Singles and doubles one-off matches support per-player / per-team handicaps
(each side can race to a different number).

Both versions are built to be streamed - see STREAMING below. The Windows app
has a separate banner window for OBS to capture. In the browser version it is
the top scoreboard line, with a thin progress strip beneath it: the current set
and round on the left, who is sitting out on the right, and a marker per set in
between - solid for the sets already played, tall for the set being played now,
with a gap at each round break. Crop the OBS source to the line above it.

-------------------------------------------------------------------
WHO BREAKS
-------------------------------------------------------------------
Setup has a Break setting with two house rules:

ALTERNATE BREAK (the default)
- The break changes hands after every rack, so the app can follow it. A gold dot
  marks the side breaking the next rack - on the scoreboard line, and to the
  left of that side's names. It flips every time you score a rack, and
  disappears once the race is won.
- "Swap break" flips it, for when the wrong side started. In a one-off match it
  swaps for good, so the order the two players/teams were picked in does not
  decide who breaks. In a round-robin it swaps that one set only.
- The schedule shows which side breaks first in every set, and the round-robin
  schedules share those first breaks out evenly:
    Round robin (5) - 15 sets, everyone plays 12 and breaks first in 6. Dead even.
    Round robin (4) - 9 sets hand out 18 first breaks between 4 players, so 4.5
              each is not possible; the schedule gets as close as any 9-set one can
              (P1 breaks first 6 times, everyone else 4).

WINNER BREAKS
- Whoever wins a rack breaks the next one. Only totals are recorded, so the app
  cannot know who won the last rack - it hides the break marker and the swap
  button instead of guessing.

The setting is shared, so phones scanning in see the same rule.

-------------------------------------------------------------------
NAMES ON THE SCOREBOARD
-------------------------------------------------------------------
- First name (the default) shows each player's first name, adding just enough of
  the surname to tell two Daves apart. A 1 v 1 always shows the whole name.
- Full name shows every word - use it when the player list holds team names
  ("Red Devils") rather than people.

-------------------------------------------------------------------
LEADERBOARD
-------------------------------------------------------------------
Ordered by wins, and deliberately just two numbers per player:
  Wins      - sets won out of sets played
  Possible  - where they would finish if they won every set they have left

-------------------------------------------------------------------
TWO WAYS TO RUN IT
-------------------------------------------------------------------
1. WINDOWS APP - windows-app\publish\PoolScoreTracker.exe
   One file. Nothing to install: no Node.js, and no WebView2 either - the app
   draws its own scoreboard. Copy it anywhere and double-click.

   The window is in two halves. The top half is the scoring area, sized to be
   read from across the room: each side's names with the score beneath them,
   and two narrow bars down the middle - one box per rack in that side's race,
   filling as racks are won. They turn pale gold when a side is on the hill and
   full gold once it has won.

   Score with the big -/+ buttons, or from the keyboard:
     A       add a rack to the left side      ALT+A   take one off
     B       add a rack to the right side     ALT+B   take one off

   A toolbar under that has Commit, Undo last, Swap break, Players, Banner and
   Reset. Players opens the roster in its own window. Below the toolbar are
   tabs: Match settings, and - in a round robin only - Matches and Leaderboard.
   A one-off match has no schedule and keeps no table, so those two are simply
   not there.

   "Banner" opens a separate fixed-size window with just the score line in it -
   that is the one to point OBS at (see STREAMING below).

   It serves the phone view itself, so phones on the same wifi can watch
   without anything else running.

   Players and scores are remembered between runs in one file:
     %LOCALAPPDATA%\PoolScoreTracker\session.json
   One per Windows account, so the exe stays a single file you can copy
   anywhere. A new install starts with five default names to rename.

2. INSTALLED BROWSER APP - see INSTALL IT ONCE below.
   The original web version, installed into Windows from the browser. Still
   here, still works, and this is the one to use on a Mac or a tablet. Unlike
   the Windows app, phones can score on it as well as watch.

The two keep separate players and scores, because they store them in different
places. Pick one and stick with it.

-------------------------------------------------------------------
INSTALL IT ONCE (Windows)
-------------------------------------------------------------------
The app installs itself into Windows and then runs on its own. The little
server in this folder exists only to hand it over to the browser - once the app
is installed you never need to start the server again.

1. Install Node.js (one-time) from https://nodejs.org  (choose the "LTS" build).
2. Double-click  install-or-update.cmd
     - A console window opens and your browser opens the app.
3. Click "Install the app" on the page.
     - Or use the install icon in the address bar / browser menu ("Install this
       site as an app" in Edge, "Cast, save and share > Install" in Chrome).
4. Close the console window. You are done.

From then on, launch "Pool Score Tracker" from the Start menu (or pin it to the
taskbar). It opens in its own window, works with no console window, no server
and no internet, and remembers your players and scores on this PC.

WHEN THE APP CHANGES
- Run  install-or-update.cmd  again, let the app load once in the browser, then
  close the console window. That refreshes the copy Windows launches.

Mac / Linux: open a terminal in this folder and run  node server.mjs  , open the
address it prints (http://localhost:4174), and install from the browser menu.

-------------------------------------------------------------------
PHONES (same wifi)
-------------------------------------------------------------------
WINDOWS APP - watching only
- Nothing to start. The app serves the phone view itself, on port 4174.
- Scan the QR code on the Match settings tab, or type the address printed
  above it.
- Phones get a read-only view: the live score, the leaderboard and the full
  schedule. They cannot change anything, so nobody can break the setup mid-game.
- The first run raises a Windows firewall prompt, because the app has to accept
  connections from the wifi. Say no and everything still works except this.
- If something else has port 4174, the Match settings tab says so and the rest
  of the app carries on as normal.

BROWSER APP - scoring from phones
- Run  install-or-update.cmd  and leave the console window open.
- The "Share live score" panel shows a QR code. Anyone on the same wifi can scan
  it for a SCORE-ONLY view that updates the live score.
- Phones can only change the score (and start a new match) - not the format or
  the player list.
- Without the console window it says "Off - scoring on this device only" and
  everything else works exactly as normal.

-------------------------------------------------------------------
STREAMING
-------------------------------------------------------------------
WINDOWS APP
- Click "Banner". A separate fixed-size window opens with nothing in it
  but the score line. In OBS add a Window Capture and pick
  "[PoolScoreTracker.exe]: Pool Score Banner". No cropping needed - the window
  is exactly the size it captures at.
- Any capture method works, including BitBlt, and it keeps working while the
  window is behind others. The banner is drawn by the app itself rather than by
  a browser engine, which is what makes that true.

BROWSER APP
- Add  /?view=board  to the address (e.g. http://localhost:4174/?view=board)
  for a clean, full-screen, read-only scoreboard. It auto-sizes to the screen -
  good for a wall display or as an OBS browser source.

Everything stays on this PC / your device - nothing is sent over the internet.
Phone sharing works on your local wifi only.

-------------------------------------------------------------------
REBUILDING (for whoever maintains this)
-------------------------------------------------------------------
  powershell -ExecutionPolicy Bypass -File build.ps1 -BumpCache -App

Packages pool-score-tracker.zip and verifies it, bumps the service worker cache
name so installed browser copies refresh, and builds PoolScoreTracker.exe.

The two are independent now: the Windows app is native C# under windows-app\
and no longer embeds the web files, so a change to index.html / app.js / the
stylesheet only affects the browser version. -BumpCache matters to that one
alone. Changing anything under windows-app\ means rebuilding with -App.
