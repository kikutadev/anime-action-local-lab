import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync, spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
const STATIC_SERVER_SCRIPT = path.join(TOOL_DIR, "qa_static_server.mjs");
const PROCESS_GUARD_SCRIPT = path.join(TOOL_DIR, "qa_process_guard.mjs");

export const QA_ROOT = path.resolve(TOOL_DIR, "..");

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
  if (!profileDir) return [];

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
      .filter(processInfo =>
        processInfo.pid !== process.pid &&
        processInfo.command.includes(profileDir));
  } catch {
    return [];
  }
}

function signalPid(pid, signal) {
  if (!isAlive(pid)) return;

  try {
    process.kill(pid, signal);
  } catch (error) {
    if (error?.code !== "ESRCH") throw error;
  }
}

function signalGroup(groupId, signal) {
  if (!isProcessGroupAlive(groupId)) return;

  try {
    process.kill(-groupId, signal);
  } catch (error) {
    if (error?.code !== "ESRCH") throw error;
  }
}

async function terminateOwnedChrome(chromePid, profileDir, graceMs = 1500) {
  if (!chromePid && findProfileProcesses(profileDir).length === 0) return;

  // 1) Root gets SIGTERM first, which gives Chrome a chance to tear down helpers itself.
  signalPid(chromePid, "SIGTERM");

  // 2) Wait for both the dedicated process group and profile-bound helpers.
  await waitUntil(
    () => !isProcessGroupAlive(chromePid) && findProfileProcesses(profileDir).length === 0,
    graceMs,
  );

  // 3) Profile matching is deliberately narrow: only this unique QA user-data-dir.
  for (const processInfo of findProfileProcesses(profileDir)) {
    signalPid(processInfo.pid, "SIGKILL");
  }

  // 4) The Chrome root owns a dedicated process group. Kill any remaining descendants.
  signalGroup(chromePid, "SIGKILL");

  await waitUntil(
    () => !isProcessGroupAlive(chromePid) && findProfileProcesses(profileDir).length === 0,
    1200,
  );
}

async function terminateOwnedProcess(rootPid, termGraceMs = 1000, killGraceMs = 800) {
  if (!rootPid) return;

  signalPid(rootPid, "SIGTERM");
  await waitUntil(() => !isProcessGroupAlive(rootPid), termGraceMs);

  signalGroup(rootPid, "SIGKILL");
  await waitUntil(() => !isProcessGroupAlive(rootPid), killGraceMs);
}

function writeStateFile(stateFile, state) {
  const temporary = stateFile + ".tmp";
  fs.writeFileSync(temporary, JSON.stringify(state), "utf8");
  fs.renameSync(temporary, stateFile);
}

async function waitForFile(file, timeoutMs, signal, processPid, label) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (signal?.aborted) throw signal.reason;
    if (processPid && !isAlive(processPid)) {
      throw new Error(`${label} exited before becoming ready`);
    }
    if (fs.existsSync(file) && fs.statSync(file).size > 0) return;
    await sleep(50, signal);
  }
  throw new QaTimeoutError(`Timed out waiting for ${label}: ${file}`);
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
    this.listeners = new Map();
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

      const listeners = this.listeners.get(message.method);
      if (listeners) {
        for (const listener of [...listeners]) {
          try {
            listener(message.params);
          } catch (error) {
            this.logs.push(
              "QA_EVENT_LISTENER_ERROR " +
              (error?.stack ?? error?.message ?? String(error)),
            );
          }
        }
      }
    };

    this.onClose = () => {
      this.closed = true;
      const error = new Error("CDP WebSocket closed");
      for (const handler of this.pending.values()) handler.reject(error);
      this.pending.clear();
      this.listeners.clear();
    };

    ws.addEventListener("message", this.onMessage);
    ws.addEventListener("close", this.onClose, { once: true });
  }

  on(method, listener) {
    if (!this.listeners.has(method)) {
      this.listeners.set(method, new Set());
    }
    const listeners = this.listeners.get(method);
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
      if (listeners.size === 0) this.listeners.delete(method);
    };
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
    const timer = setTimeout(
      () => reject(new QaTimeoutError("Timed out opening CDP WebSocket")),
      5000,
    );
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

function spawnDetached(command, args) {
  const child = spawn(command, args, {
    stdio: "ignore",
    detached: true,
  });
  child.unref();
  return child;
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
  const portFile = path.join(tempDir, "http-port");
  const stateFile = path.join(tempDir, "owned-processes.json");
  const completeMarker = path.join(tempDir, "cleanup-complete");
  fs.mkdirSync(profileDir, { recursive: true });

  const owned = {
    tempDir,
    profileDir,
    serverPid: null,
    chromePid: null,
  };
  writeStateFile(stateFile, owned);

  let serverPort = null;
  let serverPid = null;
  let chromePid = null;
  let guardPid = null;
  let cdp = null;
  let cleanupPromise = null;

  // Start the watchdog before any detachable child. If this runner is SIGKILLed,
  // the guard notices the missing parent and cleans whichever PIDs have been recorded.
  const guard = spawnDetached(process.execPath, [
    PROCESS_GUARD_SCRIPT,
    String(process.pid),
    stateFile,
    completeMarker,
  ]);
  guardPid = guard.pid;

  const cleanup = () => {
    if (cleanupPromise) return cleanupPromise;

    cleanupPromise = (async () => {
      const primaryErrors = [];

      try {
        await cdp?.close();
      } catch (error) {
        primaryErrors.push(error);
      }

      try {
        await terminateOwnedChrome(chromePid, profileDir);
      } catch (error) {
        primaryErrors.push(error);
      }

      try {
        await terminateOwnedProcess(serverPid);
      } catch (error) {
        primaryErrors.push(error);
      }

      let chromeResidual = findProfileProcesses(profileDir);
      let chromeGroupAlive = isProcessGroupAlive(chromePid);
      let serverGroupAlive = isProcessGroupAlive(serverPid);

      if (primaryErrors.length === 0 &&
          chromeResidual.length === 0 &&
          !chromeGroupAlive &&
          !serverGroupAlive) {
        try {
          fs.writeFileSync(completeMarker, "ok\n", "utf8");
        } catch (error) {
          primaryErrors.push(error);
        }
      }

      // If the primary cleanup encountered any issue, do not create the completion
      // marker. SIGTERM makes the guard perform one final narrow cleanup pass.
      try {
        await terminateOwnedProcess(guardPid, primaryErrors.length === 0 ? 500 : 2500, 1000);
      } catch (error) {
        primaryErrors.push(error);
      }

      chromeResidual = findProfileProcesses(profileDir);
      chromeGroupAlive = isProcessGroupAlive(chromePid);
      serverGroupAlive = isProcessGroupAlive(serverPid);
      const guardGroupAlive = isProcessGroupAlive(guardPid);

      const finalErrors = [];
      if (chromeGroupAlive || chromeResidual.length > 0) {
        finalErrors.push(new Error(
          `QA Chrome cleanup incomplete: groupAlive=${chromeGroupAlive} profilePids=${chromeResidual.map(x => x.pid).join(",")}`,
        ));
      }
      if (serverGroupAlive) {
        finalErrors.push(new Error(`QA HTTP server cleanup incomplete: pid=${serverPid}`));
      }
      if (guardGroupAlive) {
        finalErrors.push(new Error(`QA cleanup guardian did not exit: pid=${guardPid}`));
      }

      try {
        fs.rmSync(tempDir, { recursive: true, force: true });
      } catch (error) {
        finalErrors.push(error);
      }

      if (finalErrors.length > 0) {
        throw new AggregateError([...primaryErrors, ...finalErrors], "QA runtime cleanup failed");
      }
    })();

    return cleanupPromise;
  };

  try {
    const server = spawnDetached(process.execPath, [
      STATIC_SERVER_SCRIPT,
      publishDir,
      portFile,
    ]);
    serverPid = server.pid;
    owned.serverPid = serverPid;
    writeStateFile(stateFile, owned);

    await waitForFile(
      portFile,
      startupTimeoutMs,
      signal,
      serverPid,
      "QA HTTP server port file",
    );
    serverPort = Number(fs.readFileSync(portFile, "utf8").trim());
    if (!Number.isInteger(serverPort) || serverPort <= 0) {
      throw new Error(`Invalid QA HTTP server port: ${serverPort}`);
    }

    const pageUrl = `http://127.0.0.1:${serverPort}/`;

    const chrome = spawnDetached(chromePath, [
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
    ]);
    chromePid = chrome.pid;
    owned.chromePid = chromePid;
    writeStateFile(stateFile, owned);

    const devToolsFile = path.join(profileDir, "DevToolsActivePort");
    await waitForFile(
      devToolsFile,
      startupTimeoutMs,
      signal,
      chromePid,
      "Chrome DevTools port file",
    );

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
      serverPort,
      serverPid,
      debugPort,
      chromePid,
      guardPid,
      profileDir,
      tempDir,
      logs: cdp.logs,
      call: cdp.call.bind(cdp),
      on: cdp.on.bind(cdp),
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
