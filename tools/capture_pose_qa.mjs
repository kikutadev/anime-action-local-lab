import fs from "fs";
import path from "path";
import { QA_ROOT, withQaRuntime } from "./qa_runtime.mjs";

const OUT = path.join(QA_ROOT, "qa-captures", "current");
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

  const readyDeadline = Date.now() + 35000;
  let unityReady = false;
  while (Date.now() < readyDeadline) {
    const result = await call("Runtime.evaluate", {
      expression: "!!window.unityInstance",
      returnByValue: true,
    });
    if (result.result?.value === true) {
      unityReady = true;
      break;
    }
    await sleep(250);
  }
  if (!unityReady) throw new Error("Unity instance did not become ready");

  async function setPose(spec) {
    const before = logs.length;
    const motion = spec.split("|")[0];
    const expression =
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("SetQaState") + "," +
      JSON.stringify(spec) + ")";

    const deadline = Date.now() + 18000;
    while (Date.now() < deadline) {
      await call("Runtime.evaluate", { expression });
      await sleep(180);
      if (logs.slice(before).some(line => line.includes("QA_POSE") && line.includes("motion=" + motion))) {
        await sleep(180);
        return;
      }
    }

    throw new Error("QA pose did not become ready: " + spec + "\n" + logs.slice(-30).join("\n"));
  }

  async function screenshot(file) {
    const shot = await call("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false,
    });
    fs.writeFileSync(path.join(OUT, file), Buffer.from(shot.data, "base64"));
  }

  const specs = [
    ["idle_front", "idle|0|front"],
    ["idle_threequarter", "idle|0|threequarter"],
    ["idle_side", "idle|0|side"],
    ["walk_000", "walk|0.00|threequarter"],
    ["walk_025", "walk|0.25|threequarter"],
    ["walk_050", "walk|0.50|threequarter"],
    ["walk_075", "walk|0.75|threequarter"],
    ["jog_000", "jog|0.00|threequarter"],
    ["jog_025", "jog|0.25|threequarter"],
    ["jog_050", "jog|0.50|threequarter"],
    ["jog_075", "jog|0.75|threequarter"],
  ];

  for (const [name, spec] of specs) {
    await setPose(spec);
    await screenshot(name + ".png");
    console.log("captured", name, spec);
  }

  fs.writeFileSync(path.join(OUT, "qa.log"), logs.join("\n"));
  fs.writeFileSync(
    path.join(OUT, "manifest.json"),
    JSON.stringify({ specs, capturedAt: new Date().toISOString() }, null, 2),
  );
  console.log("QA_CAPTURE_DONE", OUT);
}, {
  timeoutMs: 180000,
});
