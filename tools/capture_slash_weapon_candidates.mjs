import fs from "node:fs";
import path from "node:path";
import { QA_ROOT, withQaRuntime } from "./qa_runtime.mjs";

const phase = Number(process.env.QA_PHASE || "0.42");
const phaseToken = String(Math.round(phase * 1000)).padStart(3, "0");
const OUT = path.join(
  QA_ROOT,
  "qa-captures",
  "slash-weapon-candidates-" + phaseToken,
);
fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

const candidates = [
  { id: "y090", euler: [0, 90, 0] },
  { id: "zero", euler: [0, 0, 0] },
  { id: "yn90", euler: [0, -90, 0] },
  { id: "zn90", euler: [0, 0, -90] },
  { id: "z090", euler: [0, 0, 90] },
  { id: "x090", euler: [90, 0, 0] },
  { id: "xn90", euler: [-90, 0, 0] },
];

const views = [
  "front",
  "front_threequarter_l",
  "side_l",
  "back_threequarter_l",
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

  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
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

  async function setPose(spec) {
    const before = logs.length;
    const expression =
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("SetQaState") + "," +
      JSON.stringify(spec) + ")";

    const limit = Date.now() + 8000;
    while (Date.now() < limit) {
      await call("Runtime.evaluate", { expression });
      await sleep(100);
      if (
        logs.slice(before).some(line =>
          line.includes("QA_POSE") &&
          line.includes("motion=slash") &&
          line.includes("phase=" + phase.toFixed(3)))
      ) {
        await sleep(100);
        return;
      }
    }
    throw new Error("QA slash pose was not acknowledged: " + spec);
  }

  async function shot(name) {
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

  for (const candidate of candidates) {
    for (const view of views) {
      const [x, y, z] = candidate.euler;
      const spec = [
        "slash",
        phase.toFixed(3),
        view,
        "0",
        x,
        y,
        z,
      ].join("|");
      await setPose(spec);
      const name = candidate.id + "__" + view;
      await shot(name);
      console.log("captured", name);
    }
  }

  fs.writeFileSync(
    path.join(OUT, "manifest.json"),
    JSON.stringify({ phase, candidates, views }, null, 2),
  );
  fs.writeFileSync(path.join(OUT, "qa.log"), logs.join("\n"));
}, { timeoutMs: 90000 });

console.log("DONE", OUT);
