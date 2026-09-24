import fs from "node:fs";
import path from "node:path";
import { QA_ROOT, withQaRuntime } from "./qa_runtime.mjs";

const OUT = path.join(QA_ROOT, "qa-captures", "run-upper-candidates");
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

const candidates = [
  { id: "w000", weight: 0.00 },
  { id: "w025", weight: 0.25 },
  { id: "w050", weight: 0.50 },
  { id: "w075", weight: 0.75 },
  { id: "w100", weight: 1.00 },
];

const phases = [
  { id: "contact_a", phase: 0.000 },
  { id: "passing", phase: 0.250 },
  { id: "up", phase: 0.375 },
];

const views = [
  "front_threequarter_l",
  "side_l",
  "back_threequarter_l",
  "back",
  "back_threequarter_r",
  "side_r",
  "front_threequarter_r",
];

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
    const ready = await call("Runtime.evaluate", {
      expression: "!!window.unityInstance",
      returnByValue: true,
    });
    if (
      ready.result?.value === true &&
      logs.some(line => line.includes("VRoid action motor bound humanoid"))
    ) {
      break;
    }
    await sleep(200);
  }

  if (!logs.some(line => line.includes("VRoid action motor bound humanoid"))) {
    throw new Error("VRoid humanoid did not become ready");
  }

  async function setPose(spec, expectedPhase, expectedView) {
    const before = logs.length;
    const expression =
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("SetQaState") + "," +
      JSON.stringify(spec) + ")";

    const deadline = Date.now() + 8000;
    while (Date.now() < deadline) {
      await call("Runtime.evaluate", { expression });
      await sleep(95);
      if (
        logs.slice(before).some(line =>
          line.includes("QA_POSE") &&
          line.includes("motion=run") &&
          line.includes("phase=" + expectedPhase.toFixed(3)) &&
          line.includes("view=" + expectedView))
      ) {
        await sleep(90);
        return;
      }
    }
    throw new Error("QA run pose was not acknowledged: " + spec);
  }

  async function screenshot(name) {
    const result = await call("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false,
    });
    fs.writeFileSync(
      path.join(OUT, name + ".png"),
      Buffer.from(result.data, "base64"),
    );
  }

  for (const phase of phases) {
    for (const candidate of candidates) {
      for (const view of views) {
        const spec = [
          "run",
          phase.phase.toFixed(3),
          view,
          candidate.weight.toFixed(2),
          "0",
          "0",
          "-35",
        ].join("|");
        await setPose(spec, phase.phase, view);
        const name = [phase.id, candidate.id, view].join("__");
        await screenshot(name);
        console.log("captured", name);
      }
    }
  }

  fs.writeFileSync(
    path.join(OUT, "manifest.json"),
    JSON.stringify({ candidates, phases, views }, null, 2),
  );
  fs.writeFileSync(path.join(OUT, "qa.log"), logs.join("\n"));
}, { timeoutMs: 120000 });

console.log("DONE", OUT);
