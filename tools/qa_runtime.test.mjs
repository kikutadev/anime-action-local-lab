import assert from "node:assert/strict";
import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { execFileSync, spawn } from "node:child_process";
import {
  QA_ROOT,
  QaTimeoutError,
  createQaRuntime,
  isProcessGroupAlive,
  withQaRuntime,
} from "./qa_runtime.mjs";

const FIXTURE_SCRIPT = path.join(QA_ROOT, "tools", "qa_runtime_fixture.mjs");

function makeSimplePublish() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "anime-action-qa-publish-"));
  fs.writeFileSync(
    path.join(dir, "index.html"),
    "<!doctype html><html><head><title>QA fixture</title></head><body>ok</body></html>",
    "utf8",
  );
  return dir;
}

function processesContaining(fragment) {
  const output = execFileSync("/bin/ps", ["-axo", "pid=,ppid=,pgid=,%cpu=,rss=,command="], {
    encoding: "utf8",
  });

  return output
    .split(/\r?\n/)
    .filter(Boolean)
    .filter(line => line.includes(fragment));
}

function pgrepContaining(fragment) {
  try {
    return execFileSync("/usr/bin/pgrep", ["-fl", fragment], { encoding: "utf8" })
      .split(/\r?\n/)
      .filter(Boolean)
      .filter(line => !line.includes("qa_runtime.test.mjs"));
  } catch (error) {
    if (error?.status === 1) return [];
    throw error;
  }
}

function listRuntimeTempDirs() {
  return fs.readdirSync(os.tmpdir())
    .filter(name => name.startsWith("anime-action-qa-") && !name.startsWith("anime-action-qa-publish-"))
    .sort();
}

async function canConnect(port) {
  if (!Number.isInteger(port) || port <= 0) return false;

  return await new Promise(resolve => {
    const socket = net.createConnection({ host: "127.0.0.1", port });
    let settled = false;
    const done = value => {
      if (settled) return;
      settled = true;
      socket.destroy();
      resolve(value);
    };
    socket.setTimeout(300);
    socket.once("connect", () => done(true));
    socket.once("error", () => done(false));
    socket.once("timeout", () => done(false));
  });
}

async function waitUntil(predicate, timeoutMs = 5000, intervalMs = 75) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) return true;
    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }
  return await predicate();
}

function captureIdentity(runtime) {
  return {
    chromePid: runtime.chromePid,
    serverPid: runtime.serverPid,
    guardPid: runtime.guardPid,
    profileDir: runtime.profileDir,
    tempDir: runtime.tempDir,
    serverPort: runtime.serverPort,
    debugPort: runtime.debugPort,
  };
}

async function assertRuntimeGone(identity) {
  assert.ok(identity, "test must capture runtime identity");

  const gone = await waitUntil(async () =>
    !isProcessGroupAlive(identity.chromePid) &&
    !isProcessGroupAlive(identity.serverPid) &&
    !isProcessGroupAlive(identity.guardPid) &&
    processesContaining(identity.profileDir).length === 0 &&
    !(await canConnect(identity.serverPort)) &&
    !(await canConnect(identity.debugPort)) &&
    !fs.existsSync(identity.tempDir)
  );

  assert.equal(gone, true, "QA runtime resources should disappear within cleanup deadline");
  assert.equal(isProcessGroupAlive(identity.chromePid), false, "Chrome process group must be gone");
  assert.equal(isProcessGroupAlive(identity.serverPid), false, "HTTP server process group must be gone");
  assert.equal(isProcessGroupAlive(identity.guardPid), false, "cleanup guardian process group must be gone");
  assert.deepEqual(processesContaining(identity.profileDir), [], "no profile-bound Chrome/helper process may remain");
  assert.deepEqual(pgrepContaining(identity.profileDir), [], "pgrep must find no profile-bound process");
  assert.equal(fs.existsSync(identity.tempDir), false, "temporary QA directory must be deleted");
  assert.equal(await canConnect(identity.serverPort), false, "QA HTTP server port must be closed");
  assert.equal(await canConnect(identity.debugPort), false, "QA CDP port must be closed");
}

async function waitForIdentity(file, child, timeoutMs = 10000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (fs.existsSync(file) && fs.statSync(file).size > 0) {
      return JSON.parse(fs.readFileSync(file, "utf8"));
    }
    if (child.exitCode !== null) {
      throw new Error("fixture exited before identity was written: " + child.exitCode);
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  throw new Error("timed out waiting for fixture identity");
}

async function waitForExit(child, timeoutMs = 10000) {
  if (child.exitCode !== null || child.signalCode !== null) {
    return { code: child.exitCode, signal: child.signalCode };
  }

  return await new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      child.off("exit", onExit);
      reject(new Error("child did not exit"));
    }, timeoutMs);

    const onExit = (code, signal) => {
      clearTimeout(timer);
      resolve({ code, signal });
    };

    child.once("exit", onExit);
  });
}

function spawnFixture(mode, identityFile, publishDir) {
  return spawn(process.execPath, [FIXTURE_SCRIPT, mode, identityFile, publishDir], {
    stdio: "ignore",
  });
}

async function runFixtureFailureCase(mode, expectedMessage) {
  const publishDir = makeSimplePublish();
  const identityFile = path.join(
    os.tmpdir(),
    "anime-action-" + mode + "-" + process.pid + "-" + Date.now() + ".json",
  );
  const child = spawnFixture(mode, identityFile, publishDir);

  try {
    const identity = await waitForIdentity(identityFile, child);
    const result = await waitForExit(child);
    assert.notEqual(result.code, 0, expectedMessage);
    await assertRuntimeGone(identity);
  } finally {
    try { process.kill(child.pid, "SIGKILL"); } catch {}
    fs.rmSync(identityFile, { force: true });
    fs.rmSync(publishDir, { recursive: true, force: true });
  }
}

async function runSignalCase(signalName) {
  const publishDir = makeSimplePublish();
  const identityFile = path.join(
    os.tmpdir(),
    "anime-action-signal-" + signalName.toLowerCase() + "-" + process.pid + "-" + Date.now() + ".json",
  );
  const child = spawnFixture("wait", identityFile, publishDir);

  try {
    const identity = await waitForIdentity(identityFile, child);
    process.kill(child.pid, signalName);
    const result = await waitForExit(child);
    assert.equal(
      result.code,
      signalName === "SIGINT" ? 130 : 143,
      signalName + " should preserve conventional exit code",
    );
    await assertRuntimeGone(identity);
  } finally {
    try { process.kill(child.pid, "SIGKILL"); } catch {}
    fs.rmSync(identityFile, { force: true });
    fs.rmSync(publishDir, { recursive: true, force: true });
  }
}

async function runParentKillCase() {
  const publishDir = makeSimplePublish();
  const identityFile = path.join(
    os.tmpdir(),
    "anime-action-parent-kill-" + process.pid + "-" + Date.now() + ".json",
  );
  const child = spawnFixture("wait", identityFile, publishDir);

  try {
    const identity = await waitForIdentity(identityFile, child);
    process.kill(child.pid, "SIGKILL");
    const result = await waitForExit(child);
    assert.equal(result.signal, "SIGKILL");
    await assertRuntimeGone(identity);
  } finally {
    try { process.kill(child.pid, "SIGKILL"); } catch {}
    fs.rmSync(identityFile, { force: true });
    fs.rmSync(publishDir, { recursive: true, force: true });
  }
}

function scanLifecyclePolicy() {
  const scanRoots = [
    path.join(QA_ROOT, "tools"),
    path.join(QA_ROOT, "docs"),
  ];
  const files = [path.join(QA_ROOT, "README.md")];

  for (const root of scanRoots) {
    if (!fs.existsSync(root)) continue;
    const stack = [root];
    while (stack.length > 0) {
      const currentDir = stack.pop();
      for (const entry of fs.readdirSync(currentDir, { withFileTypes: true })) {
        const full = path.join(currentDir, entry.name);
        if (entry.isDirectory()) {
          stack.push(full);
          continue;
        }
        if (!/\.(?:mjs|js|sh|bash|zsh|md)$/.test(entry.name)) continue;
        if (entry.name === "qa_runtime.test.mjs") continue;
        files.push(full);
      }
    }
  }

  const packageFile = path.join(QA_ROOT, "package.json");
  if (fs.existsSync(packageFile)) files.push(packageFile);

  const forbidden = [
    ["924", "2"].join(""),
    ["418", "3"].join(""),
    ["qa-cdp", "-"].join(""),
    ["qa-idle", "-front"].join(""),
    ["anime-vroid", "-cdp"].join(""),
  ];

  const violations = [];
  for (const file of files) {
    const source = fs.readFileSync(file, "utf8");
    for (const token of forbidden) {
      if (source.includes(token)) {
        violations.push(path.relative(QA_ROOT, file) + " contains forbidden legacy QA token " + token);
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    "QA must own dynamic Chrome/CDP/HTTP lifecycle; fixed external endpoints are forbidden",
  );
}

const cases = [
  {
    name: "normal completion",
    run: async () => {
      const publishDir = makeSimplePublish();
      let identity;
      try {
        await withQaRuntime(async runtime => {
          identity = captureIdentity(runtime);
          const result = await runtime.call("Runtime.evaluate", {
            expression: "1 + 1",
            returnByValue: true,
          });
          assert.equal(result.result.value, 2);
        }, { publishDir, timeoutMs: 10000 });
        await assertRuntimeGone(identity);
      } finally {
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "idempotent cleanup",
    run: async () => {
      const publishDir = makeSimplePublish();
      let runtime;
      try {
        runtime = await createQaRuntime({ publishDir });
        const identity = captureIdentity(runtime);
        await runtime.cleanup();
        await runtime.cleanup();
        await assertRuntimeGone(identity);
      } finally {
        try { await runtime?.cleanup(); } catch {}
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "intentional throw",
    run: async () => {
      const publishDir = makeSimplePublish();
      let identity;
      try {
        await assert.rejects(
          withQaRuntime(async runtime => {
            identity = captureIdentity(runtime);
            throw new Error("intentional QA failure");
          }, { publishDir, timeoutMs: 10000 }),
          /intentional QA failure/,
        );
        await assertRuntimeGone(identity);
      } finally {
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "timeout",
    run: async () => {
      const publishDir = makeSimplePublish();
      let identity;
      try {
        await assert.rejects(
          withQaRuntime(async runtime => {
            identity = captureIdentity(runtime);
            await runtime.sleep(10000);
          }, { publishDir, timeoutMs: 350 }),
          error => error instanceof QaTimeoutError,
        );
        await assertRuntimeGone(identity);
      } finally {
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "Unity load failure",
    run: async () => runFixtureFailureCase("unity-failure", "Unity failure fixture should fail"),
  },
  {
    name: "screenshot failure",
    run: async () => runFixtureFailureCase("screenshot-failure", "screenshot fixture should fail"),
  },
  {
    name: "SIGTERM interruption",
    run: async () => runSignalCase("SIGTERM"),
  },
  {
    name: "SIGINT interruption",
    run: async () => runSignalCase("SIGINT"),
  },
  {
    name: "parent Node SIGKILL guardian recovery",
    run: runParentKillCase,
  },
  {
    name: "startup CDP failure",
    run: async () => {
      const publishDir = makeSimplePublish();
      const fakeRoot = fs.mkdtempSync(path.join(os.tmpdir(), "anime-action-fake-chrome-"));
      const fakeChrome = path.join(fakeRoot, "fake-chrome.sh");
      const beforeTemps = new Set(listRuntimeTempDirs());

      fs.writeFileSync(fakeChrome, `#!/bin/sh
profile=""
for arg in "$@"; do
  case "$arg" in
    --user-data-dir=*) profile="\${arg#--user-data-dir=}" ;;
  esac
done
mkdir -p "$profile"
printf '65534\\n/devtools/browser/fake\\n' > "$profile/DevToolsActivePort"
sleep 30
`, "utf8");
      fs.chmodSync(fakeChrome, 0o755);

      try {
        await assert.rejects(
          createQaRuntime({
            publishDir,
            chromePath: fakeChrome,
            startupTimeoutMs: 450,
          }),
          /Timed out waiting for QA page target|startup and cleanup both failed/,
        );

        const clean = await waitUntil(() => {
          const afterTemps = listRuntimeTempDirs().filter(name => !beforeTemps.has(name));
          return afterTemps.length === 0 &&
            processesContaining("qa_static_server.mjs").filter(line => line.includes(publishDir)).length === 0;
        }, 5000);
        assert.equal(clean, true);
      } finally {
        fs.rmSync(fakeRoot, { recursive: true, force: true });
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "five consecutive QA runtimes",
    run: async () => {
      const publishDir = makeSimplePublish();
      try {
        for (let index = 0; index < 5; index++) {
          let identity;
          const started = performance.now();
          await withQaRuntime(async runtime => {
            identity = captureIdentity(runtime);
            await runtime.call("Runtime.enable");
            const loaded = await waitUntil(async () => {
              const result = await runtime.call("Runtime.evaluate", {
                expression: "document.readyState === 'complete' && document.title === 'QA fixture'",
                returnByValue: true,
              });
              return result.result.value === true;
            }, 3000, 50);
            assert.equal(loaded, true, "QA fixture page must finish loading");
          }, { publishDir, timeoutMs: 10000 });
          await assertRuntimeGone(identity);
          console.log(
            "  RUN",
            index + 1,
            "PASS",
            Math.round(performance.now() - started) + "ms",
          );
        }
      } finally {
        fs.rmSync(publishDir, { recursive: true, force: true });
      }
    },
  },
  {
    name: "repository lifecycle policy",
    run: async () => scanLifecyclePolicy(),
  },
];

let failures = 0;
const suiteStarted = performance.now();

for (const entry of cases) {
  const started = performance.now();
  try {
    await entry.run();
    console.log("PASS", entry.name, Math.round(performance.now() - started) + "ms");
  } catch (error) {
    failures++;
    console.error("FAIL", entry.name, Math.round(performance.now() - started) + "ms");
    console.error(error?.stack ?? error);
  }
}

const suiteMs = Math.round(performance.now() - suiteStarted);
console.log("SUMMARY", "passed=" + (cases.length - failures), "failed=" + failures, "total=" + cases.length, "duration=" + suiteMs + "ms");

process.exit(failures === 0 ? 0 : 1);
