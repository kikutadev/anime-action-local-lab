import fs from "node:fs";
import path from "node:path";
import { QA_ROOT, withQaRuntime } from "./qa_runtime.mjs";
import {
  buildIdleReferenceCaptures,
  buildQaMatrixCaptures,
  createManifestSkeleton,
} from "./qa_pose_matrix.mjs";

const captureRoot = path.join(QA_ROOT, "qa-captures");
const finalOut = path.join(captureRoot, "current");
const workOut = path.join(captureRoot, `.current-${process.pid}-${Date.now()}`);
fs.mkdirSync(workOut, { recursive: true });

function promoteOutput() {
  fs.rmSync(finalOut, { recursive: true, force: true });
  fs.renameSync(workOut, finalOut);
}

function removeWorkOutput() {
  fs.rmSync(workOut, { recursive: true, force: true });
}

function runtimeFrameName(sequence, index) {
  return `runtime_${sequence}_${String(index).padStart(2, "0")}`;
}

try {
  await withQaRuntime(async ({ call, on, logs, sleep }) => {
    await call("Runtime.enable");
    await call("Log.enable");
    await call("Page.enable");
    await call("Emulation.setDeviceMetricsOverride", {
      width: 700,
      height: 900,
      deviceScaleFactor: 1,
      mobile: false,
      screenWidth: 700,
      screenHeight: 900,
    });

    const readyDeadline = Date.now() + 45000;
    while (Date.now() < readyDeadline) {
      const result = await call("Runtime.evaluate", {
        expression: "!!window.unityInstance",
        returnByValue: true,
      });
      const avatarReady =
        logs.some(line => line.includes("Loaded AvatarSample_C.vrm")) &&
        logs.some(line => line.includes("VRoid action motor bound humanoid"));
      if (result.result?.value === true && avatarReady) break;
      await sleep(250);
    }

    if (!logs.some(line => line.includes("VRoid action motor bound humanoid"))) {
      throw new Error(
        "VRoid C did not become ready\n" + logs.slice(-60).join("\n"),
      );
    }

    async function setPose(capture) {
      const before = logs.length;
      const expression =
        "window.unityInstance && window.unityInstance.SendMessage(" +
        JSON.stringify("ModelQualityShowcase") + "," +
        JSON.stringify("SetQaState") + "," +
        JSON.stringify(capture.spec) + ")";

      const expectedPhase = Number(capture.phase).toFixed(3);
      const poseDeadline = Date.now() + 10000;
      while (Date.now() < poseDeadline) {
        await call("Runtime.evaluate", { expression });
        await sleep(110);
        const acknowledged = logs.slice(before).some(line =>
          line.includes("QA_POSE") &&
          line.includes(`motion=${capture.action}`) &&
          line.includes(`phase=${expectedPhase}`) &&
          line.includes(`view=${capture.view}`),
        );
        if (acknowledged) {
          await sleep(120);
          return;
        }
      }

      throw new Error(
        `QA pose did not become ready: ${capture.spec}\n` +
        logs.slice(-40).join("\n"),
      );
    }

    async function screenshot(name) {
      const shot = await call("Page.captureScreenshot", {
        format: "png",
        fromSurface: true,
        captureBeyondViewport: false,
      });
      fs.writeFileSync(
        path.join(workOut, name + ".png"),
        Buffer.from(shot.data, "base64"),
      );
    }

    async function sendShowcase(method, argument = "") {
      const expression =
        "window.unityInstance && window.unityInstance.SendMessage(" +
        JSON.stringify("ModelQualityShowcase") + "," +
        JSON.stringify(method) + "," +
        JSON.stringify(argument) + ")";
      await call("Runtime.evaluate", { expression });
    }

    async function captureRuntimeSequence(
      sequence,
      durationMs,
      startAction,
      stopAction = null,
    ) {
      const names = [];
      const timestamps = [];
      let recording = false;
      let ackError = null;

      const unsubscribe = on("Page.screencastFrame", params => {
        void call("Page.screencastFrameAck", {
          sessionId: params.sessionId,
        }).catch(error => {
          ackError ??= error;
        });

        if (!recording || names.length >= 80) return;

        const name = runtimeFrameName(sequence, names.length);
        fs.writeFileSync(
          path.join(workOut, name + ".png"),
          Buffer.from(params.data, "base64"),
        );
        names.push(name);
        timestamps.push(params.metadata?.timestamp ?? null);
      });

      try {
        await call("Page.startScreencast", {
          format: "png",
          maxWidth: 560,
          maxHeight: 720,
          everyNthFrame: 1,
        });
        await sleep(80);
        await startAction();
        recording = true;
        await sleep(durationMs);
        recording = false;
        if (stopAction) await stopAction();
        await sleep(60);
        await call("Page.stopScreencast");
      } finally {
        recording = false;
        unsubscribe();
        try {
          await call("Page.stopScreencast");
        } catch {}
      }

      if (ackError) throw ackError;
      if (names.length < 4) {
        throw new Error(
          `Runtime screencast produced too few frames for ${sequence}: ${names.length}`,
        );
      }

      const deltas = [];
      for (let index = 1; index < timestamps.length; index++) {
        const before = timestamps[index - 1];
        const after = timestamps[index];
        if (Number.isFinite(before) && Number.isFinite(after) && after > before) {
          deltas.push((after - before) * 1000);
        }
      }
      deltas.sort((a, b) => a - b);
      const intervalMs = deltas.length > 0
        ? Math.round(deltas[Math.floor(deltas.length / 2)])
        : Math.max(20, Math.round(durationMs / names.length));

      return {
        frames: names,
        intervalMs,
        captureMode: "cdp-screencast",
      };
    }

    const manifest = createManifestSkeleton();
    const staticCaptures = [
      ...buildIdleReferenceCaptures(),
      ...buildQaMatrixCaptures(),
    ];

    for (const capture of staticCaptures) {
      await setPose(capture);
      await screenshot(capture.name);
      manifest.staticCaptures.push({
        ...capture,
        file: capture.name + ".png",
      });
      console.log(
        "captured",
        capture.action,
        capture.phaseId,
        capture.view,
      );
    }

    await sendShowcase("ExitQaState");
    await sleep(300);
    await screenshot("runtime_idle");

    manifest.runtimeSequences.run = await captureRuntimeSequence(
      "run",
      1100,
      async () => {
        await call("Input.dispatchKeyEvent", {
          type: "keyDown",
          code: "KeyW",
          key: "w",
          windowsVirtualKeyCode: 87,
          nativeVirtualKeyCode: 87,
        });
      },
      async () => {
        await call("Input.dispatchKeyEvent", {
          type: "keyUp",
          code: "KeyW",
          key: "w",
          windowsVirtualKeyCode: 87,
          nativeVirtualKeyCode: 87,
        });
      },
    );
    await sleep(240);
    await screenshot("runtime_stop");

    manifest.runtimeSequences.slash = await captureRuntimeSequence(
      "slash",
      980,
      async () => {
        await sendShowcase("TriggerQaRuntimeAttack");
      },
    );
    await sleep(180);

    manifest.runtimeSequences.dodge = await captureRuntimeSequence(
      "dodge",
      820,
      async () => {
        await sendShowcase("TriggerQaRuntimeDodge");
      },
    );
    await sleep(160);

    const fatal = logs.filter(line =>
      /EXCEPTION|NullReferenceException|MissingReferenceException/.test(line),
    );
    if (fatal.length > 0) {
      throw new Error(
        "QA runtime logged fatal errors:\n" + fatal.slice(-20).join("\n"),
      );
    }

    manifest.runtimeStillFiles = {
      idle: "runtime_idle.png",
      stop: "runtime_stop.png",
    };
    manifest.capturedAt = new Date().toISOString();

    fs.writeFileSync(path.join(workOut, "qa.log"), logs.join("\n"));
    fs.writeFileSync(
      path.join(workOut, "manifest.json"),
      JSON.stringify(manifest, null, 2),
    );
    console.log("QA_CAPTURE_DONE", workOut);
  }, {
    timeoutMs: 180000,
  });

  promoteOutput();
  console.log("QA_CAPTURE_PROMOTED", finalOut);
} catch (error) {
  removeWorkOutput();
  throw error;
}
