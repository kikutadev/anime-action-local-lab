import fs from "node:fs";
import path from "node:path";
import { QA_ROOT, withQaRuntime } from "./qa_runtime.mjs";
import {
  buildQaMatrixCaptures,
  createManifestSkeleton,
} from "./qa_pose_matrix.mjs";

const actionId = process.env.QA_ACTION || "slash";
const OUT = path.join(QA_ROOT, "qa-captures", "action-" + actionId);
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

await withQaRuntime(async ({ call, logs, sleep }) => {
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
    const ready =
      result.result?.value === true &&
      logs.some(line => line.includes("VRoid action motor bound humanoid"));
    if (ready) break;
    await sleep(200);
  }

  if (!logs.some(line => line.includes("VRoid action motor bound humanoid"))) {
    throw new Error("VRoid humanoid did not become ready");
  }

  async function setPose(capture) {
    const before = logs.length;
    const expression =
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("SetQaState") + "," +
      JSON.stringify(capture.spec) + ")";

    const expectedPhase = Number(capture.phase).toFixed(3);
    const deadline = Date.now() + 8000;
    while (Date.now() < deadline) {
      await call("Runtime.evaluate", { expression });
      await sleep(90);
      if (
        logs.slice(before).some(line =>
          line.includes("QA_POSE") &&
          line.includes("motion=" + capture.action) &&
          line.includes("phase=" + expectedPhase) &&
          line.includes("view=" + capture.view))
      ) {
        await sleep(80);
        return;
      }
    }

    throw new Error("QA pose was not acknowledged: " + capture.spec);
  }

  async function screenshot(name) {
    const shot = await call("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false,
    });
    fs.writeFileSync(
      path.join(OUT, name + ".png"),
      Buffer.from(shot.data, "base64"),
    );
  }

  const captures = buildQaMatrixCaptures().filter(
    capture => capture.action === actionId,
  );
  if (captures.length === 0) {
    throw new Error("Unknown or empty QA action: " + actionId);
  }

  const manifest = createManifestSkeleton();
  manifest.actions = {
    [actionId]: manifest.actions[actionId],
  };
  manifest.staticCaptures = [];

  for (const capture of captures) {
    await setPose(capture);
    await screenshot(capture.name);
    manifest.staticCaptures.push({
      ...capture,
      file: capture.name + ".png",
    });
    console.log("captured", capture.phaseId, capture.view);
  }

  manifest.capturedAt = new Date().toISOString();
  fs.writeFileSync(
    path.join(OUT, "manifest.json"),
    JSON.stringify(manifest, null, 2),
  );
  fs.writeFileSync(path.join(OUT, "qa.log"), logs.join("\n"));
}, { timeoutMs: 90000 });

console.log("DONE", OUT);
