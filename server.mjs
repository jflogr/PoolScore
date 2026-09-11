import { createReadStream, existsSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { networkInterfaces } from "node:os";
import { dirname, extname, join, normalize, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const port = 4174;
const host = "0.0.0.0"; // accept connections from other devices on the LAN
const sessionFile = join(root, "session.json");

const types = {
  ".css": "text/css",
  ".html": "text/html",
  ".ico": "image/x-icon",
  ".js": "text/javascript",
  ".json": "application/json",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".webmanifest": "application/manifest+json"
};

// Shared session held in memory and mirrored to disk so a restart keeps the game.
const session = loadSession();
const sseClients = new Set();

function loadSession() {
  try {
    if (existsSync(sessionFile)) {
      const parsed = JSON.parse(readFileSync(sessionFile, "utf8"));
      return {
        rev: Number.isFinite(parsed.rev) ? parsed.rev : 0,
        state: parsed.state ?? null,
        editor: parsed.editor ?? null
      };
    }
  } catch {
    // start fresh on any read/parse problem
  }
  return { rev: 0, state: null, editor: null };
}

function persistSession() {
  try {
    writeFileSync(sessionFile, JSON.stringify(session));
  } catch {
    // disk not writable: keep running from memory
  }
}

function broadcastState() {
  const payload = JSON.stringify({ rev: session.rev, state: session.state, editor: session.editor });
  const message = `event: state\ndata: ${payload}\n\n`;
  for (const client of sseClients) {
    try {
      client.write(message);
    } catch {
      sseClients.delete(client);
    }
  }
}

function readBody(request) {
  return new Promise((resolveBody, rejectBody) => {
    let data = "";
    request.on("data", (chunk) => {
      data += chunk;
      if (data.length > 2_000_000) {
        rejectBody(new Error("payload too large"));
        request.destroy();
      }
    });
    request.on("end", () => resolveBody(data));
    request.on("error", rejectBody);
  });
}

function sendJson(response, status, body) {
  response.writeHead(status, {
    "Content-Type": "application/json",
    "Cache-Control": "no-store"
  });
  response.end(JSON.stringify(body));
}

function lanUrls() {
  const addresses = [];
  const interfaces = networkInterfaces();
  for (const entries of Object.values(interfaces)) {
    for (const entry of entries ?? []) {
      const isIPv4 = entry.family === "IPv4" || entry.family === 4;
      if (isIPv4 && !entry.internal) addresses.push(entry.address);
    }
  }
  addresses.sort((a, b) => lanRank(a) - lanRank(b));
  return addresses.map((address) => `http://${address}:${port}`);
}

// Prefer typical home/office LAN ranges over virtual adapters (Hyper-V and WSL
// commonly use 172.x), so the QR code points at a phone-reachable address.
function lanRank(address) {
  if (address.startsWith("192.168.")) return 0;
  if (address.startsWith("10.")) return 1;
  if (/^172\.(1[6-9]|2\d|3[01])\./.test(address)) return 2;
  return 3;
}

async function handleApi(request, response, pathname) {
  if (pathname === "/api/state" && request.method === "GET") {
    sendJson(response, 200, { rev: session.rev, state: session.state });
    return;
  }

  if (pathname === "/api/state" && request.method === "POST") {
    let payload;
    try {
      payload = JSON.parse(await readBody(request));
    } catch {
      sendJson(response, 400, { ok: false, error: "bad request" });
      return;
    }

    const baseRev = Number(payload.baseRev);
    if (session.state === null || baseRev === session.rev) {
      session.rev += 1;
      session.state = payload.state ?? null;
      session.editor = typeof payload.clientId === "string" ? payload.clientId : null;
      persistSession();
      broadcastState();
      sendJson(response, 200, { ok: true, rev: session.rev });
    } else {
      sendJson(response, 409, { ok: false, rev: session.rev, state: session.state });
    }
    return;
  }

  if (pathname === "/api/info" && request.method === "GET") {
    sendJson(response, 200, { urls: lanUrls(), rev: session.rev });
    return;
  }

  if (pathname === "/api/events" && request.method === "GET") {
    response.writeHead(200, {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive"
    });
    response.write("retry: 3000\n\n");
    response.write(`event: state\ndata: ${JSON.stringify({ rev: session.rev, state: session.state, editor: session.editor })}\n\n`);
    sseClients.add(response);
    request.on("close", () => sseClients.delete(response));
    return;
  }

  sendJson(response, 404, { ok: false, error: "not found" });
}

const server = createServer((request, response) => {
  const url = new URL(request.url ?? "/", `http://localhost:${port}`);
  const pathname = url.pathname;

  if (pathname.startsWith("/api/")) {
    handleApi(request, response, pathname).catch(() => {
      sendJson(response, 500, { ok: false, error: "server error" });
    });
    return;
  }

  const requested = normalize(pathname).replace(/^[/\\]+/, "");
  let filePath = resolve(join(root, requested));

  if (!filePath.startsWith(root)) {
    response.writeHead(403);
    response.end("Forbidden");
    return;
  }

  if (!existsSync(filePath) || statSync(filePath).isDirectory()) {
    filePath = join(root, "index.html");
  }

  response.writeHead(200, {
    "Content-Type": types[extname(filePath)] ?? "application/octet-stream"
  });
  createReadStream(filePath).pipe(response);
});

// Keep SSE connections alive through idle proxies/firewalls.
setInterval(() => {
  for (const client of sseClients) {
    try {
      client.write(": ping\n\n");
    } catch {
      sseClients.delete(client);
    }
  }
}, 25000);

server.listen(port, host, () => {
  const urls = lanUrls();
  console.log("Pool Score Tracker - install / update server");
  console.log("");
  console.log(`  Open on this PC:  http://localhost:${port}`);
  console.log("  Click \"Install app\" on the page (or the install icon in the address bar).");
  console.log("  Once it is installed you can close this window - the app runs on its own");
  console.log("  and does not need this server again until the code changes.");
  if (urls.length) {
    console.log("");
    console.log("  While this window is open, phones on the same wifi can also join:");
    urls.forEach((url) => console.log(`     ${url}`));
    console.log(`  Spectator / stream view:  ${urls[0]}/?view=board`);
  }
});
