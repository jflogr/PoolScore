const storageKey = "pool-score-tracker-v2";
const legacyKey = "pool-score-tracker-5-player-doubles-v1";

/* ---------------------------------------------------------------------------
 * Schedule templates. Matches are expressed in position tokens (p1..pN) and
 * resolved to real players through the active mode's line-up.
 *
 * The home side breaks the first rack by default, so the schedules are laid out
 * to share that out evenly. Any single set's break can be flipped in the app
 * ("Swap break"), which is recorded per set and marked in the schedule table.
 * ------------------------------------------------------------------------- */
// 5-player doubles: 15 sets, everyone plays 12 and breaks 6 - dead even.
const doubles5Schedule = [
  { set: 1, round: 1, home: ["p1", "p2"], away: ["p3", "p4"], sitting: "p5" },
  { set: 2, round: 1, home: ["p2", "p3"], away: ["p4", "p5"], sitting: "p1" },
  { set: 3, round: 1, home: ["p1", "p4"], away: ["p3", "p5"], sitting: "p2" },
  { set: 4, round: 1, home: ["p1", "p5"], away: ["p2", "p4"], sitting: "p3" },
  { set: 5, round: 1, home: ["p1", "p3"], away: ["p2", "p5"], sitting: "p4" },
  { set: 6, round: 2, home: ["p1", "p4"], away: ["p2", "p3"], sitting: "p5" },
  { set: 7, round: 2, home: ["p2", "p4"], away: ["p3", "p5"], sitting: "p1" },
  { set: 8, round: 2, home: ["p3", "p4"], away: ["p1", "p5"], sitting: "p2" },
  { set: 9, round: 2, home: ["p4", "p5"], away: ["p1", "p2"], sitting: "p3" },
  { set: 10, round: 2, home: ["p2", "p3"], away: ["p1", "p5"], sitting: "p4" },
  { set: 11, round: 3, home: ["p1", "p3"], away: ["p2", "p4"], sitting: "p5" },
  { set: 12, round: 3, home: ["p2", "p5"], away: ["p3", "p4"], sitting: "p1" },
  { set: 13, round: 3, home: ["p4", "p5"], away: ["p1", "p3"], sitting: "p2" },
  { set: 14, round: 3, home: ["p2", "p5"], away: ["p1", "p4"], sitting: "p3" },
  { set: 15, round: 3, home: ["p3", "p5"], away: ["p1", "p2"], sitting: "p4" }
];

// 4-player doubles: everyone partners everyone once per round, nobody sits.
// Each round plays the same three pairings, but which side breaks (home) rotates
// so the break is shared. 9 sets hand out 18 breaks between 4 players, so a dead
// even 4.5 each is impossible - the closest any 9-set schedule can get is
// 6 / 4 / 4 / 4, with the extra break going to P1.
const doubles4Schedule = buildDoubles4Schedule();

function buildDoubles4Schedule() {
  // Per round, which of the three teams containing P1 breaks: p1p2, p1p3, p1p4.
  const p1Breaks = [
    [true, true, true], // round 1: P1 breaks all three
    [false, false, true], // round 2: P4's side breaks twice
    [true, true, false] // round 3: back to P1's side twice
  ];
  const pairings = [
    { with1: ["p1", "p2"], other: ["p3", "p4"] },
    { with1: ["p1", "p3"], other: ["p2", "p4"] },
    { with1: ["p1", "p4"], other: ["p2", "p3"] }
  ];
  const out = [];
  let set = 1;
  p1Breaks.forEach((round, roundIndex) => {
    pairings.forEach((pair, pairIndex) => {
      const p1Home = round[pairIndex];
      out.push({
        set,
        round: roundIndex + 1,
        home: p1Home ? pair.with1 : pair.other,
        away: p1Home ? pair.other : pair.with1,
        sitting: null
      });
      set += 1;
    });
  });
  return out;
}

// kind "rr" = round-robin schedule; kind "match" = one-off race with handicaps.
// teamSize is players per side; slots is how many roster players the mode needs.
const MODES = {
  doubles5: { label: "2P RR 5", kind: "rr", teamSize: 2, slots: 5, schedule: doubles5Schedule },
  doubles4: { label: "2P RR 4", kind: "rr", teamSize: 2, slots: 4, schedule: doubles4Schedule },
  singles: { label: "1P Match", kind: "match", teamSize: 1, slots: 2, schedule: null },
  match2: { label: "2P Match", kind: "match", teamSize: 2, slots: 4, schedule: null }
};

const defaultRosterNames = ["Player 1", "Player 2", "Player 3", "Player 4", "Player 5"];

// Marker for the side that breaks first.
const breakDot = "●";

const els = {
  appTitle: document.querySelector("#appTitle"),
  sessionMeta: document.querySelector("#sessionMeta"),
  livePill: document.querySelector("#livePill"),
  resetAllButton: document.querySelector("#resetAllButton"),
  stripMeta: document.querySelector("#stripMeta"),
  stripSitting: document.querySelector("#stripSitting"),
  stripTrack: document.querySelector("#stripTrack"),
  streamHome: document.querySelector("#streamHomeName"),
  streamHomeScore: document.querySelector("#streamHomeScore"),
  streamRaceNumber: document.querySelector("#streamRaceNumber"),
  streamAway: document.querySelector("#streamAwayName"),
  streamAwayScore: document.querySelector("#streamAwayScore"),
  streamHomeBreak: document.querySelector("#streamHomeBreak"),
  streamAwayBreak: document.querySelector("#streamAwayBreak"),
  homeLabel: document.querySelector("#homeLabel"),
  awayLabel: document.querySelector("#awayLabel"),
  homeTeamName: document.querySelector("#homeTeamName"),
  awayTeamName: document.querySelector("#awayTeamName"),
  homeScore: document.querySelector("#homeScore"),
  awayScore: document.querySelector("#awayScore"),
  homeTeamCard: document.querySelector("#homeTeamCard"),
  awayTeamCard: document.querySelector("#awayTeamCard"),
  homeBreakBadge: document.querySelector("#homeBreakBadge"),
  awayBreakBadge: document.querySelector("#awayBreakBadge"),
  matchResult: document.querySelector("#matchResult"),
  commitButton: document.querySelector("#commitButton"),
  undoButton: document.querySelector("#undoButton"),
  newMatchButton: document.querySelector("#newMatchButton"),
  swapBreakButton: document.querySelector("#swapBreakButton"),
  leaderLabel: document.querySelector("#leaderLabel"),
  progressBadge: document.querySelector("#progressBadge"),
  standingsBody: document.querySelector("#standingsBody"),
  scheduleMeta: document.querySelector("#scheduleMeta"),
  activeSetBadge: document.querySelector("#activeSetBadge"),
  scheduleBody: document.querySelector("#scheduleBody"),
  modeSwitch: document.querySelector("#modeSwitch"),
  modeHint: document.querySelector("#modeHint"),
  breakRuleSwitch: document.querySelector("#breakRuleSwitch"),
  breakRuleHint: document.querySelector("#breakRuleHint"),
  nameStyleSwitch: document.querySelector("#nameStyleSwitch"),
  nameStyleHint: document.querySelector("#nameStyleHint"),
  lineupPickers: document.querySelector("#lineupPickers"),
  raceTarget: document.querySelector("#raceTarget"),
  doublesRaceField: document.querySelector("#doublesRaceField"),
  singlesHandicaps: document.querySelector("#singlesHandicaps"),
  targetA: document.querySelector("#targetA"),
  targetB: document.querySelector("#targetB"),
  targetALabel: document.querySelector("#targetALabel"),
  targetBLabel: document.querySelector("#targetBLabel"),
  rosterList: document.querySelector("#rosterList"),
  rosterAddForm: document.querySelector("#rosterAddForm"),
  rosterAddInput: document.querySelector("#rosterAddInput"),
  installPanel: document.querySelector("#installPanel"),
  installButton: document.querySelector("#installButton"),
  shareStatus: document.querySelector("#shareStatus"),
  shareBody: document.querySelector("#shareBody")
};

const boardView = new URLSearchParams(location.search).get("view") === "board";
// Set by the Windows app, which serves these same files inside its own window.
// There is no server behind it, so the sharing and install panels do not apply.
const isWrappedApp = Boolean(window.__poolApp);
// The host app runs on the computer (loopback / file / the Windows app). Everyone
// who joins over the wifi via the QR code is a guest: they can score but not
// change the setup.
const isLoopbackHost = location.protocol === "file:"
  || isWrappedApp
  || ["localhost", "127.0.0.1", "::1", "[::1]"].includes(location.hostname);
const isGuest = !boardView && !isLoopbackHost;
const clientId = makeClientId();

let state = loadState();

/* ---------------------------------------------------------------------------
 * Live-sync state. When the page is served by the companion server the app
 * mirrors a shared session; otherwise it runs purely on local storage.
 * ------------------------------------------------------------------------- */
let connected = false;
let serverRev = 0;
let eventSource = null;

// Display names for the active mode's line-up, recomputed every render.
let nameMap = new Map();

wireEvents();
wireInstall();
registerServiceWorker();
render();
initSync();

/* ------------------------------- events -------------------------------- */

function wireEvents() {
  document.querySelectorAll("[data-team][data-delta]").forEach((button) => {
    button.addEventListener("click", () => {
      adjustCurrentScore(button.dataset.team, Number(button.dataset.delta));
    });
  });

  els.commitButton.addEventListener("click", commitCurrentScore);
  els.undoButton.addEventListener("click", undoLastCommit);
  els.newMatchButton.addEventListener("click", startNewMatch);
  els.swapBreakButton.addEventListener("click", swapBreak);
  els.resetAllButton.addEventListener("click", resetSession);
  els.raceTarget.addEventListener("change", updateRaceTarget);
  els.targetA.addEventListener("change", () => updateMatchTarget("a", els.targetA.value));
  els.targetB.addEventListener("change", () => updateMatchTarget("b", els.targetB.value));

  els.modeSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.addEventListener("click", () => setMode(button.dataset.mode));
  });

  els.breakRuleSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.addEventListener("click", () => setBreakRule(button.dataset.breakRule));
  });

  els.nameStyleSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.addEventListener("click", () => setNameStyle(button.dataset.nameStyle));
  });

  els.rosterAddForm.addEventListener("submit", (event) => {
    event.preventDefault();
    addRosterPlayer(els.rosterAddInput.value);
    els.rosterAddInput.value = "";
    els.rosterAddInput.focus();
  });

  if (boardView) {
    document.body.classList.add("view-board");
    window.addEventListener("resize", fitBoard);
    window.addEventListener("load", fitBoard);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(fitBoard);
  }
  if (isGuest) document.body.classList.add("view-guest");
  if (isWrappedApp) document.body.classList.add("view-app");
}

function registerServiceWorker() {
  // The Windows app already has the files; caching them again would only risk
  // serving yesterday's copy after an update.
  if (isWrappedApp) return;
  if ("serviceWorker" in navigator && location.protocol !== "file:") {
    window.addEventListener("load", () => {
      navigator.serviceWorker.register("sw.js").catch(() => {});
    });
  }
}

/* ------------------------------- install -------------------------------- */

// The companion server exists to get the app installed. Once the browser says
// it can be installed, offer a button here so nobody has to hunt through the
// browser menu; after that the app runs from its own cache with no server.
let installPrompt = null;

function wireInstall() {
  window.addEventListener("beforeinstallprompt", (event) => {
    event.preventDefault(); // keep the browser's own bar out of the way
    installPrompt = event;
    renderInstall();
  });
  window.addEventListener("appinstalled", () => {
    installPrompt = null;
    renderInstall();
  });
  els.installButton.addEventListener("click", runInstall);
}

function renderInstall() {
  const alreadyInstalled = window.matchMedia("(display-mode: standalone)").matches;
  els.installPanel.hidden = boardView || isGuest || isWrappedApp || alreadyInstalled || !installPrompt;
}

async function runInstall() {
  if (!installPrompt) return;
  const prompt = installPrompt;
  installPrompt = null; // a prompt can only be used once
  try {
    await prompt.prompt();
  } catch {
    // the browser refused to show it; the browser menu still works
  }
  renderInstall();
}

/* ----------------------------- state model ----------------------------- */

function createDefaultState() {
  const roster = defaultRosterNames.map((name, index) => ({ id: `r${index + 1}`, name }));
  return {
    version: 2,
    createdAt: new Date().toISOString(),
    nextRosterId: roster.length + 1,
    roster,
    mode: "doubles5",
    breakRule: "alternate",
    nameStyle: "first",
    modes: {
      doubles5: blankDoublesMode("doubles5", roster.slice(0, 5)),
      doubles4: blankDoublesMode("doubles4", roster.slice(0, 4)),
      singles: blankMatchMode("singles", roster.slice(0, 2)),
      match2: blankMatchMode("match2", roster.slice(0, 4))
    }
  };
}

function blankDoublesMode(mode, lineupPlayers) {
  const slots = MODES[mode].slots;
  const lineup = [];
  for (let i = 0; i < slots; i += 1) {
    lineup.push(lineupPlayers[i]?.id ?? null);
  }
  return {
    lineup,
    target: 3,
    currentSet: 1,
    scores: blankScores(mode)
  };
}

function blankMatchMode(mode, lineupPlayers) {
  const slots = MODES[mode].slots;
  const lineup = [];
  for (let i = 0; i < slots; i += 1) {
    lineup.push(lineupPlayers[i]?.id ?? null);
  }
  return {
    lineup,
    targetA: 3,
    targetB: 3,
    a: 0,
    b: 0,
    winner: null,
    breakSide: "a"
  };
}

function blankScores(mode) {
  return MODES[mode].schedule.map((match) => ({
    set: match.set,
    home: 0,
    away: 0,
    committed: false,
    winner: null,
    // The home side breaks unless this set's break has been swapped by hand.
    breakSwapped: false
  }));
}

function loadState() {
  let saved = readStorage(storageKey);
  if (saved) {
    try {
      return normalizeState(JSON.parse(saved));
    } catch {
      // fall through to legacy / default
    }
  }

  const legacy = readStorage(legacyKey);
  if (legacy) {
    try {
      return normalizeState(JSON.parse(legacy));
    } catch {
      // fall through to default
    }
  }

  return createDefaultState();
}

function readStorage(key) {
  try {
    const local = localStorage.getItem(key);
    if (local) return local;
  } catch {
    // ignore
  }
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function normalizeState(candidate) {
  if (candidate && candidate.players && !candidate.modes) {
    return migrateLegacyState(candidate);
  }

  const next = createDefaultState();

  if (candidate && Array.isArray(candidate.roster) && candidate.roster.length) {
    const seen = new Set();
    next.roster = candidate.roster
      .filter((player) => player && typeof player.id === "string")
      .map((player) => ({
        id: player.id,
        name: (typeof player.name === "string" && player.name.trim()) || "Player"
      }))
      .filter((player) => (seen.has(player.id) ? false : seen.add(player.id)));
    if (!next.roster.length) next.roster = createDefaultState().roster;
  }

  next.nextRosterId = highestRosterId(next.roster) + 1;
  next.createdAt = typeof candidate?.createdAt === "string" ? candidate.createdAt : next.createdAt;

  const rosterIds = new Set(next.roster.map((player) => player.id));

  ["doubles5", "doubles4"].forEach((mode) => {
    const saved = candidate?.modes?.[mode];
    next.modes[mode] = normalizeDoublesMode(mode, saved, rosterIds, next.roster);
  });
  ["singles", "match2"].forEach((mode) => {
    next.modes[mode] = normalizeMatchMode(mode, candidate?.modes?.[mode], rosterIds, next.roster);
  });

  next.mode = MODES[candidate?.mode] ? candidate.mode : "doubles5";
  next.breakRule = candidate?.breakRule === "winner" ? "winner" : "alternate";
  next.nameStyle = candidate?.nameStyle === "full" ? "full" : "first";
  return next;
}

function normalizeDoublesMode(mode, saved, rosterIds, roster) {
  const fresh = blankDoublesMode(mode, roster.slice(0, MODES[mode].slots));
  const result = fresh;
  result.target = clampNumber(saved?.target, 1, 9, 3);

  if (Array.isArray(saved?.lineup)) {
    result.lineup = fresh.lineup.map((fallback, index) => {
      const id = saved.lineup[index];
      return rosterIds.has(id) ? id : fallback;
    });
  }

  if (Array.isArray(saved?.scores)) {
    result.scores = MODES[mode].schedule.map((match) => {
      const savedScore = saved.scores.find((score) => score.set === match.set);
      return {
        set: match.set,
        home: clampNumber(savedScore?.home, 0, result.target, 0),
        away: clampNumber(savedScore?.away, 0, result.target, 0),
        committed: Boolean(savedScore?.committed),
        winner: savedScore?.winner === "home" || savedScore?.winner === "away" ? savedScore.winner : null,
        breakSwapped: Boolean(savedScore?.breakSwapped)
      };
    });
  }

  result.currentSet = resolveCurrentSet(mode, result.scores, saved?.currentSet);
  return result;
}

function normalizeMatchMode(mode, saved, rosterIds, roster) {
  const fresh = blankMatchMode(mode, roster.slice(0, MODES[mode].slots));
  fresh.targetA = clampNumber(saved?.targetA, 1, 9, 3);
  fresh.targetB = clampNumber(saved?.targetB, 1, 9, 3);
  if (Array.isArray(saved?.lineup)) {
    fresh.lineup = fresh.lineup.map((fallback, index) => {
      const id = saved.lineup[index];
      return rosterIds.has(id) ? id : fallback;
    });
  }
  fresh.a = clampNumber(saved?.a, 0, fresh.targetA, 0);
  fresh.b = clampNumber(saved?.b, 0, fresh.targetB, 0);
  fresh.winner = saved?.winner === "a" || saved?.winner === "b" ? saved.winner : null;
  fresh.breakSide = saved?.breakSide === "b" ? "b" : "a";
  return fresh;
}

function migrateLegacyState(candidate) {
  const next = createDefaultState();
  const names = Array.isArray(candidate.players) ? candidate.players : [];
  next.roster = defaultRosterNames.map((fallback, index) => ({
    id: `r${index + 1}`,
    name: (names[index]?.name && String(names[index].name).trim()) || fallback
  }));
  next.nextRosterId = next.roster.length + 1;
  next.createdAt = typeof candidate.createdAt === "string" ? candidate.createdAt : next.createdAt;

  const target = clampNumber(candidate.target, 1, 9, 3);
  const mode = next.modes.doubles5;
  mode.target = target;
  if (Array.isArray(candidate.scores)) {
    mode.scores = doubles5Schedule.map((match) => {
      const saved = candidate.scores.find((score) => score.set === match.set);
      return {
        set: match.set,
        home: clampNumber(saved?.home, 0, target, 0),
        away: clampNumber(saved?.away, 0, target, 0),
        committed: Boolean(saved?.committed),
        winner: saved?.winner === "home" || saved?.winner === "away" ? saved.winner : null,
        breakSwapped: false
      };
    });
  }
  mode.currentSet = resolveCurrentSet("doubles5", mode.scores, candidate.currentSet);
  next.mode = "doubles5";
  return next;
}

function resolveCurrentSet(mode, scores, savedCurrent) {
  const firstOpen = scores.find((score) => !score.committed);
  if (savedCurrent === null && !firstOpen) return null;
  const exists = MODES[mode].schedule.some((match) => match.set === savedCurrent);
  if (exists && !scores[savedCurrent - 1]?.committed) return savedCurrent;
  return firstOpen?.set ?? null;
}

function highestRosterId(roster) {
  return roster.reduce((max, player) => {
    const numeric = Number(String(player.id).replace(/\D/g, ""));
    return Number.isFinite(numeric) ? Math.max(max, numeric) : max;
  }, 0);
}

function saveState() {
  const serialized = JSON.stringify(state);
  try {
    localStorage.setItem(storageKey, serialized);
  } catch {
    try {
      sessionStorage.setItem(storageKey, serialized);
    } catch {
      // storage unavailable; in-memory only
    }
  }
}

/* ------------------------- mutations + live sync ------------------------ */

let pushInFlight = false;
let pushDirty = false;

// Apply a change locally, persist + render, then sync to the server (if any).
function mutate(action) {
  if (boardView) return; // spectator view is read-only
  action();
  render();
  scheduleSync();
}

// Coalesced sync: only one POST is ever in flight. Rapid taps collapse into a
// single update, so a lone scorer never conflicts with itself.
function scheduleSync() {
  if (!connected) return;
  pushDirty = true;
  if (!pushInFlight) runSync();
}

async function runSync() {
  pushInFlight = true;
  try {
    while (pushDirty) {
      pushDirty = false;
      let response;
      try {
        response = await fetch("api/state", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ baseRev: serverRev, clientId, state })
        });
      } catch {
        setConnected(false);
        return;
      }

      if (response.ok) {
        const data = await response.json();
        serverRev = data.rev;
      } else if (response.status === 409) {
        // Another device changed the shared state first: adopt it. Any fresh
        // local edit made meanwhile sets pushDirty and is sent on the next loop.
        const data = await response.json();
        serverRev = data.rev;
        if (data.state) {
          state = normalizeState(data.state);
          render();
        }
      } else {
        return; // unexpected error: keep local state, stop syncing
      }
    }
  } finally {
    pushInFlight = false;
  }
}

async function initSync() {
  if (location.protocol === "file:") {
    renderShare();
    return;
  }
  try {
    const response = await fetch("api/state", { cache: "no-store" });
    if (!response.ok) throw new Error("no sync");
    const data = await response.json();
    setConnected(true);
    serverRev = data.rev || 0;
    if (data.state) {
      state = normalizeState(data.state);
      render();
    } else if (!boardView) {
      scheduleSync(); // seed the empty server with our local session
    }
    openEventStream();
  } catch {
    setConnected(false);
  }
}

function openEventStream() {
  try {
    eventSource = new EventSource("api/events");
  } catch {
    return;
  }
  eventSource.addEventListener("state", (event) => {
    let data;
    try {
      data = JSON.parse(event.data);
    } catch {
      return;
    }
    serverRev = data.rev || 0;
    if (data.editor === clientId) return; // ignore our own echo to avoid flicker
    if (data.state) {
      state = normalizeState(data.state);
      render();
    }
  });
  eventSource.addEventListener("open", () => setConnected(true));
  eventSource.onerror = () => {
    // EventSource reconnects automatically; reflect status while it is down.
    if (eventSource.readyState === EventSource.CLOSED) setConnected(false);
  };
}

function setConnected(value) {
  if (connected === value) {
    renderShare();
    return;
  }
  connected = value;
  els.livePill.hidden = !connected;
  renderShare();
}

/* ------------------------------- actions ------------------------------- */

function setMode(mode) {
  if (isGuest) return;
  if (!MODES[mode] || state.mode === mode) return;
  mutate(() => {
    state.mode = mode;
  });
}

function setBreakRule(rule) {
  if (isGuest) return;
  const next = rule === "winner" ? "winner" : "alternate";
  if (state.breakRule === next) return;
  mutate(() => {
    state.breakRule = next;
  });
}

function setNameStyle(style) {
  if (isGuest) return;
  const next = style === "full" ? "full" : "first";
  if (state.nameStyle === next) return;
  mutate(() => {
    state.nameStyle = next;
  });
}

function adjustCurrentScore(team, delta) {
  if (MODES[state.mode].kind === "match") {
    adjustMatchScore(team === "home" ? "a" : "b", delta);
    return;
  }
  const current = getCurrentMatch();
  if (!current) return;
  const target = activeMode().target;
  const key = team === "home" ? "home" : "away";
  mutate(() => {
    current.score[key] = clampNumber(current.score[key] + delta, 0, target, 0);
  });
}

function adjustMatchScore(side, delta) {
  const match = activeMode();
  const target = side === "a" ? match.targetA : match.targetB;
  mutate(() => {
    match[side] = clampNumber(match[side] + delta, 0, target, 0);
    match.winner = match.a >= match.targetA ? "a" : match.b >= match.targetB ? "b" : null;
  });
}

function updateRaceTarget() {
  if (isGuest) return;
  const nextTarget = clampNumber(els.raceTarget.value, 1, 9, 3);
  els.raceTarget.value = String(nextTarget);
  mutate(() => {
    const mode = activeMode();
    mode.target = nextTarget;
    mode.scores.forEach((score) => {
      if (score.committed) return;
      score.home = clampNumber(score.home, 0, nextTarget, 0);
      score.away = clampNumber(score.away, 0, nextTarget, 0);
    });
  });
}

function updateMatchTarget(side, value) {
  if (isGuest) return;
  const nextTarget = clampNumber(value, 1, 9, 3);
  const input = side === "a" ? els.targetA : els.targetB;
  input.value = String(nextTarget);
  mutate(() => {
    const match = activeMode();
    if (side === "a") {
      match.targetA = nextTarget;
      match.a = clampNumber(match.a, 0, nextTarget, 0);
    } else {
      match.targetB = nextTarget;
      match.b = clampNumber(match.b, 0, nextTarget, 0);
    }
    match.winner = match.a >= match.targetA ? "a" : match.b >= match.targetB ? "b" : null;
  });
}

function commitCurrentScore() {
  const current = getCurrentMatch();
  if (!current) return;
  const target = activeMode().target;
  const { score } = current;
  if (Math.max(score.home, score.away) < target || score.home === score.away) {
    alert(`A team must reach ${target} and lead before committing.`);
    return;
  }
  mutate(() => {
    score.committed = true;
    score.winner = score.home > score.away ? "home" : "away";
    const mode = activeMode();
    const next = mode.scores.find((item) => !item.committed && item.set > score.set)
      || mode.scores.find((item) => !item.committed);
    mode.currentSet = next?.set ?? null;
  });
}

function undoLastCommit() {
  if (MODES[state.mode].kind !== "rr") return;
  const mode = activeMode();
  const last = [...mode.scores].filter((score) => score.committed).sort((a, b) => b.set - a.set)[0];
  if (!last) return;
  mutate(() => {
    last.committed = false;
    last.winner = null;
    mode.currentSet = last.set;
  });
}

function startNewMatch() {
  mutate(() => {
    const match = activeMode();
    match.a = 0;
    match.b = 0;
    match.winner = null;
  });
}

// Flip who breaks. In a one-off match this swaps the two sides for good (so the
// order players were picked in does not decide the break); in a round-robin it
// swaps the break for the current set only.
function swapBreak() {
  if (MODES[state.mode].kind === "match") {
    mutate(() => {
      const match = activeMode();
      match.breakSide = match.breakSide === "a" ? "b" : "a";
    });
    return;
  }
  const current = getCurrentMatch();
  if (!current) return;
  mutate(() => {
    current.score.breakSwapped = !current.score.breakSwapped;
  });
}

function setLineupSlot(mode, slotIndex, rosterId) {
  if (isGuest) return;
  mutate(() => {
    const lineup = state.modes[mode].lineup;
    const existingIndex = lineup.indexOf(rosterId);
    const previous = lineup[slotIndex];
    if (existingIndex !== -1 && existingIndex !== slotIndex) {
      lineup[existingIndex] = previous; // swap to keep the line-up free of duplicates
    }
    lineup[slotIndex] = rosterId || null;
    if (MODES[mode].kind === "match") {
      const m = state.modes[mode];
      m.a = 0;
      m.b = 0;
      m.winner = null;
    }
  });
}

function addRosterPlayer(rawName) {
  if (isGuest) return;
  const name = String(rawName || "").trim();
  if (!name) return;
  mutate(() => {
    const id = `r${state.nextRosterId}`;
    state.nextRosterId += 1;
    state.roster.push({ id, name });
    fillEmptyLineupSlots();
  });
}

function renameRosterPlayer(id, rawName) {
  if (isGuest) return;
  const name = String(rawName || "").trim();
  mutate(() => {
    const player = state.roster.find((entry) => entry.id === id);
    if (player) player.name = name || player.name;
  });
}

function removeRosterPlayer(id) {
  if (isGuest) return;
  if (!confirm("Remove this player from the list?")) return;
  mutate(() => {
    state.roster = state.roster.filter((player) => player.id !== id);
    if (!state.roster.length) {
      state.roster = createDefaultState().roster;
      state.nextRosterId = state.roster.length + 1;
    }
    Object.keys(MODES).forEach((mode) => {
      const lineup = state.modes[mode].lineup;
      for (let i = 0; i < lineup.length; i += 1) {
        if (lineup[i] === id) lineup[i] = null;
      }
    });
    fillEmptyLineupSlots();
  });
}

// Auto-assign unused roster players to any empty line-up slots.
function fillEmptyLineupSlots() {
  Object.keys(MODES).forEach((mode) => {
    const lineup = state.modes[mode].lineup;
    const used = new Set(lineup.filter(Boolean));
    const spare = state.roster.map((player) => player.id).filter((id) => !used.has(id));
    for (let i = 0; i < lineup.length; i += 1) {
      if (!lineup[i] && spare.length) {
        lineup[i] = spare.shift();
        used.add(lineup[i]);
      }
    }
  });
}

function resetSession() {
  if (isGuest) return;
  const mode = activeMode();
  const label = MODES[state.mode].label;
  if (!confirm(`Reset all ${label} scores?`)) return;
  mutate(() => {
    if (MODES[state.mode].kind === "match") {
      mode.a = 0;
      mode.b = 0;
      mode.winner = null;
    } else {
      mode.scores = blankScores(state.mode);
      mode.currentSet = MODES[state.mode].schedule[0].set;
    }
  });
}

/* ------------------------------- render -------------------------------- */

function render() {
  saveState();
  document.body.dataset.mode = state.mode;
  document.body.dataset.kind = MODES[state.mode].kind;
  document.body.dataset.breakRule = state.breakRule;
  document.body.dataset.nameStyle = state.nameStyle;
  refreshNameMap();
  renderSessionMeta();
  if (MODES[state.mode].kind === "match") {
    renderMatch();
  } else {
    renderDoublesMatch();
    renderProgressStrip();
    renderStandings();
    renderSchedule();
  }
  if (!boardView && !isGuest) {
    renderModeSwitch();
    renderBreakRule();
    renderNameStyle();
    renderLineupPickers();
    renderTargets();
    renderRoster();
  }
  if (boardView) fitBoard();
}

// Spectator board: scale the single scoreboard row so it fills the screen
// without overflowing, whatever the player names are.
function fitBoard() {
  const line = document.querySelector("#streamLine");
  if (!line) return;
  if (!window.innerWidth || !window.innerHeight) return; // not laid out yet
  const probe = Math.max(40, window.innerHeight * 0.12);
  line.style.fontSize = `${probe}px`;
  const contentWidth = line.scrollWidth;
  const contentHeight = line.offsetHeight;
  if (!contentWidth || !contentHeight) return;
  const scale = Math.min(
    (window.innerWidth * 0.94) / contentWidth,
    (window.innerHeight * 0.72) / contentHeight
  );
  line.style.fontSize = `${probe * Math.max(scale, 0.05)}px`;
}

function renderSessionMeta() {
  els.appTitle.textContent = `Pool - ${MODES[state.mode].label}`;

  if (MODES[state.mode].kind === "match") {
    const match = activeMode();
    els.sessionMeta.textContent = match.targetA === match.targetB
      ? `Race to ${match.targetA}`
      : `Race to ${match.targetA} / ${match.targetB}`;
    return;
  }

  const mode = activeMode();
  const schedule = MODES[state.mode].schedule;
  const completed = mode.scores.filter((score) => score.committed).length;
  els.sessionMeta.textContent = `Race to ${mode.target} - ${completed} of ${schedule.length} sets`;
}

function renderDoublesMatch() {
  const current = getCurrentMatch();
  const controls = document.querySelectorAll("[data-team][data-delta]");
  const mode = activeMode();

  // Home / away is only ever about the break, which has its own marker.
  els.homeLabel.hidden = true;
  els.awayLabel.hidden = true;
  els.commitButton.hidden = false;
  els.undoButton.hidden = false;
  els.newMatchButton.hidden = true;
  els.swapBreakButton.hidden = !alternateBreak();
  els.matchResult.hidden = true;

  if (!current) {
    const completed = mode.scores.filter((score) => score.committed).length;
    els.stripMeta.textContent = "All sets complete";
    els.stripSitting.hidden = false;
    els.stripSitting.textContent = "Complete";
    els.streamHome.textContent = "Final";
    els.streamHomeScore.textContent = String(completed);
    els.streamRaceNumber.textContent = `(${mode.target})`;
    els.streamAwayScore.textContent = String(MODES[state.mode].schedule.length);
    els.streamAway.textContent = "sets played";
    els.homeTeamName.textContent = "Session complete";
    els.awayTeamName.textContent = "Session complete";
    els.homeScore.textContent = "0";
    els.awayScore.textContent = "0";
    els.commitButton.disabled = true;
    els.swapBreakButton.disabled = true;
    showBreak(null);
    controls.forEach((button) => { button.disabled = true; });
    return;
  }

  const match = current.match;
  const score = current.score;
  const committable = Math.max(score.home, score.away) >= mode.target && score.home !== score.away;

  els.stripMeta.textContent = `Set ${match.set} - Round ${match.round}`;
  els.stripSitting.hidden = !match.sitting;
  if (match.sitting) els.stripSitting.textContent = `Sitting: ${nameForToken(match.sitting)}`;
  els.streamHome.textContent = streamTeamName(match.home);
  els.streamHomeScore.textContent = String(score.home);
  els.streamRaceNumber.textContent = `(${mode.target})`;
  els.streamAwayScore.textContent = String(score.away);
  els.streamAway.textContent = streamTeamName(match.away);
  setTeamNames(els.homeTeamName, match.home.map(nameForToken));
  setTeamNames(els.awayTeamName, match.away.map(nameForToken));
  els.homeScore.textContent = String(score.home);
  els.awayScore.textContent = String(score.away);
  els.commitButton.disabled = !committable;
  els.swapBreakButton.disabled = false;
  showBreak(rackBreakSide(firstBreakSide(score), score.home + score.away, committable));
  controls.forEach((button) => { button.disabled = false; });
}

// House rule: with alternate break the app can follow the break rack by rack.
// With winner breaks it cannot (only totals are recorded), so it shows nothing.
function alternateBreak() {
  return state.breakRule === "alternate";
}

// Which side breaks the first rack of a set: home unless the set was swapped.
function firstBreakSide(score) {
  return score.breakSwapped ? "away" : "home";
}

// Who breaks the rack about to be played. Alternate break hands the break over
// after every rack, so the racks already played decide it. `decided` means the
// race is already won and there is no next rack to break.
function rackBreakSide(first, racksPlayed, decided) {
  if (!alternateBreak() || decided) return null;
  if (racksPlayed % 2 === 0) return first;
  return first === "home" ? "away" : "home";
}

// A side's players, one name per line, so both read big on the scoring row.
function setTeamNames(el, names) {
  el.innerHTML = "";
  names.forEach((name) => {
    const line = document.createElement("span");
    line.className = "team-name-line";
    line.textContent = name.toUpperCase();
    el.append(line);
  });
}

// Paint the break marker on the team cards and the stream capture line.
function showBreak(side) {
  els.homeTeamCard.classList.toggle("breaking", side === "home");
  els.awayTeamCard.classList.toggle("breaking", side === "away");
  els.homeBreakBadge.hidden = side !== "home";
  els.awayBreakBadge.hidden = side !== "away";
  els.streamHomeBreak.hidden = side !== "home";
  els.streamAwayBreak.hidden = side !== "away";
}

// One marker per set, grouped into rounds: solid for the sets already played,
// tall for the set being played now.
function renderProgressStrip() {
  const mode = activeMode();
  els.stripTrack.innerHTML = "";
  let group = null;
  let round = null;

  MODES[state.mode].schedule.forEach((match) => {
    if (match.round !== round) {
      round = match.round;
      group = document.createElement("div");
      group.className = "strip-round";
      group.title = `Round ${round}`;
      els.stripTrack.append(group);
    }
    const score = getScore(match.set);
    const marker = document.createElement("span");
    const isCurrent = mode.currentSet === match.set;
    marker.className = `strip-seg${score.committed ? " done" : ""}${isCurrent ? " current" : ""}`;
    marker.title = `Set ${match.set} - Round ${match.round}`;
    group.append(marker);
  });
}

function renderMatch() {
  const match = activeMode();
  const single = MODES[state.mode].teamSize === 1;
  const names = matchSideNames();
  const controls = document.querySelectorAll("[data-team][data-delta]");

  const sideALabel = single ? "Player A" : "Team A";
  const sideBLabel = single ? "Player B" : "Team B";

  els.stripMeta.textContent = match.targetA === match.targetB
    ? `Race to ${match.targetA}`
    : `Handicap - ${sideALabel} to ${match.targetA}, ${sideBLabel} to ${match.targetB}`;
  els.stripSitting.hidden = true;

  // Only worth a label when the two sides are racing to different numbers.
  const handicap = match.targetA !== match.targetB;
  els.homeLabel.hidden = !handicap;
  els.awayLabel.hidden = !handicap;
  els.homeLabel.textContent = `Race to ${match.targetA}`;
  els.awayLabel.textContent = `Race to ${match.targetB}`;
  setTeamNames(els.homeTeamName, names.a);
  setTeamNames(els.awayTeamName, names.b);
  els.homeScore.textContent = String(match.a);
  els.awayScore.textContent = String(match.b);

  els.streamHome.textContent = names.aStream;
  els.streamHomeScore.textContent = String(match.a);
  els.streamRaceNumber.textContent = match.targetA === match.targetB
    ? `(${match.targetA})`
    : `(${match.targetA} / ${match.targetB})`;
  els.streamAwayScore.textContent = String(match.b);
  els.streamAway.textContent = names.bStream;

  const winnerName = match.winner === "a" ? names.aName : match.winner === "b" ? names.bName : null;
  els.matchResult.hidden = !winnerName;
  if (winnerName) els.matchResult.textContent = `${winnerName} wins!`;

  els.commitButton.hidden = true;
  els.undoButton.hidden = true;
  els.newMatchButton.hidden = false;
  els.swapBreakButton.hidden = !alternateBreak();
  els.swapBreakButton.disabled = false;
  showBreak(rackBreakSide(match.breakSide === "b" ? "away" : "home", match.a + match.b, Boolean(match.winner)));

  controls.forEach((button) => {
    const isPlus = Number(button.dataset.delta) > 0;
    button.disabled = Boolean(match.winner) && isPlus;
  });
}

// Names for each side of a one-off match, using the active mode's name map.
function matchSideNames() {
  const ts = MODES[state.mode].teamSize;
  const m = activeMode();
  const a = [];
  const b = [];
  for (let i = 0; i < ts; i += 1) {
    a.push(nameMap.get(m.lineup[i]) || (ts === 1 ? "Player A" : `A${i + 1}`));
    b.push(nameMap.get(m.lineup[ts + i]) || (ts === 1 ? "Player B" : `B${i + 1}`));
  }
  return {
    a,
    b,
    aName: a.join(" / "),
    bName: b.join(" / "),
    aStream: a.map((s) => s.toUpperCase()).join(" | "),
    bStream: b.map((s) => s.toUpperCase()).join(" | ")
  };
}

function renderStandings() {
  const standings = calculateStandings();
  const mode = activeMode();
  const schedule = MODES[state.mode].schedule;
  const completed = mode.scores.filter((score) => score.committed).length;
  const highWins = standings.length ? Math.max(...standings.map((row) => row.wins)) : 0;
  const leaders = highWins > 0 ? standings.filter((row) => row.wins === highWins).map((row) => row.name) : [];

  els.progressBadge.textContent = `${completed} / ${schedule.length}`;
  els.leaderLabel.textContent = leaders.length ? `${leaders.join(" / ")} leading` : "No leader";
  els.standingsBody.innerHTML = "";

  standings.forEach((row) => {
    const tr = document.createElement("tr");
    addCell(tr, row.name);
    addCell(tr, `${row.wins}/${row.played}`, "wins-cell");
    addCell(tr, row.possible, "muted-col");
    els.standingsBody.append(tr);
  });
}

function renderSchedule() {
  const current = getCurrentMatch();
  const mode = activeMode();
  const schedule = MODES[state.mode].schedule;
  const completed = mode.scores.filter((score) => score.committed).length;

  els.scheduleMeta.textContent = alternateBreak()
    ? `${completed} complete, ${schedule.length - completed} remaining - ${breakDot} breaks first`
    : `${completed} complete, ${schedule.length - completed} remaining`;
  els.activeSetBadge.textContent = current ? `Set ${current.match.set}` : "Done";
  els.scheduleBody.innerHTML = "";

  schedule.forEach((match) => {
    const score = getScore(match.set);
    const isActive = current?.match.set === match.set;
    const breaking = alternateBreak() ? firstBreakSide(score) : null;
    const row = document.createElement("tr");
    row.className = `schedule-row${isActive ? " active" : ""}${score.committed ? " done" : ""}`;

    addCell(row, match.set);
    addCell(row, match.round);
    addBreakableTeamCell(row, match.home, breaking === "home");
    addCell(row, score.committed || isActive || score.home || score.away ? `${score.home} - ${score.away}` : "-", "score-cell");
    addBreakableTeamCell(row, match.away, breaking === "away");
    addCell(row, match.sitting ? nameForToken(match.sitting) : "-", "sit-col");

    const statusCell = document.createElement("td");
    const status = document.createElement("span");
    status.className = `status-pill${isActive ? " active" : ""}${score.committed ? " done" : ""}`;
    status.textContent = score.committed ? "Done" : isActive ? "Current" : "Waiting";
    statusCell.append(status);
    row.append(statusCell);

    if (!score.committed && !boardView && !isGuest) {
      row.addEventListener("click", () => {
        mutate(() => {
          activeMode().currentSet = match.set;
        });
      });
    }

    els.scheduleBody.append(row);
  });
}

function renderModeSwitch() {
  const modeHints = {
    doubles5: "Round-robin doubles, 5 players - one sits out each set, 15 sets over 3 rounds.",
    doubles4: "Round-robin doubles, 4 players - everyone partners everyone once per round, 9 sets, nobody sits.",
    singles: "One-off 1 v 1. Pick two players; each can race to a different number (handicap).",
    match2: "One-off 2 v 2. Pick two pairs; each team can race to a different number (handicap)."
  };
  // Only worth saying when the app is tracking the break.
  const breakNotes = {
    doubles5: "Everyone plays 12 sets and breaks first in 6 of them.",
    doubles4: "9 sets share 18 first breaks between 4 players, so a dead-even split is impossible: P1 breaks first 6 times, the rest 4.",
    singles: "Swap break decides who breaks the first rack, so the pick order does not.",
    match2: "Swap break decides which team breaks the first rack, so the pick order does not."
  };
  els.modeSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.classList.toggle("active", button.dataset.mode === state.mode);
  });
  const hint = modeHints[state.mode] || "";
  const note = alternateBreak() ? breakNotes[state.mode] : "";
  els.modeHint.textContent = note ? `${hint} ${note}` : hint;
}

function renderBreakRule() {
  const hints = {
    alternate: `Break changes hands after every rack. ${breakDot} shows who breaks next; Swap break flips it if the wrong side started.`,
    winner: "Whoever wins a rack breaks the next one. The app only records totals, so it cannot follow the break - no break marker is shown."
  };
  els.breakRuleSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.classList.toggle("active", button.dataset.breakRule === state.breakRule);
  });
  els.breakRuleHint.textContent = hints[state.breakRule] || "";
}

function renderNameStyle() {
  const hints = {
    first: "Shows each player's first name, adding just enough of the surname to tell two Daves apart. A 1 v 1 always shows the whole name.",
    full: "Shows every word of the name - use this when the player list holds team names rather than people."
  };
  els.nameStyleSwitch.querySelectorAll(".mode-button").forEach((button) => {
    button.classList.toggle("active", button.dataset.nameStyle === state.nameStyle);
  });
  els.nameStyleHint.textContent = hints[state.nameStyle] || "";
}

function lineupSlotLabel(mode, i) {
  const cfg = MODES[mode];
  if (cfg.kind === "rr") return `P${i + 1}`;
  if (cfg.teamSize === 1) return i === 0 ? "Player A" : "Player B";
  return `Team ${i < cfg.teamSize ? "A" : "B"} (${(i % cfg.teamSize) + 1})`;
}

function renderLineupPickers() {
  els.lineupPickers.innerHTML = "";
  const mode = state.mode;
  const slots = MODES[mode].slots;
  const lineup = state.modes[mode].lineup;

  for (let i = 0; i < slots; i += 1) {
    const field = document.createElement("label");
    field.className = "player-field";
    const span = document.createElement("span");
    span.textContent = lineupSlotLabel(mode, i);
    field.append(span);

    const select = document.createElement("select");
    const blank = document.createElement("option");
    blank.value = "";
    blank.textContent = "- choose -";
    select.append(blank);

    state.roster.forEach((player) => {
      const option = document.createElement("option");
      option.value = player.id;
      option.textContent = player.name;
      if (lineup[i] === player.id) option.selected = true;
      select.append(option);
    });

    select.addEventListener("change", () => setLineupSlot(mode, i, select.value));
    field.append(select);
    els.lineupPickers.append(field);
  }
}

function renderTargets() {
  const isMatch = MODES[state.mode].kind === "match";
  els.doublesRaceField.hidden = isMatch;
  els.singlesHandicaps.hidden = !isMatch;

  if (isMatch) {
    const match = activeMode();
    const single = MODES[state.mode].teamSize === 1;
    const names = matchSideNames();
    els.targetALabel.textContent = single ? `${names.aName} to` : "Team A to";
    els.targetBLabel.textContent = single ? `${names.bName} to` : "Team B to";
    if (document.activeElement !== els.targetA) els.targetA.value = String(match.targetA);
    if (document.activeElement !== els.targetB) els.targetB.value = String(match.targetB);
  } else if (document.activeElement !== els.raceTarget) {
    els.raceTarget.value = String(activeMode().target);
  }
}

function renderRoster() {
  els.rosterList.innerHTML = "";
  state.roster.forEach((player) => {
    const row = document.createElement("div");
    row.className = "roster-row";

    const input = document.createElement("input");
    input.value = player.name;
    input.autocomplete = "off";
    input.maxLength = 24;
    input.addEventListener("change", () => renameRosterPlayer(player.id, input.value));
    row.append(input);

    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "roster-remove";
    remove.textContent = "Remove";
    remove.title = `Remove ${player.name}`;
    remove.addEventListener("click", () => removeRosterPlayer(player.id));
    row.append(remove);

    els.rosterList.append(row);
  });
}

function renderShare() {
  if (boardView || isGuest || isWrappedApp) return; // no server behind the app
  if (location.protocol === "file:") {
    els.shareStatus.textContent = "Local only";
    els.shareBody.innerHTML = "<p class=\"share-hint\">Scoring works exactly as it does here. To let phones on the same wifi join in, run install-or-update.cmd on this PC and leave that window open.</p>";
    return;
  }

  if (!connected) {
    els.shareStatus.textContent = "Off - scoring on this device only";
    els.shareBody.innerHTML = "<p class=\"share-hint\">This is the normal way to run the app: everything works and scores are saved on this device. To let phones on the same wifi join in, run install-or-update.cmd on this PC and leave that window open.</p>";
    return;
  }

  els.shareStatus.textContent = "Live - players scan to score from their phones";
  els.shareBody.innerHTML = "";
  const list = document.createElement("div");
  list.className = "share-list";
  els.shareBody.append(list);

  const spectator = `${location.origin}/?view=board`;

  fetch("api/info", { cache: "no-store" })
    .then((response) => (response.ok ? response.json() : null))
    .then((info) => {
      const urls = info && Array.isArray(info.urls) ? info.urls : [];
      const primary = urls[0];
      const qr = primary ? qrBlock(primary) : null;
      if (qr) {
        list.append(qr);
      } else if (primary) {
        // QR could not be generated - fall back to the address so sharing still works
        list.append(shareRow("Join on a phone", primary));
      }
      const hint = document.createElement("p");
      hint.className = "share-hint";
      hint.textContent = "Same wifi only. Phones get a score-only view - they can update the score but not change the format or players.";
      list.append(hint);
      list.append(shareRow("Spectator view (read-only)", spectator));
    })
    .catch(() => {
      list.append(shareRow("Spectator view (read-only)", spectator));
    });
}

// Build a scannable QR code for a join URL (uses the vendored qrcode library).
function qrBlock(url) {
  if (typeof qrcode === "undefined") return null;
  let svg;
  try {
    const qr = qrcode(0, "M");
    qr.addData(url);
    qr.make();
    svg = qr.createSvgTag({ cellSize: 6, margin: 2, scalable: true });
  } catch {
    return null;
  }
  const wrap = document.createElement("div");
  wrap.className = "share-qr";
  const caption = document.createElement("p");
  caption.className = "share-subhead";
  caption.textContent = "Scan to score from a phone:";
  wrap.append(caption);
  const box = document.createElement("div");
  box.className = "share-qr-box";
  box.innerHTML = svg;
  wrap.append(box);
  return wrap;
}

function shareRow(label, url) {
  const row = document.createElement("div");
  row.className = "share-row";
  if (label) {
    const tag = document.createElement("span");
    tag.className = "share-tag";
    tag.textContent = label;
    row.append(tag);
  }
  const code = document.createElement("code");
  code.textContent = url;
  row.append(code);

  const copy = document.createElement("button");
  copy.type = "button";
  copy.className = "ghost-button share-copy";
  copy.textContent = "Copy";
  copy.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(url);
      copy.textContent = "Copied";
      setTimeout(() => { copy.textContent = "Copy"; }, 1500);
    } catch {
      copy.textContent = "Copy failed";
    }
  });
  row.append(copy);
  return row;
}

/* ------------------------------ standings ------------------------------ */

function calculateStandings() {
  const mode = activeMode();
  const schedule = MODES[state.mode].schedule;
  const slots = MODES[state.mode].slots;

  const rows = [];
  for (let i = 0; i < slots; i += 1) {
    const token = `p${i + 1}`;
    rows.push({
      token,
      name: nameForToken(token),
      wins: 0,
      played: 0,
      total: 0
    });
  }
  const byToken = (token) => rows.find((row) => row.token === token);

  // Total = every set this player is down to play, whether played yet or not.
  schedule.forEach((match) => {
    [...match.home, ...match.away].forEach((token) => { byToken(token).total += 1; });
  });

  mode.scores.forEach((score) => {
    if (!score.committed) return;
    const match = schedule.find((item) => item.set === score.set);
    const winners = score.winner === "home" ? match.home : match.away;
    [...match.home, ...match.away].forEach((token) => { byToken(token).played += 1; });
    winners.forEach((token) => { byToken(token).wins += 1; });
  });

  // Possible = where they finish if they win every set they have left.
  rows.forEach((row) => { row.possible = row.wins + (row.total - row.played); });

  return rows.sort((a, b) => b.wins - a.wins || b.played - a.played || a.name.localeCompare(b.name));
}

/* ------------------------------- helpers ------------------------------- */

function activeMode() {
  return state.modes[state.mode];
}

function getCurrentMatch() {
  if (MODES[state.mode].kind === "match") return null;
  const mode = activeMode();
  if (mode.currentSet === null) return null;
  const match = MODES[state.mode].schedule.find((item) => item.set === mode.currentSet);
  if (!match) return null;
  return { match, score: getScore(match.set) };
}

function getScore(set) {
  return activeMode().scores.find((score) => score.set === set);
}

function tokenIndex(token) {
  return Number(String(token).slice(1)) - 1;
}

function nameForToken(token) {
  const idx = tokenIndex(token);
  const id = activeMode().lineup[idx];
  return nameMap.get(id) || `P${idx + 1}`;
}

function rosterName(id) {
  return state.roster.find((player) => player.id === id)?.name || null;
}

/* --------------------------- name display ------------------------------ */

// Rebuild the id -> display-name map for the current mode's line-up. First names
// keep the scoreboard short; full names are there for team names, where every
// word matters. A 1 v 1 always has room for the whole name.
function refreshNameMap() {
  const cfg = MODES[state.mode];
  const useFull = state.nameStyle === "full" || (cfg.kind === "match" && cfg.teamSize === 1);
  nameMap = buildNameMap(activeMode().lineup, useFull);
}

function parseName(raw) {
  const parts = String(raw || "").trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return { first: "", last: "" };
  return { first: parts[0], last: parts.slice(1).join(" ") };
}

// Map each roster id in `ids` to a display name. With useFull, the whole name
// is shown. Otherwise show the first name, and where two players in the set
// share a first name, append the shortest last-name prefix that tells them apart.
function buildNameMap(ids, useFull) {
  const map = new Map();
  const entries = ids
    .filter(Boolean)
    .map((id) => ({ id, raw: (rosterName(id) || "").trim() }));

  if (useFull) {
    entries.forEach((entry) => map.set(entry.id, entry.raw));
    return map;
  }

  const groups = new Map();
  entries.forEach((entry) => {
    const parsed = parseName(entry.raw);
    entry.first = parsed.first;
    entry.last = parsed.last;
    const key = parsed.first.toLowerCase();
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(entry);
  });

  groups.forEach((group) => {
    if (group.length === 1) {
      const only = group[0];
      map.set(only.id, only.first || only.raw);
      return;
    }
    group.forEach((entry) => {
      const prefix = uniqueLastPrefix(entry, group);
      map.set(entry.id, prefix ? `${entry.first} ${prefix}` : (entry.first || entry.raw));
    });
  });

  return map;
}

// Shortest leading slice of this entry's last name that is unique within the
// group (others sharing the same first name). Falls back to the full last name.
function uniqueLastPrefix(entry, group) {
  const last = entry.last || "";
  if (!last) return "";
  const others = group.filter((other) => other.id !== entry.id);
  for (let len = 1; len <= last.length; len += 1) {
    const prefix = last.slice(0, len).toLowerCase();
    const collides = others.some((other) => (other.last || "").slice(0, len).toLowerCase() === prefix);
    if (!collides) return last.slice(0, len);
  }
  return last;
}

function teamName(tokens) {
  return tokens.map(nameForToken).join(" / ");
}

function streamTeamName(tokens) {
  return tokens.map((token) => nameForToken(token).toUpperCase()).join(" | ");
}

function addCell(row, value, className = "") {
  const cell = document.createElement("td");
  if (className) cell.className = className;
  cell.textContent = String(value);
  row.append(cell);
}

// A schedule team cell, with the break dot in front of the side that breaks.
function addBreakableTeamCell(row, tokens, breaking) {
  const cell = document.createElement("td");
  cell.className = "team-cell";
  const dot = document.createElement("span");
  dot.className = "break-dot";
  dot.textContent = breakDot;
  dot.hidden = !breaking;
  if (breaking) dot.title = "Breaks first";
  cell.append(dot, document.createTextNode(teamName(tokens)));
  row.append(cell);
}

function clampNumber(value, min, max, fallback) {
  const number = Number(value);
  if (!Number.isFinite(number)) return fallback;
  return Math.min(max, Math.max(min, Math.round(number)));
}

function makeClientId() {
  try {
    if (crypto && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  } catch {
    // ignore
  }
  return `c${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
}
