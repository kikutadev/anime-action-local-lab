import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/weapon-axis-loaded";
fs.rmSync(OUT,{recursive:true,force:true});fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Page.enable");
const specs=[
["e_000_000_000","0|0|0"],["e_090_000_000","90|0|0"],["e_n90_000_000","-90|0|0"],
["e_000_090_000","0|90|0"],["e_000_n90_000","0|-90|0"],["e_000_180_000","0|180|0"],
["e_000_000_090","0|0|90"],["e_000_000_n90","0|0|-90"]];
for(const [name,e] of specs){
 const expr='window.unityInstance&&window.unityInstance.SendMessage("ModelQualityShowcase","SetQaState","swordidle|0|threequarter|0.45|'+e+'")';
 await call("Runtime.evaluate",{expression:expr});
 await new Promise(r=>setTimeout(r,700));
 const shot=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
 fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(shot.data,"base64"));
 console.log("captured",name);
}
});
