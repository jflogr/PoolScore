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
PLAYER STATS
-------------------------------------------------------------------
Ordered by wins, then sets played, then rack difference:
  Wins      - sets won out of sets played
  Possible  - where they would finish if they won every set they have left
  Games     - racks won-lost across the sets they have played, because a
              hill-hill win and a 3-0 sweep both count as one set

Singles and doubles (Windows app) keep a history instead of a schedule, for
nights of several sets:
- A won race goes into the history when the board is cleared for the next
  one - New match, Clear scores, or changing a player. A race nobody has won
  yet is dropped as before.
- The Player stats tab shows Wins and Games from those races (no Possible -
  there is nothing scheduled to play), with the history under it: when each
  finished, the score, the winner marked, and how long it took.
- The line under the scoreboard gives how many have been played and the
  average match. "Clear history" on that tab starts the night afresh; the
  race on the board is left alone. Singles and doubles keep separate
  histories, and phones see them too.

-------------------------------------------------------------------
MATCH TIMES (Windows app)
-------------------------------------------------------------------
- What is timed is the games (see GAME TIMES). A match's time is its games
  added up, so the changeover and any break between matches are not counted
  as play.
- Press Start (the gold button between the scores, or on an allowed phone)
  when the players are at the table: that puts the match and its first game
  on the clock. Next match and New match no longer start the clock - the next
  match waits for its own Start. Forget it and the first rack still starts
  the match, but that first game has no time.
- The time so far sits at the top of the scoreboard, between the names, and
  every played set's time is in the Matches tab.
- A line under the scoreboard gives the average match so far, how many are
  left, and the projected finish: the games still to play at the average
  game so far, plus the usual changeover before each set yet to start.
  Games to a set come from the sets already played - until one is, a race is
  taken to run halfway between a sweep and hill-hill. The changeover is the
  gap from one set ending to the next Start; a gap over half an hour counts
  as a break and is left out. Every game and every set played feeds those
  averages, so the forecast firms up as the night goes on. It needs one
  timed game. Round robins only - singles and doubles show the average
  alone (see PLAYER STATS).
- Long match (Match settings, on the right) lights the scoreboard up once a
  match runs past it: an orange frame round the scoreboard and an orange
  clock. It goes off as soon as a side wins. Off, or 20 to 45 minutes; 25 to
  start with. It only warns - the score is never touched, so if the table
  agrees to cut a long one short (say, play 1-1 as the decider), set the
  score by hand. The banner never shows it.

GAME TIMES
- Every game (rack) is timed, roughly: from one score change to the next,
  so it includes racking up. A match's first game starts with its Start.
  Once a side has won, no game is being timed until the next Start.
- GAME 3:21 under the match clock is the game on the table.
- Taking a rack off undoes that game, and if it was the one just finished
  its clock picks up again from when it really started - so a wrong tap put
  right straight away costs nothing.
- Restart game clock on the Play tab starts the game on the table again from
  now, for when a run of taps has left it timing from the wrong moment.
- The Matches tab lists each set's games under it - the running score, a
  tick on the side that won it, and how long it took - with the fastest,
  slowest and average game across the night along the top. Singles and
  doubles show the same under each race in the History.
- Phones get it all too: MATCH and GAME clocks along the foot of the score,
  and the same games, with the winning side's names, under each match or
  race. Allowed phones can press Start; Restart game clock stays at the PC.

-------------------------------------------------------------------
SHOOTING ORDER (Windows app)
-------------------------------------------------------------------
The app keeps track of who is at the table. Their name stays bright on the
scoreboard, the banner and phones; everyone waiting is dimmed.

- Play tab > Shooting order for this match: who breaks, and - in doubles -
  who shoots after them. The rest follows: breaker, next, breaker's partner,
  next's partner, and round again. Out of the box it is the first player of the
  side breaking, then the first player of the other side. Picking a breaker
  from the other side before the first rack takes the break across too.
- ENTER (or Turn over on the Play tab, or on an allowed phone) is a miss: the
  next player in the order is up. A pocketed ball needs nothing pressed -
  the same player carries on.
- Push reverse (Play tab or phone) steps the turn back one, for when the
  other side puts a pusher back in.
- Each new rack is broken by the next player in the order from the side
  breaking it, so partners take turns to break. Under winner breaks, the
  side that took the rack breaks. Taking a rack off steps it back.
- Nothing here watches the table yet: the order only moves when somebody
  says so. A camera-driven shot clock is planned to plug into it.

-------------------------------------------------------------------
NO DOUBLE-SCORING (Windows app)
-------------------------------------------------------------------
Once a rack is added, nobody - the PC or any phone - can add another for 5
seconds. No rack is played that quickly, so a second plus inside it is almost
always two people scoring the same rack. Whoever was turned away is told who
just updated the score (on the PC across the scoreboard, on a phone under
its score). Taking a rack off is never held up, and clears the wait, so
fixing a tap on the wrong side is instant.

-------------------------------------------------------------------
TWO WAYS TO RUN IT
-------------------------------------------------------------------
1. WINDOWS APP - windows-app\publish\PoolScore.exe
   One file. Nothing to install: no Node.js, and no WebView2 either - the app
   draws its own scoreboard. Copy it anywhere and double-click.

   The window is in two halves. The top half is the scoring area, sized to be
   read from across the room: each side's names with the score beneath them,
   and two narrow bars down the middle - one box per rack in that side's race,
   filling as racks are won. They turn pale gold when a side is on the hill and
   full gold once it has won.

   Under each score is the race that side is chasing - "RACE TO 3" - small and
   grey, because the number rarely changes and the score always does. On a
   handicap the two sides show different numbers, which is the point of it.

   Score with the big -/+ buttons, or from the keyboard:
     LEFT ARROW    add a rack to the left side     ALT+LEFT    take one off
     RIGHT ARROW   add a rack to the right side    ALT+RIGHT   take one off
   The arrows point at the side they score. They work wherever the focus is,
   including a player dropdown you have just picked from - only a text box,
   or a dropdown with its list open, keeps the arrow for itself.

   The gold button between the scores says what happens next: Start before a
   match (it puts the match and its first game on the clock), then Next match
   - New match in singles and doubles - once a side has won.

   ENTER passes the turn on a miss - see SHOOTING ORDER below.

   The toolbar at the top has Players (the roster, in its own window),
   Previous match (round robin undo) and Banner. Below the scoreboard are
   tabs, in the order you need them:
     Play            - opens first. Everything used during play: who is
                       shooting (large) and who is next, and big buttons for
                       Turn over, Push reverse, Restart game clock, Swap break
                       and Reset (called Clear scores in singles and doubles,
                       where there is only the one score to clear - it always
                       asks first). Under them, this match's shooting order.
     Matches         - round robins only: the schedule, with each set's games.
     Player stats    - the stats, and in singles and doubles the history.
     Match settings  - set once a night: format, race, break rule, names,
                       the line-up and Long match.

   "Banner" opens a separate fixed-size window with just the score line in it -
   that is the one to point OBS at (see STREAMING below).

   It serves the phone view itself, so phones on the same wifi can watch -
   and score, once you allow them - without anything else running.

   Players and scores are remembered between runs in one file:
     %LOCALAPPDATA%\PoolScore\session.json
   One per Windows account, so the exe stays a single file you can copy
   anywhere. A new install starts with five default names to rename.

   Builds before the PoolScore rename kept theirs under a PoolScoreTracker
   folder instead. If this app finds no file of its own it reads that one to
   start from, but it only ever writes its own - so the older exe keeps its
   players and scores exactly as they were, and still works if you ever need
   to go back to it. Delete the PoolScore folder to start over from the
   defaults.

2. INSTALLED BROWSER APP - see INSTALL IT ONCE below.
   The original web version, installed into Windows from the browser. Still
   here, still works, and this is the one to use on a Mac or a tablet. Phones
   can score on it as well as watch.

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
WINDOWS APP - anyone can watch, you choose who scores
- Nothing to start. The app serves the phone view itself, on port 4174.
- Scan the QR code down the right of the window, or type the address printed
  under it.
- Every phone gets the live score, the player stats, the full schedule with
  each played set's time, and the average match and projected finish.
- To score, a phone types a name and taps "Ask to score". A bar appears under
  the scoreboard on the PC - "Dave wants to score" - with Allow and Deny. It
  never covers the score or takes focus, and the OBS banner never shows it.
  Several asks at once queue up and are answered one at a time.
- An allowed phone gets a minus and a plus beside each side's score, and the
  same gold button as the PC under them: Start before a match, then Next
  match (New match in singles and doubles) once a side has won. Pressed late,
  it does only what the PC's button says at that moment, so it can never
  start or end the wrong thing.
- In singles and doubles an allowed phone can also set each side's race, pick
  the players and clear the scores - there is no schedule to show, so the
  room is there. Changing a player starts the score from 0 - 0, as it does
  at the PC.
- In a round robin that is all a phone gets. The format, the line-up and
  undo stay at the PC, so nobody can break the evening's schedule mid-game.
- Allowed phones are listed under "Scoring" below the QR code. The cross next
  to one stops it scoring at once; it can ask again.
- Approvals last until the app closes, so every match night starts with only
  the PC scoring. A phone that was allowed last time has to ask again.
- A tap is tied to the set that was on the phone's screen. If the set has moved
  on by the time it arrives, it is dropped rather than scored into the next one.
- The first run raises a Windows firewall prompt, because the app has to accept
  connections from the wifi. Tick BOTH private and public networks. A laptop
  that has never been told the house wifi is trusted counts it as public, so
  ticking only private blocks the very connection the phones need.
- Cancelling that prompt - or closing it, which counts as cancelling - is
  remembered as a block, and nothing asks again. The app cannot tell: Windows
  is turning the phones away at the door, so the address and the QR code still
  look exactly right while no phone can load the page. If that has happened:
    Windows Security > Firewall & network protection >
    Allow an app through firewall > Change settings, find "Pool Score", and
    tick both Private and Public. If it is listed more than once, tick them all.
- Guest, hotel and some pub wifi turns on client isolation, which stops two
  devices on the same network from reaching each other at all. Nothing on this
  PC can change that - use a phone hotspot instead.
- The address is re-read whenever this PC's network changes, so a laptop that
  joins the wifi after the app is already open, or picks up a different address
  part way through the night, brings the QR code up to date on its own.
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
  "[PoolScore.exe]: Pool Score Banner". No cropping needed - the window
  is exactly the size it captures at.
- The race sits between the two scores in brackets: (3) while both sides are
  chasing the same total, and (3/5) - home first - when a handicap has them
  chasing different ones.
- The scoring keys work in this window too, so clicking the banner to drag it
  onto the right monitor does not leave the keyboard dead.
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
name so installed browser copies refresh, and builds PoolScore.exe.

The two are independent now: the Windows app is native C# under windows-app\
and no longer embeds the web files, so a change to index.html / app.js / the
stylesheet only affects the browser version. -BumpCache matters to that one
alone. Changing anything under windows-app\ means rebuilding with -App.
