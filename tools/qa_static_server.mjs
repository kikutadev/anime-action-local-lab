import fs from "node:fs";
import http from "node:http";
import path from "node:path";

const [rootArg, portFileArg] = process.argv.slice(2);
if (!rootArg || !portFileArg) {
  throw new Error("Usage: qa_static_server.mjs <publish-dir> <port-file>");
}

const root = path.resolve(rootArg);
const portFile = path.resolve(portFileArg);

const mimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".data", "application/octet-stream"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".jpeg", "image/jpeg"],
  [".svg", "image/svg+xml"],
  [".vrm", "application/octet-stream"],
]);

function safeStaticPath(requestUrl) {
  const url = new URL(requestUrl ?? "/", "http://127.0.0.1");
  let pathname = decodeURIComponent(url.pathname);
  if (pathname.endsWith("/")) pathname += "index.html";

  const absolute = path.resolve(root, "." + pathname);
  const rootWithSep = root.endsWith(path.sep) ? root : root + path.sep;
  if (absolute !== root && !absolute.startsWith(rootWithSep)) return null;
  return absolute;
}

const sockets = new Set();
const server = http.createServer((req, res) => {
  const file = safeStaticPath(req.url);
  if (!file) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  fs.stat(file, (statError, stat) => {
    if (statError || !stat.isFile()) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }

    res.writeHead(200, {
      "Content-Type": mimeTypes.get(path.extname(file).toLowerCase()) ?? "application/octet-stream",
      "Cache-Control": "no-store",
    });

    if (req.method === "HEAD") {
      res.end();
      return;
    }

    const stream = fs.createReadStream(file);
    stream.on("error", () => {
      if (!res.headersSent) res.writeHead(500);
      res.end();
    });
    stream.pipe(res);
  });
});

server.on("connection", socket => {
  sockets.add(socket);
  socket.once("close", () => sockets.delete(socket));
});

let shuttingDown = false;
async function shutdown(exitCode = 0) {
  if (shuttingDown) return;
  shuttingDown = true;

  server.closeAllConnections?.();
  for (const socket of sockets) socket.destroy();

  if (server.listening) {
    await new Promise(resolve => server.close(() => resolve()));
  }

  try {
    fs.rmSync(portFile, { force: true });
  } catch {}

  process.exit(exitCode);
}

process.once("SIGTERM", () => void shutdown(0));
process.once("SIGINT", () => void shutdown(0));

server.listen(0, "127.0.0.1", () => {
  const address = server.address();
  if (!address || typeof address === "string") {
    void shutdown(1);
    return;
  }

  fs.writeFileSync(portFile, String(address.port) + "\n", { encoding: "utf8" });
});
