import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Log.enable");await call("Page.enable");
const expr='window.unityInstance&&window.unityInstance.SendMessage("ModelQualityShowcase","SetQaState","swordidle|0|threequarter|0.45|0|0|0")';
await call("Runtime.evaluate",{expression:expr});
await new Promise(r=>setTimeout(r,1400));
const shot=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
fs.writeFileSync("/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/weapon-axis/check_loaded.png",Buffer.from(shot.data,"base64"));
console.log(logs.filter(x=>/QA_POSE|Loaded Avatar|bound humanoid|Exception|failed/i.test(x)).slice(-20).join("\n"));
});
