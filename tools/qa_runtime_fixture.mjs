import fs from "node:fs";
import path from "node:path";
import { withQaRuntime } from "./qa_runtime.mjs";

const [mode, identityFile, publishDir] = process.argv.slice(2);
if (!mode || !identityFile || !publishDir) {
  throw new Error("Usage: qa_runtime_fixture.mjs <mode> <identity-file> <publish-dir>");
}

await withQaRuntime(async runtime => {
  fs.writeFileSync(identityFile, JSON.stringify({
    chromePid: runtime.chromePid,
    serverPid: runtime.serverPid,
    guardPid: runtime.guardPid,
    profileDir: runtime.profileDir,
    tempDir: runtime.tempDir,
    serverPort: runtime.serverPort,
    debugPort: runtime.debugPort,
  }), "utf8");

  if (mode === "wait") {
    await runtime.sleep(60000);
    return;
  }

  if (mode === "unity-failure") {
    throw new Error("simulated Unity load failure");
  }

  if (mode === "screenshot-failure") {
    await runtime.call("Page.enable");
    const shot = await runtime.call("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false,
    });
    fs.writeFileSync(path.join("/dev/null", "qa.png"), Buffer.from(shot.data, "base64"));
    return;
  }

  throw new Error("Unknown fixture mode: " + mode);
}, {
  publishDir,
  timeoutMs: 65000,
});
