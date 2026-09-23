import { withQaRuntime } from "./qa_runtime.mjs";


await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Log.enable");
const st=await call("Runtime.evaluate",{expression:"JSON.stringify({unity:!!window.unityInstance,title:document.title})",returnByValue:true});
console.log("STATE",st.result.value);
await call("Runtime.evaluate",{expression:"window.unityInstance&&window.unityInstance.SendMessage(\"ModelQualityShowcase\",\"SetQaState\",\"swordidle|0|threequarter|0.45|0|0|0\")"});
await new Promise(r=>setTimeout(r,1800));
console.log(logs.slice(-80).join("\n"));
});
