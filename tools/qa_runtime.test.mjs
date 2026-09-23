import assert from "node:assert/strict";
import fs from "node:fs";
import net from "node:net";
import path from "node:path";
import test from "node:test";
import { execFileSync } from "node:child_process";
import {
  QA_ROOT,
  QaTimeoutError,
  isProcessGroupAlive,
  withQaRuntime,
} from "./qa_runtime.mjs";

function processesContaining(fragment) {
  const output = execFileSync("/bin/ps", ["-axo", "pid=,ppid=,%cpu=,rss=,command="], {
    encoding: "utf8",
  });

  return output
    .split(/\r?\n/)
    .filter(Boolean)
    .filter(line => line.includes(fragment));
}

async function canConnect(port) {
  return await new Promise(resolve => {
    const socket = net.createConnection({ host: "127.0.0.1", port });
    const done = value => {
      socket.destroy();
      resolve(value);
    };
    socket.setTimeout(300);
    socket.once("connect", () => done(true));
    socket.once("error", () => done(false));
    socket.once("timeout", () => done(false));
  });
}

async function assertRuntimeGone(identity) {
  assert.ok(identity, "test must capture runtime identity");
  await new Promise(resolve => setTimeout(resolve, 150));

  assert.equal(
    isProcessGroupAlive(identity.chromePid),
    false,
    `Chrome process group ${identity.chromePid} must be gone`,
  );
  assert.deepEqual(
    processesContaining(identity.profileDir),
    [],
    `no Chrome/helper process may retain profile ${identity.profileDir}`,
  );
  assert.equal(fs.existsSync(identity.tempDir), false, "temporary QA directory must be deleted");
  assert.equal(await canConnect(identity.serverPort), false, "QA HTTP server port must be closed");
}

function captureIdentity(runtime) {
  return {
    chromePid: runtime.chromePid,
    profileDir: runtime.profileDir,
    tempDir: runtime.tempDir,
    serverPort: runtime.serverPort,
  };
}

test("QA runtime cleans Chrome/helpers/server after normal completion", async () => {
  let identity;
  await withQaRuntime(async runtime => {
    identity = captureIdentity(runtime);
    const result = await runtime.call("Runtime.evaluate", {
      expression: "1 + 1",
      returnByValue: true,
    });
    assert.equal(result.result.value, 2);
  }, {
    publishDir: path.join(QA_ROOT, "publish"),
    timeoutMs: 10000,
  });

  await assertRuntimeGone(identity);
});

test("QA runtime cleans Chrome/helpers/server after an intentional error", async () => {
  let identity;
  await assert.rejects(
    withQaRuntime(async runtime => {
      identity = captureIdentity(runtime);
      await runtime.call("Runtime.evaluate", {
        expression: "document.title",
        returnByValue: true,
      });
      throw new Error("intentional QA failure");
    }, {
      publishDir: path.join(QA_ROOT, "publish"),
      timeoutMs: 10000,
    }),
    /intentional QA failure/,
  );

  await assertRuntimeGone(identity);
});

test("QA runtime cleans Chrome/helpers/server after timeout", async () => {
  let identity;
  await assert.rejects(
    withQaRuntime(async runtime => {
      identity = captureIdentity(runtime);
      await runtime.sleep(10000);
    }, {
      publishDir: path.join(QA_ROOT, "publish"),
      timeoutMs: 500,
    }),
    error => error instanceof QaTimeoutError,
  );

  await assertRuntimeGone(identity);
});
