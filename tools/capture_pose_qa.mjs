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

  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
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
    throw new Error("VRoid C did not become ready\n" + logs.slice(-60).join("\n"));
  }

  async function setPose(spec) {
    const motion = spec.split("|")[0];
    const before = logs.length;
    const expression =
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("SetQaState") + "," +
      JSON.stringify(spec) + ")";

    const poseDeadline = Date.now() + 10000;
    while (Date.now() < poseDeadline) {
      await call("Runtime.evaluate", { expression });
      await sleep(140);
      if (
        logs.slice(before).some(
          line => line.includes("QA_POSE") && line.includes("motion=" + motion),
        )
      ) {
        await sleep(160);
        return;
      }
    }
    throw new Error("QA pose did not become ready: " + spec + "\n" + logs.slice(-40).join("\n"));
  }

  async function screenshot(name) {
    const shot = await call("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false,
    });
    fs.writeFileSync(path.join(OUT, name + ".png"), Buffer.from(shot.data, "base64"));
  }

  const staticSpecs = [
    ["idle_front", "idle|0|front|0.20|0|0|-35"],
    ["idle_threequarter", "idle|0|threequarter|0.20|0|0|-35"],
    ["idle_side", "idle|0|side|0.20|0|0|-35"],
    ["walk_000", "walk|0.00|threequarter|0.15|0|0|-35"],
    ["walk_025", "walk|0.25|threequarter|0.15|0|0|-35"],
    ["walk_050", "walk|0.50|threequarter|0.15|0|0|-35"],
    ["walk_075", "walk|0.75|threequarter|0.15|0|0|-35"],
    ["jog_000", "jog|0.00|threequarter|0.05|0|0|-35"],
    ["jog_025", "jog|0.25|threequarter|0.05|0|0|-35"],
    ["jog_050", "jog|0.50|threequarter|0.05|0|0|-35"],
    ["jog_075", "jog|0.75|threequarter|0.05|0|0|-35"],
    ["run_000", "run|0.00|threequarter|0.03|0|0|-35"],
    ["run_025", "run|0.25|threequarter|0.03|0|0|-35"],
    ["run_050", "run|0.50|threequarter|0.03|0|0|-35"],
    ["run_075", "run|0.75|threequarter|0.03|0|0|-35"],
    ["slash_000", "slash|0.00|threequarter|0|0|90|0"],
    ["slash_020", "slash|0.20|threequarter|0|0|90|0"],
    ["slash_040", "slash|0.40|threequarter|0|0|90|0"],
    ["slash_055", "slash|0.55|threequarter|0|0|90|0"],
    ["slash_075", "slash|0.75|threequarter|0|0|90|0"],
    ["slash_095", "slash|0.95|threequarter|0|0|90|0"],
    ["dodge_000", "dodge|0.00|threequarter|0|0|90|0"],
    ["dodge_025", "dodge|0.25|threequarter|0|0|90|0"],
    ["dodge_050", "dodge|0.50|threequarter|0|0|90|0"],
    ["dodge_075", "dodge|0.75|threequarter|0|0|90|0"],
    ["dodge_095", "dodge|0.95|threequarter|0|0|90|0"],
  ];

  for (const [name, spec] of staticSpecs) {
    await setPose(spec);
    await screenshot(name);
    console.log("captured", name);
  }

  await call("Runtime.evaluate", {
    expression:
      "window.unityInstance && window.unityInstance.SendMessage(" +
      JSON.stringify("ModelQualityShowcase") + "," +
      JSON.stringify("ExitQaState") + ")",
  });
  await sleep(450);
  await screenshot("runtime_idle");

  await call("Input.dispatchKeyEvent", {
    type: "keyDown",
    code: "KeyW",
    key: "w",
    windowsVirtualKeyCode: 87,
    nativeVirtualKeyCode: 87,
  });
  for (let i = 0; i < 10; i++) {
    await sleep(110);
    await screenshot("runtime_run_" + String(i).padStart(2, "0"));
  }
  await call("Input.dispatchKeyEvent", {
    type: "keyUp",
    code: "KeyW",
    key: "w",
    windowsVirtualKeyCode: 87,
    nativeVirtualKeyCode: 87,
  });
  await sleep(350);
  await screenshot("runtime_stop");

  await call("Input.dispatchMouseEvent", {
    type: "mousePressed",
    x: 470,
    y: 380,
    button: "left",
    buttons: 1,
    clickCount: 1,
  });
  await call("Input.dispatchMouseEvent", {
    type: "mouseMoved",
    x: 590,
    y: 380,
    button: "left",
    buttons: 1,
  });
  await sleep(180);
  await call("Input.dispatchMouseEvent", {
    type: "mouseReleased",
    x: 590,
    y: 380,
    button: "left",
    buttons: 0,
    clickCount: 1,
  });
  await sleep(260);
  await screenshot("runtime_camera_orbit");

  async function tapKey(code, key, keyCode) {
    await call("Input.dispatchKeyEvent", {
      type: "keyDown",
      code,
      key,
      windowsVirtualKeyCode: keyCode,
      nativeVirtualKeyCode: keyCode,
    });
    await sleep(45);
    await call("Input.dispatchKeyEvent", {
      type: "keyUp",
      code,
      key,
      windowsVirtualKeyCode: keyCode,
      nativeVirtualKeyCode: keyCode,
    });
  }

  await tapKey("KeyJ", "j", 74);
  for (let i = 0; i < 8; i++) {
    await sleep(180);
    await screenshot("runtime_slash_" + String(i).padStart(2, "0"));
  }
  await sleep(260);

  await tapKey("KeyK", "k", 75);
  for (let i = 0; i < 7; i++) {
    await sleep(145);
    await screenshot("runtime_dodge_" + String(i).padStart(2, "0"));
  }
  await sleep(220);

  const fatal = logs.filter(line => /EXCEPTION|NullReferenceException|MissingReferenceException/.test(line));
  if (fatal.length > 0) {
    throw new Error("QA runtime logged fatal errors:\n" + fatal.slice(-20).join("\n"));
  }

  fs.writeFileSync(path.join(OUT, "qa.log"), logs.join("\n"));
  fs.writeFileSync(
    path.join(OUT, "manifest.json"),
    JSON.stringify(
      {
        staticSpecs,
        runtimeFrames: {
          locomotion: 12,
          cameraOrbit: 1,
          slash: 8,
          dodge: 7,
        },
        capturedAt: new Date().toISOString(),
      },
      null,
      2,
    ),
  );
  console.log("QA_CAPTURE_DONE", OUT);
}, {
  timeoutMs: 150000,
});
