import fs from "node:fs";
import { execFileSync } from "node:child_process";

const [parentPidArg, stateFileArg, completeMarkerArg] = process.argv.slice(2);
const parentPid = Number(parentPidArg);
const stateFile = stateFileArg;
const completeMarker = completeMarkerArg;

if (!Number.isInteger(parentPid) || parentPid <= 0 || !stateFile || !completeMarker) {
  throw new Error("Invalid QA guard arguments");
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

function isGroupAlive(groupId) {
  if (!Number.isInteger(groupId) || groupId <= 0) return false;
  try {
    process.kill(-groupId, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

function readState() {
  try {
    return JSON.parse(fs.readFileSync(stateFile, "utf8"));
  } catch {
    return {};
  }
}

function profileProcesses(profileDir) {
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
      .filter(info => info.pid !== process.pid && info.pid !== parentPid && info.command.includes(profileDir));
  } catch {
    return [];
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function waitUntil(predicate, timeoutMs, intervalMs = 50) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return true;
    await sleep(intervalMs);
  }
  return predicate();
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
  if (!isGroupAlive(groupId)) return;
  try {
    process.kill(-groupId, signal);
  } catch (error) {
    if (error?.code !== "ESRCH") throw error;
  }
}

async function terminateChrome(chromePid, profileDir) {
  if (!chromePid && profileProcesses(profileDir).length === 0) return;

  signalPid(chromePid, "SIGTERM");
  await waitUntil(
    () => !isGroupAlive(chromePid) && profileProcesses(profileDir).length === 0,
    1500,
  );

  for (const info of profileProcesses(profileDir)) {
    signalPid(info.pid, "SIGKILL");
  }
  signalGroup(chromePid, "SIGKILL");

  await waitUntil(
    () => !isGroupAlive(chromePid) && profileProcesses(profileDir).length === 0,
    1200,
  );
}

async function terminateServer(serverPid) {
  if (!serverPid) return;

  signalPid(serverPid, "SIGTERM");
  await waitUntil(() => !isGroupAlive(serverPid), 1000);
  signalGroup(serverPid, "SIGKILL");
  await waitUntil(() => !isGroupAlive(serverPid), 800);
}

let cleaning = false;
async function cleanup() {
  if (cleaning) return;
  cleaning = true;

  const state = readState();

  try {
    await terminateChrome(Number(state.chromePid), state.profileDir);
  } catch {}

  try {
    await terminateServer(Number(state.serverPid));
  } catch {}

  try {
    if (state.tempDir) fs.rmSync(state.tempDir, { recursive: true, force: true });
  } catch {}
}

async function exitFromSignal() {
  if (!fs.existsSync(completeMarker)) {
    await cleanup();
  }
  process.exit(0);
}

process.once("SIGTERM", () => void exitFromSignal());
process.once("SIGINT", () => void exitFromSignal());

while (true) {
  if (!isAlive(parentPid)) {
    if (!fs.existsSync(completeMarker)) {
      await cleanup();
    }
    process.exit(0);
  }
  await sleep(100);
}
