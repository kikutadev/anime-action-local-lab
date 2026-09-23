import fs from "fs";
import http from "http";
import os from "os";
import path from "path";
import { execFileSync, spawn } from "child_process";
import { fileURLToPath } from "url";

export const QA_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function resolveChromeExecutable() {
  if (process.env.QA_CHROME) return process.env.QA_CHROME;

  const cacheRoot = path.join(os.homedir(), "Library", "Caches", "ms-playwright");
  if (fs.existsSync(cacheRoot)) {
    const versions = fs.readdirSync(cacheRoot)
      .map(name => {
        const match = /^chromium_headless_shell-(\d+)$/.exec(name);
        return match ? { name, version: Number(match[1]) } : null;
      })
      .filter(Boolean)
      .sort((a, b) => b.version - a.version);

    for (const entry of versions) {
      const executable = path.join(
        cacheRoot,
        entry.name,
        "chrome-headless-shell-mac-arm64",
        "chrome-headless-shell",
      );
      if (fs.existsSync(executable)) return executable;
    }
  }

  throw new Error(
    "Dedicated Playwright chrome-headless-shell was not found. Install the Playwright Chromium runtime or set QA_CHROME.",
  );
}

export const DEFAULT_CHROME = resolveChromeExecutable();

const MIME_TYPES = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".data", "application/octet-stream"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".jpeg", "image/jpeg"],
  [".svg", "image/svg+xml"],
  [".vrm", "application/octet-stream"],
]);

export class QaTimeoutError extends Error {
  constructor(message) {
    super(message);
    this.name = "QaTimeoutError";
  }
}

export function sleep(ms, signal) {
  if (signal?.aborted) {
    return Promise.reject(signal.reason ?? new Error("QA runtime aborted"));
  }

  return new Promise((resolve, reject) => {
    const timer = setTimeout(done, ms);
    const onAbort = () => {
      clearTimeout(timer);
      signal?.removeEventListener("abort", onAbort);
      reject(signal.reason ?? new Error("QA runtime aborted"));
    };

    function done() {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }

    signal?.addEventListener("abort", onAbort, { once: true });
  });
}

function isAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

export function isProcessGroupAlive(groupId) {
  if (!Number.isInteger(groupId) || groupId <= 0) return false;
  try {
    process.kill(-groupId, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

async function waitUntil(predicate, timeoutMs, intervalMs = 50) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return true;
    await sleep(intervalMs);
  }
  return predicate();
}

function findProfileProcesses(profileDir) {
  try {
    const output = execFileSync("/bin/ps", ["-axo", "pid=,command="], { encoding: "utf8" });
    return output
      .split(/\r?\n/)
      .filter(Boolean)
      .map(line => {
        const match = /^\s*(\d+)\s+(.*)$/.exec(line);
        return match ? { pid: Number(match[1]), command: match[2] } : null;
      })
      .filter(Boolean)
      .filter(processInfo => processInfo.command.includes(profileDir));
  } catch {
    return [];
  }
}

async function terminateOwnedChrome(chrome, profileDir, graceMs = 1500) {
  if (!chrome?.pid) return;
  const groupId = chrome.pid;

  if (isAlive(chrome.pid)) {
    try {
      chrome.kill("SIGTERM");
    } catch {}
  }

  await waitUntil(
    () => !isProcessGroupAlive(groupId) && findProfileProcesses(profileDir).length === 0,
    graceMs,
  );

  const residualProfileProcesses = findProfileProcesses(profileDir);
  for (const processInfo of residualProfileProcesses) {
    try {
      process.kill(processInfo.pid, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  if (isProcessGroupAlive(groupId)) {
    // Chrome owns a dedicated process group created only for this QA run.
    // This is narrower than pkill and cannot target the user's normal Chrome.
    try {
      process.kill(-groupId, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  await waitUntil(
    () => !isProcessGroupAlive(groupId) && findProfileProcesses(profileDir).length === 0,
    1000,
  );

  const survivors = findProfileProcesses(profileDir);
  if (isProcessGroupAlive(groupId) || survivors.length > 0) {
    throw new Error(
      `QA Chrome cleanup incomplete: groupAlive=${isProcessGroupAlive(groupId)} profilePids=${survivors.map(x => x.pid).join(",")}`,
    );
  }
}

function safeStaticPath(root, requestUrl) {
  const url = new URL(requestUrl ?? "/", "http://127.0.0.1");
  let pathname = decodeURIComponent(url.pathname);
  if (pathname.endsWith("/")) pathname += "index.html";

  const absolute = path.resolve(root, "." + pathname);
  const rootWithSep = root.endsWith(path.sep) ? root : root + path.sep;
  if (absolute !== root && !absolute.startsWith(rootWithSep)) return null;
  return absolute;
}

async function startStaticServer(root) {
  const sockets = new Set();
  const server = http.createServer((req, res) => {
    const file = safeStaticPath(root, req.url);
    if (!file) {
      res.writeHead(403);
      res.end("Forbidden");
      return;
    }

    fs.stat(file, (statError, stat) => {
      if (statError || !stat.isFile()) {
        res.writeHead(404);
        res.end("Not found");
        return;
      }

      const headers = {
        "Content-Type": MIME_TYPES.get(path.extname(file).toLowerCase()) ?? "application/octet-stream",
        "Cache-Control": "no-store",
      };
      res.writeHead(200, headers);

      if (req.method === "HEAD") {
        res.end();
        return;
      }

      const stream = fs.createReadStream(file);
      stream.on("error", () => {
        if (!res.headersSent) res.writeHead(500);
        res.end();
      });
      stream.pipe(res);
    });
  });

  server.on("connection", socket => {
    sockets.add(socket);
    socket.once("close", () => sockets.delete(socket));
  });

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      server.off("error", reject);
      resolve();
    });
  });

  const address = server.address();
  if (!address || typeof address === "string") {
    throw new Error("QA HTTP server did not expose a TCP port");
  }

  return {
    port: address.port,
    async close() {
      if (!server.listening) return;
      server.closeAllConnections?.();
      for (const socket of sockets) socket.destroy();
      await new Promise(resolve => server.close(() => resolve()));
    },
  };
}

async function waitForFile(file, timeoutMs, signal, chrome) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (signal?.aborted) throw signal.reason;
    if (chrome.exitCode !== null) {
      throw new Error(`QA Chrome exited before DevTools became ready (exit=${chrome.exitCode})`);
    }
    if (fs.existsSync(file) && fs.statSync(file).size > 0) return;
    await sleep(50, signal);
  }
  throw new QaTimeoutError(`Timed out waiting for ${file}`);
}

async function fetchTargets(debugPort, pageUrl, timeoutMs, signal) {
  const deadline = Date.now() + timeoutMs;
  let lastError;

  while (Date.now() < deadline) {
    if (signal?.aborted) throw signal.reason;
    try {
      const response = await fetch(`http://127.0.0.1:${debugPort}/json`, { signal });
      if (!response.ok) throw new Error(`CDP target query failed: HTTP ${response.status}`);
      const targets = await response.json();
      const page = targets.find(target => target.type === "page" && target.url.startsWith(pageUrl));
      if (page?.webSocketDebuggerUrl) return page;
    } catch (error) {
      if (signal?.aborted) throw signal.reason;
      lastError = error;
    }
    await sleep(75, signal);
  }

  throw new QaTimeoutError(
    `Timed out waiting for QA page target at ${pageUrl}${lastError ? `: ${lastError.message}` : ""}`,
  );
}

class CdpClient {
  constructor(ws, signal) {
    this.ws = ws;
    this.signal = signal;
    this.nextId = 1;
    this.pending = new Map();
    this.logs = [];
    this.closed = false;

    this.onMessage = event => {
      const message = JSON.parse(event.data);
      if (message.id && this.pending.has(message.id)) {
        const handler = this.pending.get(message.id);
        this.pending.delete(message.id);
        message.error
          ? handler.reject(new Error(JSON.stringify(message.error)))
          : handler.resolve(message.result);
        return;
      }

      if (message.method === "Runtime.consoleAPICalled") {
        this.logs.push((message.params.args ?? []).map(arg => arg.value ?? arg.description ?? "").join(" "));
      } else if (message.method === "Runtime.exceptionThrown") {
        this.logs.push("EXCEPTION " + JSON.stringify(message.params.exceptionDetails));
      } else if (message.method === "Log.entryAdded") {
        this.logs.push("LOG " + message.params.entry.level + " " + message.params.entry.text);
      }
    };

    this.onClose = () => {
      this.closed = true;
      const error = new Error("CDP WebSocket closed");
      for (const handler of this.pending.values()) handler.reject(error);
      this.pending.clear();
    };

    ws.addEventListener("message", this.onMessage);
    ws.addEventListener("close", this.onClose, { once: true });
  }

  async call(method, params = {}) {
    if (this.signal?.aborted) throw this.signal.reason;
    if (this.closed || this.ws.readyState !== WebSocket.OPEN) {
      throw new Error(`Cannot call ${method}: CDP WebSocket is not open`);
    }

    const id = this.nextId++;
    const response = new Promise((resolve, reject) => this.pending.set(id, { resolve, reject }));
    this.ws.send(JSON.stringify({ id, method, params }));

    if (!this.signal) return response;
    return Promise.race([
      response,
      new Promise((_, reject) => {
        const onAbort = () => {
          this.signal.removeEventListener("abort", onAbort);
          reject(this.signal.reason ?? new Error("QA runtime aborted"));
        };
        this.signal.addEventListener("abort", onAbort, { once: true });
        response.then(
          () => this.signal.removeEventListener("abort", onAbort),
          () => this.signal.removeEventListener("abort", onAbort),
        );
      }),
    ]);
  }

  async close() {
    if (this.closed) return;
    const closed = new Promise(resolve => this.ws.addEventListener("close", resolve, { once: true }));
    try {
      this.ws.close();
    } catch {}

    await Promise.race([closed, sleep(500)]);
    if (!this.closed) {
      try {
        this.ws.close();
      } catch {}
    }
  }
}

async function connectCdp(webSocketUrl, signal) {
  if (signal?.aborted) throw signal.reason;
  const ws = new WebSocket(webSocketUrl);

  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new QaTimeoutError("Timed out opening CDP WebSocket")), 5000);
    const onAbort = () => reject(signal.reason ?? new Error("QA runtime aborted"));
    const cleanup = () => {
      clearTimeout(timer);
      signal?.removeEventListener("abort", onAbort);
      ws.removeEventListener("open", onOpen);
      ws.removeEventListener("error", onError);
    };
    const onOpen = () => {
      cleanup();
      resolve();
    };
    const onError = event => {
      cleanup();
      reject(event.error ?? new Error("CDP WebSocket connection failed"));
    };

    ws.addEventListener("open", onOpen, { once: true });
    ws.addEventListener("error", onError, { once: true });
    signal?.addEventListener("abort", onAbort, { once: true });
  });

  return new CdpClient(ws, signal);
}

export async function createQaRuntime({
  root = QA_ROOT,
  publishDir = path.join(root, "publish"),
  chromePath = DEFAULT_CHROME,
  width = 700,
  height = 900,
  startupTimeoutMs = 15000,
  signal,
} = {}) {
  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "anime-action-qa-"));
  const profileDir = path.join(tempDir, "chrome-profile");
  fs.mkdirSync(profileDir, { recursive: true });

  let httpServer;
  let chrome;
  let cdp;

  const cleanup = async () => {
    const errors = [];

    try {
      await cdp?.close();
    } catch (error) {
      errors.push(error);
    }

    try {
      await terminateOwnedChrome(chrome, profileDir);
    } catch (error) {
      errors.push(error);
    }

    try {
      await httpServer?.close();
    } catch (error) {
      errors.push(error);
    }

    try {
      fs.rmSync(tempDir, { recursive: true, force: true });
    } catch (error) {
      errors.push(error);
    }

    if (errors.length > 0) {
      throw new AggregateError(errors, "QA runtime cleanup failed");
    }
  };

  try {
    httpServer = await startStaticServer(publishDir);
    const pageUrl = `http://127.0.0.1:${httpServer.port}/`;

    chrome = spawn(chromePath, [
      "--no-sandbox",
      "--enable-unsafe-swiftshader",
      "--use-angle=swiftshader",
      "--disable-background-networking",
      "--disable-default-apps",
      "--no-first-run",
      "--remote-debugging-port=0",
      "--user-data-dir=" + profileDir,
      `--window-size=${width},${height}`,
      pageUrl,
    ], {
      stdio: "ignore",
      detached: true,
    });

    const devToolsFile = path.join(profileDir, "DevToolsActivePort");
    await waitForFile(devToolsFile, startupTimeoutMs, signal, chrome);
    const [portLine] = fs.readFileSync(devToolsFile, "utf8").trim().split(/\r?\n/);
    const debugPort = Number(portLine);
    if (!Number.isInteger(debugPort) || debugPort <= 0) {
      throw new Error(`Invalid DevTools port: ${portLine}`);
    }

    const page = await fetchTargets(debugPort, pageUrl, startupTimeoutMs, signal);
    cdp = await connectCdp(page.webSocketDebuggerUrl, signal);

    return {
      root,
      publishDir,
      pageUrl,
      serverPort: httpServer.port,
      debugPort,
      chromePid: chrome.pid,
      profileDir,
      tempDir,
      logs: cdp.logs,
      call: cdp.call.bind(cdp),
      sleep: ms => sleep(ms, signal),
      signal,
      cleanup,
    };
  } catch (error) {
    try {
      await cleanup();
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], "QA runtime startup and cleanup both failed");
    }
    throw error;
  }
}

export async function withQaRuntime(action, {
  timeoutMs = 120000,
  ...runtimeOptions
} = {}) {
  const controller = new AbortController();
  let receivedSignal = null;
  let runtime = null;
  let timer = null;
  let rejectInterruption = null;

  const onSignal = signalName => {
    if (receivedSignal) return;
    receivedSignal = signalName;
    const error = new Error(`QA interrupted by ${signalName}`);
    controller.abort(error);
    rejectInterruption?.(error);
  };

  const onSigint = () => onSignal("SIGINT");
  const onSigterm = () => onSignal("SIGTERM");
  process.once("SIGINT", onSigint);
  process.once("SIGTERM", onSigterm);

  try {
    runtime = await createQaRuntime({ ...runtimeOptions, signal: controller.signal });

    const interruption = new Promise((_, reject) => {
      rejectInterruption = reject;
      if (receivedSignal) reject(controller.signal.reason);
    });

    const timeout = new Promise((_, reject) => {
      timer = setTimeout(() => {
        const error = new QaTimeoutError(`QA action timed out after ${timeoutMs} ms`);
        controller.abort(error);
        reject(error);
      }, timeoutMs);
    });

    return await Promise.race([action(runtime), timeout, interruption]);
  } catch (error) {
    if (receivedSignal) return undefined;
    throw error;
  } finally {
    if (timer) clearTimeout(timer);
    try {
      await runtime?.cleanup();
    } finally {
      process.off("SIGINT", onSigint);
      process.off("SIGTERM", onSigterm);
      if (receivedSignal) {
        process.exitCode = receivedSignal === "SIGINT" ? 130 : 143;
      }
    }
  }
}
